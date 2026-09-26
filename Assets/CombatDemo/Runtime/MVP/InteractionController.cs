using System;
using System.Collections.Generic;
using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public interface IInteractable
    {
        string StableId { get; }
        Transform Root { get; }
        Vector3 InteractionPoint { get; }
        float Radius { get; }
        bool Available(MvpWorld world);
        string Label(MvpWorld world);
        string BlockReason(MvpWorld world);
        bool Interact(MvpWorld world);
    }
    public sealed class InteractionController
    {
        readonly MvpWorld world;
        readonly List<IInteractable> objects = new List<IInteractable>();
        readonly RaycastHit[] hits = new RaycastHit[32];
        public IInteractable Focus { get; private set; }
        public InteractionController(MvpWorld world)
        {
            this.world = world;
            foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>())
                if (component is IInteractable interactable) objects.Add(interactable);
        }
        public void Refresh()
        {
            Focus = null; float bestScreen = float.MaxValue, bestDistance = float.MaxValue;
            if (!world.Running) return;
            foreach (var candidate in objects)
            {
                if (candidate.Root == null || !candidate.Root.gameObject.activeInHierarchy || !candidate.Available(world)) continue;
                Vector3 point = candidate.InteractionPoint;
                float distance = EnemyController.FlatDistance(world.player.transform.position, point);
                if (distance > candidate.Radius || Mathf.Abs(point.y - world.player.transform.position.y) > 1.5f) continue;
                var viewport = world.gameplayCamera.WorldToViewportPoint(point);
                if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1 || Occluded(candidate)) continue;
                float score = new Vector2((viewport.x - .5f) * world.gameplayCamera.aspect, viewport.y - .5f).sqrMagnitude;
                if (Focus != null && (score > bestScreen || (score == bestScreen && (distance > bestDistance ||
                    (distance == bestDistance && string.CompareOrdinal(candidate.StableId, Focus.StableId) >= 0))))) continue;
                Focus = candidate; bestScreen = score; bestDistance = distance;
            }
        }
        bool Occluded(IInteractable candidate) => Blocked(world.gameplayCamera.transform.position, candidate) ||
            Blocked(world.player.transform.position + Vector3.up, candidate);
        bool Blocked(Vector3 start, IInteractable candidate)
        {
            Vector3 delta = candidate.InteractionPoint - start;
            int count = Physics.RaycastNonAlloc(start, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return true;
            for (int i = 0; i < count; i++)
                if (!hits[i].transform.IsChildOf(world.player.transform) && !hits[i].transform.IsChildOf(candidate.Root)) return true;
            return false;
        }
        public string Prompt
        {
            get
            {
                if (Focus == null) return null;
                string reason = world.InteractionBlockReason ?? Focus.BlockReason(world);
                return reason == null ? "E " + Focus.Label(world) : Focus.Label(world) + " · " + reason;
            }
        }
        public bool TryInteract()
        {
            var displayed = Focus; Refresh();
            // A moving camera cannot silently execute a different object than the displayed prompt.
            if (displayed == null || Focus != displayed || world.InteractionBlockReason != null) return false;
            string reason = Focus.BlockReason(world);
            if (reason != null) { world.Flow.Notify(reason); return false; }
            return Focus.Interact(world);
        }
    }
}

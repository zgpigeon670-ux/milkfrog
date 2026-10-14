using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace Milkfrog.CombatDemo
{
    public sealed class CharacterPreviewView : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RawImage image;
        GameObject rig, model;
        Camera previewCamera;
        RenderTexture texture;
        PlayableGraph graph;
        EquipmentVisuals visuals;
        MvpWorld world;
        float yaw;
        Quaternion baseRotation;
        public bool HasResources => rig != null && texture != null && graph.IsValid();
        public float Yaw => yaw;
        public Quaternion ModelRotation => model != null ? model.transform.localRotation : Quaternion.identity;
        public void Show(MvpWorld owner)
        {
            Release(); world = owner;
            // Dedicated headless test/server processes have no graphics device.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            int layer = LayerMask.NameToLayer("InventoryPreview");
            if (layer < 0) { Debug.LogError("Missing InventoryPreview layer."); return; }
            rig = new GameObject("Inventory Preview Rig"); rig.transform.position = new Vector3(-1000,-1000,-1000);
            var presenter = world.player.GetComponent<CombatAnimationPresenter>();
            model = Instantiate(presenter.animator.gameObject, rig.transform, false); model.name = "Preview Model";
            baseRotation = model.transform.localRotation;
            foreach (var part in model.GetComponentsInChildren<EquipmentVisualPart>(true)) Destroy(part.gameObject);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            foreach (var trail in model.GetComponentsInChildren<TrailRenderer>(true)) { trail.Clear(); trail.enabled = false; }
            foreach (var child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            var animator = model.GetComponent<Animator>(); animator.applyRootMotion = false; animator.runtimeAnimatorController = null;
            graph = PlayableGraph.Create("Inventory Idle Preview"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var clip = AnimationClipPlayable.Create(graph, presenter.profile.idle);
            AnimationPlayableOutput.Create(graph,"Preview",animator).SetSourcePlayable(clip); graph.Play(); graph.Evaluate(0);
            visuals = rig.AddComponent<EquipmentVisuals>(); visuals.modelRoot = model.transform; visuals.Apply(world.Inventory,world.FindItem);
            var rect = image.rectTransform.rect;
            int height = Mathf.Max(1, Mathf.RoundToInt(512 * rect.height / Mathf.Max(1,rect.width)));
            texture = new RenderTexture(512,height,24,RenderTextureFormat.ARGB32) { name = "Inventory Portrait", antiAliasing = 2 }; texture.Create(); image.texture = texture;
            var cameraObject = new GameObject("Preview Camera"); cameraObject.transform.SetParent(rig.transform,false);
            previewCamera = cameraObject.AddComponent<Camera>(); previewCamera.cullingMask = 1 << layer; previewCamera.targetTexture = texture;
            previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = new Color(.065f,.09f,.115f,1);
            previewCamera.orthographic = true; previewCamera.nearClipPlane = .01f; previewCamera.farClipPlane = 10;
            previewCamera.aspect = (float)texture.width / texture.height;
            var bounds = Bounds(); previewCamera.orthographicSize = Mathf.Max(.5f,bounds.size.y * .60f,bounds.size.x * .60f / previewCamera.aspect);
            previewCamera.transform.position = bounds.center + new Vector3(0, .03f, 4);
            previewCamera.transform.LookAt(bounds.center);
            var lightObject = new GameObject("Preview Light"); lightObject.transform.SetParent(rig.transform,false);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.8f; light.cullingMask = 1 << layer;
            light.transform.rotation = Quaternion.Euler(25,210,0); yaw = 0;
        }
        Bounds Bounds()
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true); var bounds = new Bounds(model.transform.position,Vector3.zero);
            bool first = true;
            foreach(var renderer in renderers) if (!(renderer is TrailRenderer)) { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
            return bounds;
        }
        public void RefreshEquipment() { if (visuals != null && world != null) visuals.Apply(world.Inventory,world.FindItem); }
        public void OnBeginDrag(PointerEventData data) { }
        public void OnDrag(PointerEventData data)
        {
            if (rig == null || data.button != PointerEventData.InputButton.Left) return;
            yaw += data.delta.x * .5f; model.transform.localRotation = Quaternion.Euler(0,yaw,0) * baseRotation;
        }
        void Update() { if (graph.IsValid()) { graph.Evaluate(Time.unscaledDeltaTime); model.transform.localRotation = Quaternion.Euler(0,yaw,0) * baseRotation; } }
        void OnDisable() => Release();
        void OnDestroy() => Release();
        public void Release()
        {
            if (graph.IsValid()) graph.Destroy();
            if (previewCamera != null) { previewCamera.enabled = false; previewCamera.targetTexture = null; }
            if (image != null) image.texture = null;
            if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
            if (rig != null) { rig.SetActive(false); Destroy(rig); rig = null; } world = null;
        }
    }
}

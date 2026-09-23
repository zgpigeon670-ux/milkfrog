using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class CombatActorView : MonoBehaviour
    {
        public CombatActor actor;
        public Renderer body;
        public Transform attackMarker;
        public Renderer attackRenderer;
        public TextMesh label;
        public Camera viewCamera;
        MaterialPropertyBlock colors;
        public bool cacheLabels = true;
        public bool retainFactionColor;
        public bool diagnosticsOnly;
        CombatDebugHud debugHud;
        Color stateColor;
        Color flashColor;
        float flashRemaining, flashDuration;
        public float FlashAmount => flashDuration > 0 ? Mathf.Clamp01(flashRemaining / flashDuration) : 0;
        CombatState shownState = (CombatState)(-1);
        bool shownWindow;
        public void Flash(Color color, float duration)
        {
            flashColor = color; flashDuration = flashRemaining = Mathf.Max(.001f, duration); PaintBody();
        }
        public void TickFlash(float dt) { if (flashRemaining <= 0) return; flashRemaining = Mathf.Max(0, flashRemaining - dt); PaintBody(); }
        public void ResetFlash() { flashRemaining = flashDuration = 0; PaintBody(); }
        void PaintBody()
        {
            if (body == null || colors == null) return;
            colors.Clear();
            bool shaderFlash = body.sharedMaterial != null && body.sharedMaterial.HasProperty("_FlashAmount");
            colors.SetColor(BaseColor, shaderFlash ? stateColor : Color.Lerp(stateColor, flashColor, FlashAmount));
            colors.SetColor(ColorId, stateColor);
            colors.SetFloat("_FlashAmount", Mathf.Sqrt(FlashAmount));
            colors.SetColor("_FlashColor", flashColor);
            body.SetPropertyBlock(colors);
        }
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Start()
        {
            colors = new MaterialPropertyBlock();
            debugHud = FindFirstObjectByType<CombatDebugHud>();
            actor.Core.StateChanged += OnState;
            OnState(actor.Core.State, actor.Core.State);
        }

        void OnDestroy() { if (actor != null && actor.Core != null) actor.Core.StateChanged -= OnState; }

        void OnState(CombatState previous, CombatState next)
        {
            Color color = actor.isPlayer ? new Color(.25f, .8f, .95f) : new Color(.95f, .35f, .3f);
            switch (next)
            {
                case CombatState.Guard: color = new Color(.3f, .55f, 1); break;
                case CombatState.AttackStartup: color = new Color(1, .72f, .15f); break;
                case CombatState.AttackActive: color = new Color(1, .22f, .1f); break;
                case CombatState.AttackRecovery: color = new Color(.6f, .42f, .2f); break;
                case CombatState.HitStun: color = Color.magenta; break;
                case CombatState.DeflectedStun: color = Color.white; break;
                case CombatState.PostureBroken: color = new Color(1, .3f, .8f); break;
                case CombatState.Dead: color = new Color(.2f, .22f, .25f); break;
            }
            stateColor = retainFactionColor ? (actor.isPlayer ? new Color(.25f, .8f, .95f) : new Color(.95f, .35f, .3f)) : color;
            PaintBody();
            if (attackRenderer != null) Paint(attackRenderer, color);
        }

        void Paint(Renderer target, Color color)
        {
            if (target == null) return;
            colors.Clear();
            colors.SetColor(BaseColor, color);
            colors.SetColor(ColorId, color);
            target.SetPropertyBlock(colors);
        }

        void LateUpdate()
        {
            if (actor.Core == null) return;
            bool diagnostics = !diagnosticsOnly || (debugHud != null && debugHud.showDebug);
            label.gameObject.SetActive(diagnostics);
            bool attacking = diagnostics && actor.Core.IsAttacking;
            attackMarker.gameObject.SetActive(attacking);
            if (attacking)
            {
                float range = actor.Core.Tuning.range;
                attackMarker.position = actor.transform.position + Vector3.up * .04f + actor.AttackForward * range * .5f;
                attackMarker.rotation = Quaternion.LookRotation(actor.AttackForward);
                attackMarker.localScale = new Vector3(actor.Core.Tuning.attackHalfWidth * 2, .035f, range);
            }
            if (!cacheLabels || shownState != actor.Core.State || shownWindow != actor.Core.DeflectOpen)
            {
            shownState = actor.Core.State; shownWindow = actor.Core.DeflectOpen;
            label.text = (actor.isPlayer ? "PLAYER" : "ENEMY") + "\n" + actor.Core.State +
                (actor.Core.DeflectOpen ? "\nDEFLECT WINDOW" : "");
            }
            if (viewCamera != null) label.transform.rotation = viewCamera.transform.rotation;
        }
    }
}


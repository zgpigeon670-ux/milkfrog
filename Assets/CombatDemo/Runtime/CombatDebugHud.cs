using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class CombatDebugHud : MonoBehaviour
    {
        public CombatDemoSession session;
        GUIStyle title, text, small;
        public bool cacheText = true;
        public bool showDebug = true;
        readonly string[] actorText = new string[6];
        string lastEvent, decision;
        float nextRefresh;
        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current?.f2Key.wasPressedThisFrame == true) showDebug = !showDebug;
            if (cacheText && showDebug && session != null && session.player.Core != null && Time.unscaledTime >= nextRefresh)
            { RefreshText(); nextRefresh = Time.unscaledTime + .1f; }
        }
        void RefreshText()
        {
            for (int i = 0; i < 2; i++)
            {
                var actor = i == 0 ? session.player : session.enemy; var core = actor.Core;
                actorText[i * 3] = (actor.isPlayer ? "PLAYER   " : "ENEMY   ") + core.State + "   " + core.Remaining.ToString("F2") + "s";
                actorText[i * 3 + 1] = "HP " + core.Health.ToString("F0") + "/" + core.Tuning.maxHealth + "   POSTURE " + core.Posture.ToString("F0") + "/" + core.Tuning.maxPosture;
                actorText[i * 3 + 2] = "Deflect window: " + (core.DeflectOpen ? "OPEN " : "closed ") + core.DeflectRemaining.ToString("F3") + "s    Attack #" + core.AttackId;
            }
            lastEvent = "Last event: " + session.LastHit;
            decision = "Mode: " + session.mode + "    AI: " + session.Brain.Decision + "    Blocks: " + session.Brain.Blocks + "/" + session.settings.blocksBeforeDeflect + "    Wait: " + session.Brain.WaitRemaining.ToString("F2");
        }
        void OnGUI()
        {
            if (session == null || session.player.Core == null || !showDebug) return;
            if (!cacheText || lastEvent == null) RefreshText();
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
                text = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                title.normal.textColor = text.normal.textColor = small.normal.textColor = Color.white;
            }
            Matrix4x4 saved = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1100f, Screen.height / 700f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            Panel(new Rect(16, 16, 1068, 158));
            GUI.Label(new Rect(32, 24, 1000, 32), "MILKFROG / COMBAT LAB", title);
            ActorPanel(session.player, 32, 52);
            ActorPanel(session.enemy, 562, 52);
            GUI.Label(new Rect(32, 145, 1000, 24), lastEvent, text);

            float bottom = Screen.height / scale - 138;
            Panel(new Rect(16, bottom, 1068, 122));
            GUI.Label(new Rect(32, bottom + 10, 1000, 24), "WASD Move    LMB Attack / Deathblow    RMB Guard / Deflect    R Reset    F1 Mode    F2 HUD", text);
            GUI.Label(new Rect(32, bottom + 38, 1000, 24), decision, text);
            GUI.Label(new Rect(32, bottom + 65, 1000, 22), "Gold = Startup | Red = Active | Brown = Recovery | Blue = Guard | Pink = Broken | White = Deflected", small);
            GUI.Label(new Rect(32, bottom + 88, 1000, 22), "Duel: attack twice into guard; the third attack is deflected. Release and tap RMB just before the counter lands.", small);
            if (session.Finished)
            {
                Panel(new Rect(335, 285, 430, 94));
                GUI.Label(new Rect(358, 297, 400, 36), session.enemy.Core.State == CombatState.Dead ? "ENEMY DEFEATED" : "PLAYER DEFEATED", title);
                GUI.Label(new Rect(358, 341, 400, 28), "Press R to restart the round", text);
            }
            GUI.matrix = saved;
        }

        void ActorPanel(CombatActor actor, float x, float y)
        {
            var core = actor.Core;
            GUI.Label(new Rect(x, y, 510, 24), actorText[actor.isPlayer ? 0 : 3], text);
            GUI.Label(new Rect(x, y + 27, 500, 24), actorText[actor.isPlayer ? 1 : 4], text);
            Bar(new Rect(x, y + 55, 215, 9), core.Health / core.Tuning.maxHealth, new Color(.3f, .8f, .6f));
            Bar(new Rect(x + 235, y + 55, 215, 9), core.Posture / core.Tuning.maxPosture, new Color(1, .65f, .15f));
            GUI.Label(new Rect(x, y + 68, 500, 22), actorText[actor.isPlayer ? 2 : 5], small);
        }

        static void Bar(Rect rect, float value, Color color)
        {
            var previous = GUI.color;
            GUI.color = new Color(.12f, .15f, .2f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            rect.width *= Mathf.Clamp01(value);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void Panel(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(.025f, .035f, .05f, .97f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}


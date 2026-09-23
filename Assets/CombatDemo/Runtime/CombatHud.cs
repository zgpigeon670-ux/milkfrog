using UnityEngine;

namespace Milkfrog.CombatDemo
{
    // Essential combat information has no dependency on the optional F2 diagnostics.
    public sealed class CombatHud : MonoBehaviour
    {
        public CombatDemoSession session;
        GUIStyle nameStyle, hintStyle, centerStyle, resultStyle;
        readonly Color ink = new Color(.045f, .035f, .025f, .88f);
        readonly Color bronze = new Color(.51f, .41f, .24f);
        readonly Color gold = new Color(.95f, .64f, .18f);
        readonly Color red = new Color(.72f, .10f, .08f);
        float playerTrail = 1, enemyTrail = 1;

        void OnEnable() { if (session != null) session.RoundReset += ResetBars; }
        void OnDisable() { if (session != null) session.RoundReset -= ResetBars; }
        void ResetBars() => playerTrail = enemyTrail = 1;
        void Update()
        {
            if (session == null || session.player.Core == null) return;
            playerTrail = Mathf.MoveTowards(playerTrail, Health(session.player), Time.unscaledDeltaTime * .3f);
            enemyTrail = Mathf.MoveTowards(enemyTrail, Health(session.enemy), Time.unscaledDeltaTime * .3f);
        }
        static float Health(CombatActor actor) => Mathf.Clamp01(actor.Core.Health / actor.Core.Tuning.maxHealth);
        static float Posture(CombatActor actor) => Mathf.Clamp01(actor.Core.Posture / actor.Core.Tuning.maxPosture);

        void OnGUI()
        {
            if (session == null || session.player.Core == null) return;
            if (nameStyle == null)
            {
                nameStyle = Style(19, TextAnchor.MiddleLeft, new Color(.91f, .85f, .71f));
                hintStyle = Style(13, TextAnchor.MiddleLeft, new Color(.78f, .75f, .66f));
                centerStyle = Style(15, TextAnchor.MiddleCenter, new Color(.95f, .77f, .42f));
                resultStyle = Style(38, TextAnchor.MiddleCenter, new Color(.90f, .24f, .16f));
            }
            var matrix = GUI.matrix; var previousColor = GUI.color; int depth = GUI.depth;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale); GUI.color = Color.white; GUI.depth = 10;
            GUI.Label(new Rect(48, 24, 380, 30), "TRAINING SWORDSMAN", nameStyle);
            HealthBar(new Rect(48, 59, 300, 13), Health(session.enemy), enemyTrail);
            Diamond(35, 65, 5, red);
            PostureBar(new Rect(width * .5f - 210, 43, 420, 13), Posture(session.enemy));
            GUI.Label(new Rect(width * .5f - 150, 61, 300, 23), "ENEMY POSTURE", centerStyle);

            GUI.Label(new Rect(48, height - 102, 250, 26), "WOLF / VITALITY", nameStyle);
            HealthBar(new Rect(48, height - 72, 265, 17), Health(session.player), playerTrail);
            Diamond(35, height - 63, 6, red);
            PostureBar(new Rect(width * .5f - 170, height - 75, 340, 12), Posture(session.player));
            GUI.Label(new Rect(width * .5f - 150, height - 58, 300, 22), "POSTURE", centerStyle);
            GUI.Label(new Rect(48, height - 35, width - 96, 24), "WASD  Move     LMB  Attack     RMB  Guard / Deflect     R  Restart     F1  Training mode     F2  Diagnostics", hintStyle);

            if (!session.Finished && session.enemy.Core.State == CombatState.PostureBroken)
            {
                Diamond(width * .5f, height * .53f, 10, red);
                GUI.Label(new Rect(width * .5f - 180, height * .53f + 18, 360, 28), "POSTURE BROKEN  /  LMB DEATHBLOW", centerStyle);
            }
            if (session.Finished)
            {
                Fill(new Rect(0, height * .40f, width, 114), new Color(.04f, .02f, .015f, .72f));
                GUI.Label(new Rect(0, height * .40f + 10, width, 58), session.player.Core.State == CombatState.Dead ? "DEATH" : "SHINOBI EXECUTION", resultStyle);
                GUI.Label(new Rect(0, height * .40f + 72, width, 26), "R  /  RESTART", centerStyle);
            }
            GUI.matrix = matrix; GUI.color = previousColor; GUI.depth = depth;
        }

        GUIStyle Style(int size, TextAnchor anchor, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, fontStyle = FontStyle.Bold };
            style.normal.textColor = color; return style;
        }
        void HealthBar(Rect r, float value, float trail)
        {
            Frame(r); Fill(new Rect(r.x, r.y, r.width * Mathf.Max(value, trail), r.height), new Color(.65f, .42f, .25f));
            Fill(new Rect(r.x, r.y, r.width * value, r.height), red);
            Fill(new Rect(r.x, r.y, r.width * value, 2), new Color(.95f, .39f, .26f));
        }
        void PostureBar(Rect r, float value)
        {
            Frame(r); float half = r.width * .5f;
            var color = Color.Lerp(gold, new Color(1, .27f, .08f), Mathf.InverseLerp(.7f, 1, value));
            Fill(new Rect(r.center.x - half * value, r.y, r.width * value, r.height), color);
            Fill(new Rect(r.center.x - 1, r.y - 3, 2, r.height + 6), bronze);
            Diamond(r.x - 9, r.center.y, 4, bronze); Diamond(r.xMax + 9, r.center.y, 4, bronze);
        }
        void Frame(Rect r)
        {
            Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), ink);
            Fill(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), bronze); Fill(r, ink);
        }
        static void Diamond(float x, float y, float radius, Color color)
        {
            var matrix = GUI.matrix; GUIUtility.RotateAroundPivot(45, new Vector2(x, y));
            Fill(new Rect(x - radius, y - radius, radius * 2, radius * 2), color); GUI.matrix = matrix;
        }
        static void Fill(Rect r, Color color)
        {
            var previous = GUI.color; GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = previous;
        }
    }
}

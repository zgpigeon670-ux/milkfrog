using System;
using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public sealed class MvpHud : MonoBehaviour
    {
        public Func<MvpHudData> Read;
        public bool showDebug;

        const float RefreshInterval = .1f;
        MvpHudData data;
        float nextRefreshTime;

        GUIStyle encounterStyle;
        GUIStyle bossStyle;
        GUIStyle centerStyle;
        GUIStyle hintStyle;
        GUIStyle systemStyle;
        GUIStyle debugStyle;
        GUIStyle enemyStyle;
        GUIStyle enemyStatusStyle;

        static readonly Color Ink = new Color(.045f, .035f, .025f, .92f);
        static readonly Color Bronze = new Color(.51f, .41f, .24f);
        static readonly Color Gold = new Color(.95f, .64f, .18f);
        static readonly Color Red = new Color(.72f, .10f, .08f);
        static readonly Color Text = new Color(.91f, .85f, .71f);
        static readonly Color MutedText = new Color(.78f, .75f, .66f);

        void Update()
        {
            if (Time.unscaledTime >= nextRefreshTime)
                Refresh();
        }

        public void Refresh()
        {
            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            data = Read == null ? null : Read();
        }

        void OnGUI()
        {
            if (data == null || !data.visible)
                return;

            EnsureStyles();

            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            if (scale <= 0f)
                return;

            float width = Screen.width / scale;
            float height = Screen.height / scale;
            try
            {
                GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
                GUI.color = Color.white;
                GUI.depth = 10;

                DrawEncounter(width);
                DrawSystemMessage(width);
                DrawBoss(width);
                DrawPlayerBars(width, height);
                DrawReticle(width, height);
                DrawEnemies(width, scale);

                if (showDebug && !string.IsNullOrEmpty(data.debug))
                {
                    float debugWidth = Mathf.Clamp(width * .5f - 270f, 170f, 360f);
                    GUI.Label(new Rect(48f, 78f, debugWidth, 92f), data.debug, debugStyle);
                }

                GUI.Label(new Rect(28f, height - 31f, width - 56f, 22f),
                    "WASD MOVE   MOUSE LOOK   MMB LOCK   SPACE DODGE   LMB ATTACK   RMB GUARD   ESC MENU",
                    hintStyle);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                GUI.depth = previousDepth;
            }
        }

        void EnsureStyles()
        {
            if (encounterStyle != null)
                return;

            encounterStyle = Style(17, Text, TextAnchor.MiddleLeft);
            bossStyle = Style(17, Text, TextAnchor.MiddleCenter);
            centerStyle = Style(13, MutedText, TextAnchor.MiddleCenter);
            hintStyle = Style(12, MutedText, TextAnchor.MiddleCenter);
            systemStyle = Style(16, Gold, TextAnchor.MiddleCenter);
            systemStyle.wordWrap = false;
            debugStyle = Style(12, MutedText, TextAnchor.UpperLeft);
            debugStyle.wordWrap = true;
            enemyStyle = Style(13, Text, TextAnchor.MiddleCenter);
            enemyStyle.clipping = TextClipping.Clip;
            enemyStatusStyle = Style(11, Gold, TextAnchor.MiddleCenter);
            enemyStatusStyle.clipping = TextClipping.Clip;
        }

        static GUIStyle Style(int fontSize, Color color, TextAnchor anchor)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = anchor,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = color;
            return style;
        }

        void DrawEncounter(float width)
        {
            string encounter = string.IsNullOrEmpty(data.encounter) ? "EXPLORATION" : data.encounter;
            GUI.Label(new Rect(48f, 39f, Mathf.Max(160f, width * .5f - 280f), 27f),
                "ENCOUNTER  /  " + encounter, encounterStyle);
        }

        void DrawSystemMessage(float width)
        {
            if (string.IsNullOrEmpty(data.message))
                return;

            GUI.Label(new Rect(30f, 5f, width - 60f, 27f), data.message, systemStyle);
        }

        void DrawBoss(float width)
        {
            if (string.IsNullOrEmpty(data.bossName))
                return;

            float barWidth = Mathf.Min(420f, Mathf.Max(300f, width - 80f));
            float x = (width - barWidth) * .5f;
            GUI.Label(new Rect(x, 39f, barWidth, 23f), data.bossName, bossStyle);
            DrawHealthBar(new Rect(x, 65f, barWidth, 12f), Ratio(data.bossHealth, data.bossMaxHealth), Red);
            DrawPostureBar(new Rect(x, 87f, barWidth, 7f), Ratio(data.bossPosture, data.bossMaxPosture));
        }

        void DrawPlayerBars(float width, float height)
        {
            GUI.Label(new Rect(48f, height - 111f, 280f, 24f), "VITALITY", encounterStyle);
            DrawHealthBar(new Rect(48f, height - 82f, 280f, 15f), Ratio(data.health, data.maxHealth), Red);

            float center = width * .5f;
            DrawPostureBar(new Rect(center - 170f, height - 82f, 340f, 12f),
                Ratio(data.posture, data.maxPosture));
            GUI.Label(new Rect(center - 150f, height - 63f, 300f, 20f), "POSTURE", centerStyle);

            if (data.charging || data.charge > 0f)
            {
                Rect chargeBar = new Rect(center - 78f, height - 137f, 156f, 6f);
                DrawThinBar(chargeBar, Mathf.Clamp01(data.charge), Gold);
                GUI.Label(new Rect(center - 170f, height - 161f, 340f, 20f),
                    data.charge >= 1f ? "READY  /  RELEASE" : "CHARGING", centerStyle);
            }
        }

        void DrawReticle(float width, float height)
        {
            float x = width * .5f;
            float y = height * .5f;
            Color reticle = new Color(.95f, .85f, .64f, .88f);
            Fill(new Rect(x - 7f, y - .5f, 4f, 1f), reticle);
            Fill(new Rect(x + 3f, y - .5f, 4f, 1f), reticle);
            Fill(new Rect(x - .5f, y - 7f, 1f, 4f), reticle);
            Fill(new Rect(x - .5f, y + 3f, 1f, 4f), reticle);

            if (data.camera == null || !data.lockPoint.HasValue)
                return;

            Vector2 lockScreen;
            if (!TryProject(data.camera, data.lockPoint.Value, out lockScreen))
                return;

            DrawLockFrame(lockScreen.x / CurrentScale, (Screen.height - lockScreen.y) / CurrentScale);
        }

        float CurrentScale
        {
            get { return Mathf.Min(Screen.width / 1280f, Screen.height / 720f); }
        }

        void DrawEnemies(float width, float scale)
        {
            if (data.camera == null || data.enemies == null)
                return;

            for (int i = 0; i < data.enemies.Length; i++)
            {
                MvpEnemyBar enemy = data.enemies[i];
                if (!enemy.visible)
                    continue;

                Vector2 screen;
                if (!TryProject(data.camera, enemy.point, out screen))
                    continue;

                float centerX = screen.x / scale;
                float headY = Mathf.Clamp((Screen.height - screen.y) / scale, 48f, Screen.height / scale - 100f);
                float barWidth = Mathf.Min(164f, width - 48f);
                float x = Mathf.Clamp(centerX - barWidth * .5f, 8f, width - barWidth - 8f);
                string label = string.IsNullOrEmpty(enemy.name) ? enemy.status : enemy.name;
                GUI.Label(new Rect(x, headY - 43f, barWidth, 17f), label ?? string.Empty, enemyStyle);
                if (!string.IsNullOrEmpty(enemy.name) && !string.IsNullOrEmpty(enemy.status))
                    GUI.Label(new Rect(x, headY - 28f, barWidth, 15f), enemy.status, enemyStatusStyle);

                float barY = headY - 10f;
                DrawThinBar(new Rect(x, barY, barWidth, 5f), Ratio(enemy.health, enemy.maxHealth), Red);
                DrawThinBar(new Rect(x, barY + 7f, barWidth, 4f),
                    Ratio(enemy.posture, enemy.maxPosture), Gold);
            }
        }

        static bool TryProject(Camera camera, Vector3 worldPoint, out Vector2 screenPoint)
        {
            screenPoint = Vector2.zero;
            if (camera == null)
                return false;

            Vector3 viewport = camera.WorldToViewportPoint(worldPoint);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                return false;

            Vector3 projected = camera.WorldToScreenPoint(worldPoint);
            Rect pixelRect = camera.pixelRect;
            if (!pixelRect.Contains(new Vector2(projected.x, projected.y)))
                return false;

            screenPoint = new Vector2(projected.x, projected.y);
            return true;
        }

        static void DrawLockFrame(float x, float y)
        {
            const float half = 19f;
            const float length = 8f;
            const float thickness = 2f;
            Color color = Gold;
            DrawCorner(x - half, y - half, -1f, -1f, length, thickness, color);
            DrawCorner(x + half, y - half, 1f, -1f, length, thickness, color);
            DrawCorner(x - half, y + half, -1f, 1f, length, thickness, color);
            DrawCorner(x + half, y + half, 1f, 1f, length, thickness, color);
        }

        static void DrawCorner(float x, float y, float horizontal, float vertical, float length, float thickness, Color color)
        {
            float horizontalX = horizontal < 0f ? x - length : x;
            float horizontalY = vertical < 0f ? y : y - thickness;
            float verticalX = horizontal < 0f ? x : x - thickness;
            float verticalY = vertical < 0f ? y : y - length;
            Fill(new Rect(horizontalX, horizontalY, length, thickness), color);
            Fill(new Rect(verticalX, verticalY, thickness, length), color);
        }

        static void DrawHealthBar(Rect rect, float value, Color fill)
        {
            Frame(rect);
            float width = Mathf.Max(0f, rect.width - 2f) * Mathf.Clamp01(value);
            Fill(new Rect(rect.x + 1f, rect.y + 1f, width, Mathf.Max(0f, rect.height - 2f)), fill);
            if (width > 0f)
                Fill(new Rect(rect.x + 1f, rect.y + 1f, width, 2f), new Color(1f, .38f, .25f, .95f));
        }

        static void DrawPostureBar(Rect rect, float value)
        {
            Frame(rect);
            value = Mathf.Clamp01(value);
            float fillWidth = Mathf.Max(0f, rect.width - 2f) * value;
            Color color = Color.Lerp(Gold, new Color(1f, .27f, .08f), Mathf.InverseLerp(.7f, 1f, value));
            Fill(new Rect(rect.center.x - fillWidth * .5f, rect.y + 1f, fillWidth, Mathf.Max(0f, rect.height - 2f)), color);
        }

        static void DrawThinBar(Rect rect, float value, Color fill)
        {
            Fill(rect, Ink);
            Rect inner = new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f));
            Fill(inner, Bronze);
            Fill(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(value), inner.height), fill);
        }

        static float Ratio(float value, float maximum)
        {
            return maximum > 0f ? Mathf.Clamp01(value / maximum) : 0f;
        }

        static void Frame(Rect rect)
        {
            Fill(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), Ink);
            Fill(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), Bronze);
            Fill(rect, Ink);
        }

        static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }

    public sealed class MvpHudData
    {
        public bool visible = true;
        public float health;
        public float maxHealth = 100f;
        public float posture;
        public float maxPosture = 100f;
        public float charge;
        public bool charging;
        public string encounter = "EXPLORATION";
        public string message = string.Empty;
        public string debug = string.Empty;
        public Camera camera;
        public Vector3? lockPoint;
        public string bossName;
        public float bossHealth;
        public float bossMaxHealth = 600f;
        public float bossPosture;
        public float bossMaxPosture = 300f;
        public MvpEnemyBar[] enemies = Array.Empty<MvpEnemyBar>();
    }

    public struct MvpEnemyBar
    {
        public string name;
        public string status;
        public Vector3 point;
        public float health;
        public float maxHealth;
        public float posture;
        public float maxPosture;
        public bool visible;
    }
}

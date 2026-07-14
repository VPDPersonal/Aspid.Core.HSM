using System.Collections.Generic;
using UnityEngine;

namespace Aspid.Core.HSM.Sample
{
    /// <summary>
    /// Runtime-built IMGUI styles and drawing helpers shared by all screens.
    /// No assets required — solid textures are generated on demand.
    /// </summary>
    public static class GameStyles
    {
        private static readonly Dictionary<Color32, Texture2D> Textures = new();

        // Palette
        public static readonly Color Night = new(0.07f, 0.08f, 0.12f);
        public static readonly Color Panel = new(0.12f, 0.14f, 0.20f, 0.96f);
        public static readonly Color PanelLight = new(0.18f, 0.21f, 0.30f);
        public static readonly Color Accent = new(1.00f, 0.72f, 0.22f);
        public static readonly Color Grass = new(0.16f, 0.30f, 0.18f);
        public static readonly Color GrassLight = new(0.20f, 0.37f, 0.22f);
        public static readonly Color BloodRed = new(0.24f, 0.08f, 0.09f);
        public static readonly Color HpGreen = new(0.35f, 0.78f, 0.35f);
        public static readonly Color HpRed = new(0.86f, 0.30f, 0.26f);
        public static readonly Color SkyBlue = new(0.34f, 0.62f, 0.86f);

        public static Texture2D Tex(Color color)
        {
            Color32 key = color;
            if (Textures.TryGetValue(key, out var tex) && tex != null)
                return tex;

            tex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            Textures[key] = tex;
            return tex;
        }

        private static GUIStyle _title;
        public static GUIStyle Title => _title ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 42,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Accent },
        };

        private static GUIStyle _heading;
        public static GUIStyle Heading => _heading ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
        };

        private static GUIStyle _body;
        public static GUIStyle Body => _body ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            richText = true,
            normal = { textColor = new Color(0.9f, 0.9f, 0.95f) },
        };

        private static GUIStyle _hint;
        public static GUIStyle Hint => _hint ??= new GUIStyle(Body)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.62f, 0.65f, 0.72f) },
        };

        private static GUIStyle _button;
        public static GUIStyle Button => _button ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            fixedHeight = 44,
            normal =
            {
                background = Tex(PanelLight),
                textColor = Color.white,
            },
            hover =
            {
                background = Tex(new Color(0.26f, 0.30f, 0.42f)),
                textColor = Accent,
            },
            active =
            {
                background = Tex(Accent),
                textColor = Night,
            },
        };

        public static void DrawBackground(Color color)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Tex(color));
        }

        public static void DrawRect(Rect rect, Color color)
        {
            GUI.DrawTexture(rect, Tex(color));
        }

        public static void DrawBar(Rect rect, float fill01, Color fillColor, string label = null)
        {
            DrawRect(rect, new Color(0f, 0f, 0f, 0.55f));

            var inner = new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * Mathf.Clamp01(fill01), rect.height - 4);
            if (inner.width > 0)
                DrawRect(inner, fillColor);

            if (!string.IsNullOrEmpty(label))
                GUI.Label(rect, label, Body);
        }

        /// <summary>Rect of the given size horizontally centered on screen.</summary>
        public static Rect Centered(float width, float height, float y)
        {
            return new Rect((Screen.width - width) / 2f, y, width, height);
        }

        /// <summary>A vertical stack of menu buttons; returns the index of the clicked one or -1.</summary>
        public static int ButtonColumn(float y, float width, params string[] labels)
        {
            var clicked = -1;
            for (var i = 0; i < labels.Length; i++)
            {
                if (GUI.Button(Centered(width, 44, y + i * 56), labels[i], Button))
                    clicked = i;
            }

            return clicked;
        }
    }
}

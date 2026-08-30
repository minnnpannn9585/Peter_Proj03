using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// Builds the plain uGUI objects the HUD needs. Centralised so every panel gets the same
    /// font, the same colours and the same rect handling, and so the HUD can be constructed
    /// without a hand-authored scene hierarchy.
    /// </summary>
    public static class HudFactory
    {
        public static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.09f, 0.88f);
        public static readonly Color CardColor = new Color(0.20f, 0.28f, 0.36f, 0.95f);
        public static readonly Color CardDimColor = new Color(0.16f, 0.16f, 0.18f, 0.75f);
        public static readonly Color SelectedColor = new Color(0.24f, 0.52f, 0.34f, 0.98f);
        public static readonly Color TextColor = new Color(0.93f, 0.95f, 0.97f, 1f);
        public static readonly Color MutedTextColor = new Color(0.66f, 0.70f, 0.74f, 1f);
        public static readonly Color WarnColor = new Color(0.94f, 0.35f, 0.28f, 1f);
        public static readonly Color FullColor = new Color(0.98f, 0.78f, 0.22f, 1f);
        public static readonly Color GoodColor = new Color(0.40f, 0.86f, 0.48f, 1f);
        public static readonly Color FillColor = new Color(0.30f, 0.68f, 0.42f, 1f);

        static Font cachedFont;

        /// <summary>
        /// The built-in runtime font. Unity renamed Arial.ttf to LegacyRuntime.ttf, so both names
        /// are tried before giving up; a null font still renders (uGUI falls back) but logs once.
        /// </summary>
        public static Font DefaultFont
        {
            get
            {
                if (cachedFont != null)
                {
                    return cachedFont;
                }

                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                if (cachedFont == null)
                {
                    Debug.LogWarning("HudFactory could not load a built-in font; HUD labels may " +
                                     "render with the uGUI fallback.");
                }

                return cachedFont;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent, RectSpec spec)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 0;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            spec.ApplyTo(rect);
            return rect;
        }

        /// <summary>
        /// Turns an existing GameObject into a positioned panel, so a panel component can be the
        /// root of its own subtree instead of hanging off a separate holder object.
        /// </summary>
        public static RectTransform Configure(
            GameObject go, Transform parent, RectSpec spec, Color background)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = go.AddComponent<RectTransform>();
            }

            rect.SetParent(parent, false);
            spec.ApplyTo(rect);

            if (background.a > 0f)
            {
                Image image = go.GetComponent<Image>();
                if (image == null)
                {
                    image = go.AddComponent<Image>();
                }

                image.color = background;
                image.raycastTarget = false;
            }

            return rect;
        }

        /// <summary>Fully transparent, for panels that draw no backdrop of their own.</summary>
        public static readonly Color NoBackground = new Color(0f, 0f, 0f, 0f);

        /// <summary>A rect with a flat background image behind it.</summary>
        public static RectTransform CreatePanel(string name, Transform parent, RectSpec spec, Color color)
        {
            RectTransform rect = CreateRect(name, parent, spec);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            RectSpec spec,
            string content,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent, spec);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = DefaultFont;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.text = content ?? string.Empty;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>A clickable panel with a centred main label and an optional subtitle.</summary>
        public static Button CreateButton(
            string name,
            Transform parent,
            RectSpec spec,
            string title,
            int fontSize,
            out Text titleLabel)
        {
            RectTransform rect = CreateRect(name, parent, spec);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = CardColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            titleLabel = CreateText("Title", rect, Stretch(), title, fontSize,
                TextAnchor.MiddleCenter, TextColor);
            return button;
        }

        /// <summary>Fills the parent completely.</summary>
        public static RectSpec Stretch()
        {
            return new RectSpec(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
        }

        /// <summary>Fills the parent with a uniform inset on every side.</summary>
        public static RectSpec Inset(float padding)
        {
            return new RectSpec(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(-padding * 2f, -padding * 2f));
        }

        /// <summary>A top-anchored row inside a card, measured down from the card's top edge.</summary>
        public static RectSpec Row(float top, float height, float sideInset = 10f)
        {
            return new RectSpec(
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(sideInset, -top),
                new Vector2(-sideInset * 2f, height));
        }

        public static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
    }
}

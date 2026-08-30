using UnityEngine;

namespace ParcelSort
{
    public static class DestinationPalette
    {
        public static readonly Color Red = new Color(0.86f, 0.22f, 0.16f, 1f);
        public static readonly Color Blue = new Color(0.18f, 0.42f, 0.92f, 1f);
        public static readonly Color Green = new Color(0.24f, 0.72f, 0.32f, 1f);
        public static readonly Color Yellow = new Color(0.95f, 0.78f, 0.16f, 1f);

        /// <summary>
        /// Worn by parcels whose destination is still hidden. Deliberately desaturated so it
        /// cannot be mistaken for any bay colour at a glance.
        /// </summary>
        public static readonly Color Blind = new Color(0.55f, 0.55f, 0.58f, 1f);

        public static Color For(DestinationColor color)
        {
            switch (color)
            {
                case DestinationColor.Blue:
                    return Blue;
                case DestinationColor.Green:
                    return Green;
                case DestinationColor.Yellow:
                    return Yellow;
                default:
                    return Red;
            }
        }

        public static bool TryParse(string text, out DestinationColor color)
        {
            color = DestinationColor.Red;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return System.Enum.TryParse(text.Trim(), true, out color);
        }

        public static void Apply(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            Material mat = renderer.material;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        public static void ApplyAll(Renderer[] renderers, Color color)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Apply(renderers[i], color);
            }
        }
    }
}

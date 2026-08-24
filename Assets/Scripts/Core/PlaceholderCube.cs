using UnityEngine;

namespace ParcelSort
{
    public static class PlaceholderCube
    {
        public static GameObject Create(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            Renderer materialSource = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            Renderer renderer = go.GetComponent<Renderer>();
            if (materialSource != null && materialSource.sharedMaterial != null)
            {
                renderer.sharedMaterial = materialSource.sharedMaterial;
            }

            DestinationPalette.Apply(renderer, color);
            return go;
        }
    }
}

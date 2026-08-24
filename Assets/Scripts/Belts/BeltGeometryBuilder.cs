using System.Collections.Generic;
using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// Turns a BeltPath polyline into prefab instances: one long deck slab per straight
    /// sub-segment, a corner piece at every bend, and support legs under elevated runs.
    /// </summary>
    public static class BeltGeometryBuilder
    {
        const float LegSpacing = 2f;
        const float LegGroundY = 0f;
        const float ElevatedThreshold = 0.9f;

        public static void Build(BeltPath belt, ModuleCatalog catalog)
        {
            if (belt == null || catalog == null)
            {
                return;
            }

            Vector3[] points = belt.Points;
            if (points.Length < 2)
            {
                return;
            }

            var tinted = new List<Renderer>();
            var scratch = new List<Renderer>();
            float deckDrop = BeltPath.ParcelHalf + BeltPath.DeckThickness * 0.5f;

            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
                Vector3 delta = b - a;
                float length = delta.magnitude;
                if (length < 0.001f)
                {
                    continue;
                }

                Vector3 dir = delta / length;
                GameObject deck = Object.Instantiate(catalog.beltDeckPrefab, belt.transform);
                deck.name = "Deck_" + i;
                deck.transform.position = (a + b) * 0.5f + Vector3.down * deckDrop;
                deck.transform.rotation = Quaternion.Euler(
                    GridMath.PitchDegrees(dir),
                    GridMath.YawDegrees(dir),
                    0f);
                // Width and thickness follow the visual scale; length stays exact so the
                // deck still spans the segment end to end.
                float s = BeltPath.VisualScale;
                deck.transform.localScale = new Vector3(s, s, length);
                deck.GetComponentsInChildren(true, scratch);
                tinted.AddRange(scratch);

                BuildLegs(belt, catalog, a, b, i);
            }

            if (catalog.beltCornerPrefab != null)
            {
                for (int i = 1; i < points.Length - 1; i++)
                {
                    Vector3 inDir = (points[i] - points[i - 1]).normalized;
                    Vector3 outDir = (points[i + 1] - points[i]).normalized;
                    Vector3 bisect = (inDir + outDir);
                    if (bisect.sqrMagnitude < 0.000001f)
                    {
                        bisect = outDir;
                    }

                    GameObject corner = Object.Instantiate(catalog.beltCornerPrefab, belt.transform);
                    corner.name = "Corner_" + i;
                    corner.transform.position = points[i] + Vector3.down * deckDrop;
                    corner.transform.rotation = Quaternion.Euler(0f, GridMath.YawDegrees(bisect), 0f);
                    corner.transform.localScale = Vector3.one * BeltPath.VisualScale;
                    corner.GetComponentsInChildren(true, scratch);
                    tinted.AddRange(scratch);
                }
            }

            belt.DeckRenderers = tinted.ToArray();
        }

        static void BuildLegs(BeltPath belt, ModuleCatalog catalog, Vector3 a, Vector3 b, int segmentIndex)
        {
            if (catalog.beltLegPrefab == null)
            {
                return;
            }

            float deckDrop = BeltPath.ParcelHalf + BeltPath.DeckThickness;
            float maxHeight = Mathf.Max(a.y, b.y) - deckDrop;
            if (maxHeight < ElevatedThreshold)
            {
                return;
            }

            float length = Vector3.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.RoundToInt(length / LegSpacing));
            for (int s = 0; s <= steps; s++)
            {
                float t = steps == 0 ? 0.5f : (float)s / steps;
                Vector3 point = Vector3.Lerp(a, b, t);
                float height = point.y - deckDrop - LegGroundY;
                if (height < ElevatedThreshold)
                {
                    continue;
                }

                // Endpoints are supported by the corner/node pillars, so skip them here.
                if (s == steps || (s == 0 && segmentIndex == 0))
                {
                    continue;
                }

                GameObject leg = Object.Instantiate(catalog.beltLegPrefab, belt.transform);
                leg.name = "Leg_" + segmentIndex + "_" + s;
                leg.transform.position = new Vector3(point.x, LegGroundY, point.z);
                leg.transform.rotation = Quaternion.identity;
                float thickness = BeltPath.VisualScale;
                leg.transform.localScale = new Vector3(thickness, height, thickness);
            }
        }
    }
}

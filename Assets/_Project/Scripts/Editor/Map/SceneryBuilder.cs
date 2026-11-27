using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PermisVR.Editor.Map
{
    /// Visual-only map content: underpasses (deck on side walls, with embankments), walls and
    /// fences, and trees. Merged per 400 m tile (larger than the road tiles) to keep draw calls low.
    internal static class SceneryBuilder
    {
        private const float TileSizeM = 400f;

        // Underpass cross-section, outward from the road edge.
        private const float WallGapM = 0.4f;          // between sidewalk and wall
        private const float WallThicknessM = 0.6f;
        private const float DeckOverhangM = 1f;
        private const float EmbankmentWidthM = 8f;
        private const float DeckBottomM = 4.6f;       // clearance under the deck
        private const float DeckThicknessM = 1.4f;

        private const float WallThicknessBarrierM = 0.25f;
        private const float FenceThicknessM = 0.06f;
        private const float HedgeThicknessM = 0.8f;

        private const float TrunkRadiusPerHeight = 0.02f;
        private const float MinTrunkRadiusM = 0.12f;
        private const int TrunkSides = 5;
        private const float CrownVerticalScale = 1.15f;

        /// How far beyond the road edge an underpass (walls, deck and embankments) covers the ground.
        public static float CoverBeyondRoadM(float sidewalkWidthM) =>
            sidewalkWidthM + WallGapM + WallThicknessM + DeckOverhangM + EmbankmentWidthM;

        public static int BuildUnderpasses(Transform root, MapUnderpass[] underpasses, float sidewalkWidthM)
        {
            var concrete = new MeshBuilder();
            var steel = new MeshBuilder();
            var walls = new MeshBuilder();
            var embankments = new MeshBuilder();
            var built = 0;
            var bottom = Vector3.up * DeckBottomM;
            var top = Vector3.up * (DeckBottomM + DeckThicknessM);

            foreach (var underpass in underpasses ?? new MapUnderpass[0])
            {
                if (underpass.points == null || underpass.points.Length < 2)
                {
                    continue;
                }

                var points = underpass.points.Select(p => p.ToVector3(0f)).ToList();
                var halfClear = underpass.roadWidthM * 0.5f + sidewalkWidthM + WallGapM;
                var halfWall = halfClear + WallThicknessM;
                var halfDeck = halfWall + DeckOverhangM;
                var faces = underpass.steel ? steel : concrete;

                var deckLeft = MapGeometry.Offset(points, -halfDeck);
                var deckRight = MapGeometry.Offset(points, halfDeck);
                var footLeft = MapGeometry.Offset(points, -(halfDeck + EmbankmentWidthM));
                var footRight = MapGeometry.Offset(points, halfDeck + EmbankmentWidthM);
                var innerLeft = MapGeometry.Offset(points, -halfClear);
                var innerRight = MapGeometry.Offset(points, halfClear);
                var outerLeft = MapGeometry.Offset(points, -halfWall);
                var outerRight = MapGeometry.Offset(points, halfWall);

                for (var i = 0; i < points.Count - 1; i++)
                {
                    var along = points[i + 1] - points[i];
                    if (along.sqrMagnitude < 1e-6f)
                    {
                        continue;
                    }

                    var right = MapGeometry.Right(along.normalized);

                    // Deck slab: underside, top, and its long side faces.
                    concrete.AddQuad(deckLeft[i] + bottom, deckLeft[i + 1] + bottom, deckRight[i + 1] + bottom, deckRight[i] + bottom, Vector3.down);
                    concrete.AddQuad(deckLeft[i] + top, deckLeft[i + 1] + top, deckRight[i + 1] + top, deckRight[i] + top, Vector3.up);
                    faces.AddQuad(deckLeft[i] + bottom, deckLeft[i + 1] + bottom, deckLeft[i + 1] + top, deckLeft[i] + top, -right);
                    faces.AddQuad(deckRight[i] + bottom, deckRight[i + 1] + bottom, deckRight[i + 1] + top, deckRight[i] + top, right);

                    // Side walls holding the deck: road-facing and back faces (both collide).
                    walls.AddQuad(innerLeft[i], innerLeft[i + 1], innerLeft[i + 1] + bottom, innerLeft[i] + bottom, right);
                    walls.AddQuad(outerLeft[i], outerLeft[i + 1], outerLeft[i + 1] + bottom, outerLeft[i] + bottom, -right);
                    walls.AddQuad(innerRight[i], innerRight[i + 1], innerRight[i + 1] + bottom, innerRight[i] + bottom, -right);
                    walls.AddQuad(outerRight[i], outerRight[i + 1], outerRight[i + 1] + bottom, outerRight[i] + bottom, right);

                    // Earth embankments sloping from the deck edge down to the ground on both sides.
                    var slopeUp = Vector3.up * EmbankmentWidthM;
                    var slopeOut = right * (DeckBottomM + DeckThicknessM);
                    embankments.AddQuad(deckLeft[i] + top, deckLeft[i + 1] + top, footLeft[i + 1], footLeft[i], slopeUp - slopeOut);
                    embankments.AddQuad(deckRight[i] + top, deckRight[i + 1] + top, footRight[i + 1], footRight[i], slopeUp + slopeOut);
                }

                AddUnderpassEnd(points, 0, -1f, faces, walls, embankments, deckLeft, deckRight, innerLeft, innerRight,
                    outerLeft, outerRight, footLeft, footRight, bottom, top);
                AddUnderpassEnd(points, points.Count - 1, 1f, faces, walls, embankments, deckLeft, deckRight, innerLeft,
                    innerRight, outerLeft, outerRight, footLeft, footRight, bottom, top);
                built++;
            }

            var layer = new GameObject("Underpasses").transform;
            layer.SetParent(root, false);
            Emit(layer, "Underpass_Deck", concrete, MapMaterialLibrary.Get("BridgeConcrete", new Color(0.58f, 0.58f, 0.56f)), false);
            Emit(layer, "Underpass_SteelGirders", steel, MapMaterialLibrary.Get("BridgeSteel", new Color(0.86f, 0.42f, 0.12f)), false);
            Emit(layer, "Underpass_Walls", walls, MapMaterialLibrary.Get("BridgeConcrete", new Color(0.58f, 0.58f, 0.56f)), true);
            Emit(layer, "Underpass_Embankments", embankments, MapMaterialLibrary.Get("Embankment", new Color(0.3f, 0.42f, 0.22f)), false);
            return built;
        }

        /// Entrance face across the deck, wall end caps and the embankment end triangles.
        private static void AddUnderpassEnd(List<Vector3> points, int index, float outwardSign, MeshBuilder faces,
            MeshBuilder walls, MeshBuilder embankments, Vector3[] deckLeft, Vector3[] deckRight, Vector3[] innerLeft,
            Vector3[] innerRight, Vector3[] outerLeft, Vector3[] outerRight, Vector3[] footLeft, Vector3[] footRight,
            Vector3 bottom, Vector3 top)
        {
            var neighbour = index == 0 ? points[1] : points[index - 1];
            var outward = (points[index] - neighbour).normalized;
            outward.y = 0f;

            faces.AddQuad(deckLeft[index] + bottom, deckRight[index] + bottom, deckRight[index] + top, deckLeft[index] + top, outward);
            walls.AddQuad(innerLeft[index], outerLeft[index], outerLeft[index] + bottom, innerLeft[index] + bottom, outward);
            walls.AddQuad(innerRight[index], outerRight[index], outerRight[index] + bottom, innerRight[index] + bottom, outward);
            embankments.AddTriangle(deckLeft[index] + top, footLeft[index], deckLeft[index], outward);
            embankments.AddTriangle(deckRight[index] + top, footRight[index], deckRight[index], outward);
        }

        public static int BuildBarriers(Transform root, MapBarrier[] barriers)
        {
            var wallTiles = new Dictionary<Vector2Int, MeshBuilder>();
            var fenceTiles = new Dictionary<Vector2Int, MeshBuilder>();
            var hedgeTiles = new Dictionary<Vector2Int, MeshBuilder>();
            var built = 0;

            foreach (var barrier in barriers ?? new MapBarrier[0])
            {
                if (barrier.points == null || barrier.points.Length < 2 || barrier.heightM <= 0f)
                {
                    continue;
                }

                var (tiles, thickness) = barrier.type switch
                {
                    "fence" => (fenceTiles, FenceThicknessM),
                    "hedge" => (hedgeTiles, HedgeThicknessM),
                    _ => (wallTiles, WallThicknessBarrierM),
                };
                var points = barrier.points.Select(p => p.ToVector3(0f)).ToList();
                AddBox(TileFor(tiles, points[0]), points, thickness * 0.5f, barrier.heightM);
                built++;
            }

            var layer = new GameObject("WallsAndFences").transform;
            layer.SetParent(root, false);
            EmitTiles(layer, "Walls", wallTiles, MapMaterialLibrary.Get("Wall", new Color(0.8f, 0.76f, 0.68f)), true);
            EmitTiles(layer, "Fences", fenceTiles, MapMaterialLibrary.Get("Fence", new Color(0.2f, 0.27f, 0.22f)), true);
            EmitTiles(layer, "Hedges", hedgeTiles, MapMaterialLibrary.Get("Hedge", new Color(0.2f, 0.35f, 0.15f)), true);
            return built;
        }

        /// A thin box swept along a polyline: both sides, top and end caps.
        private static void AddBox(MeshBuilder builder, List<Vector3> points, float halfThicknessM, float heightM)
        {
            var left = MapGeometry.Offset(points, -halfThicknessM);
            var right = MapGeometry.Offset(points, halfThicknessM);
            var up = Vector3.up * heightM;
            for (var i = 0; i < points.Count - 1; i++)
            {
                var along = points[i + 1] - points[i];
                if (along.sqrMagnitude < 1e-6f)
                {
                    continue;
                }

                var side = MapGeometry.Right(along.normalized);
                builder.AddQuad(left[i], left[i + 1], left[i + 1] + up, left[i] + up, -side);
                builder.AddQuad(right[i], right[i + 1], right[i + 1] + up, right[i] + up, side);
                builder.AddQuad(left[i] + up, left[i + 1] + up, right[i + 1] + up, right[i] + up, Vector3.up);
            }

            var last = points.Count - 1;
            builder.AddQuad(left[0], right[0], right[0] + up, left[0] + up, points[0] - points[1]);
            builder.AddQuad(left[last], right[last], right[last] + up, left[last] + up, points[last] - points[last - 1]);
        }

        public static int BuildTrees(Transform root, MapTree[] trees)
        {
            var trunkTiles = new Dictionary<Vector2Int, MeshBuilder>();
            var crownTiles = new Dictionary<Vector2Int, MeshBuilder>();
            var built = 0;

            foreach (var tree in trees ?? new MapTree[0])
            {
                if (tree.position == null || tree.heightM <= 0f || tree.crownRadiusM <= 0f)
                {
                    continue;
                }

                var basePoint = tree.position.ToVector3(0f);
                // Deterministic per-position yaw so regenerating gives the same forest.
                var yaw = Quaternion.Euler(0f, Mathf.Repeat(basePoint.x * 37.3f + basePoint.z * 91.7f, 360f), 0f);
                AddTrunk(TileFor(trunkTiles, basePoint), basePoint, tree.heightM, tree.crownRadiusM);
                AddCrown(TileFor(crownTiles, basePoint), basePoint + Vector3.up * (tree.heightM - tree.crownRadiusM * CrownVerticalScale),
                    tree.crownRadiusM, yaw);
                built++;
            }

            var layer = new GameObject("Trees").transform;
            layer.SetParent(root, false);
            EmitTiles(layer, "TreeTrunks", trunkTiles, MapMaterialLibrary.Get("TreeTrunk", new Color(0.33f, 0.25f, 0.18f)), false);
            EmitTiles(layer, "TreeCrowns", crownTiles, MapMaterialLibrary.Get("TreeFoliage", new Color(0.22f, 0.38f, 0.16f)), false);
            return built;
        }

        private static void AddTrunk(MeshBuilder builder, Vector3 basePoint, float heightM, float crownRadiusM)
        {
            var radius = Mathf.Max(MinTrunkRadiusM, heightM * TrunkRadiusPerHeight);
            var up = Vector3.up * (heightM - crownRadiusM * CrownVerticalScale);
            for (var k = 0; k < TrunkSides; k++)
            {
                var a0 = k * Mathf.PI * 2f / TrunkSides;
                var a1 = (k + 1) * Mathf.PI * 2f / TrunkSides;
                var p0 = basePoint + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var p1 = basePoint + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                var outward = (p0 + p1) * 0.5f - basePoint;
                builder.AddQuad(p0, p1, p1 + up, p0 + up, outward);
            }
        }

        // Unit icosahedron: 12 vertices, 20 faces. Flat-shaded, it reads as a low-poly crown.
        private static readonly Vector3[] IcoVertices = BuildIcoVertices();

        private static readonly int[] IcoFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        private static Vector3[] BuildIcoVertices()
        {
            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var raw = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            return raw.Select(v => v.normalized).ToArray();
        }

        private static void AddCrown(MeshBuilder builder, Vector3 center, float radiusM, Quaternion yaw)
        {
            var scale = new Vector3(radiusM, radiusM * CrownVerticalScale, radiusM);
            for (var f = 0; f < IcoFaces.Length; f += 3)
            {
                var a = center + yaw * Vector3.Scale(IcoVertices[IcoFaces[f]], scale);
                var b = center + yaw * Vector3.Scale(IcoVertices[IcoFaces[f + 1]], scale);
                var c = center + yaw * Vector3.Scale(IcoVertices[IcoFaces[f + 2]], scale);
                builder.AddTriangle(a, b, c, (a + b + c) / 3f - center);
            }
        }

        private static MeshBuilder TileFor(Dictionary<Vector2Int, MeshBuilder> tiles, Vector3 position)
        {
            var key = new Vector2Int(Mathf.FloorToInt(position.x / TileSizeM), Mathf.FloorToInt(position.z / TileSizeM));
            if (!tiles.TryGetValue(key, out var builder))
            {
                builder = new MeshBuilder();
                tiles[key] = builder;
            }

            return builder;
        }

        private static void Emit(Transform parent, string name, MeshBuilder builder, Material material, bool withCollider)
        {
            EmitTiles(parent, name, new Dictionary<Vector2Int, MeshBuilder> { [Vector2Int.zero] = builder }, material, withCollider);
        }

        private static void EmitTiles(Transform parent, string name, Dictionary<Vector2Int, MeshBuilder> tiles,
            Material material, bool withCollider)
        {
            foreach (var tile in tiles)
            {
                if (tile.Value.IsEmpty)
                {
                    continue;
                }

                var go = new GameObject($"{name}_{tile.Key.x}_{tile.Key.y}");
                go.transform.SetParent(parent, false);
                var mesh = GeneratedMeshStore.Save(tile.Value.Build(go.name));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                if (withCollider)
                {
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                }

                UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, UnityEditor.StaticEditorFlags.BatchingStatic);
            }
        }
    }
}

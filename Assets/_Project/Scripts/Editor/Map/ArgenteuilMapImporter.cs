using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PermisVR.Editor.Map
{
    /// Builds the Argenteuil map (ground, roads, sidewalks, markings, buildings, signs) under a
    /// Map_Generated root in the M2 scene, from argenteuil_map.json plus the overrides file.
    /// Everything under the root is regenerated on each import; never hand-edit it.
    public static class ArgenteuilMapImporter
    {
        private const string MapJsonPath = "Assets/_Project/Data/Maps/argenteuil_map.json";
        private const string OverridesPath = "Assets/_Project/Data/Maps/argenteuil_overrides.json";
        private const string M1ScenePath = "Assets/_Project/Scenes/M1_TestPlane.unity";
        private const string M2ScenePath = "Assets/_Project/Scenes/M2_Argenteuil.unity";
        private const string RootName = "Map_Generated";

        private const float TileSizeM = 200f;
        private const float GroundSizeM = 2200f;
        private const float BarrierHeightM = 2f;
        private const float RoadSurfaceY = 0.02f;
        private const float MarkingY = 0.03f;
        private const float CurbHeightM = 0.15f;
        private const float SidewalkWidthM = 2f;
        private const float JunctionTrimM = 7f;
        private const float MinSidewalkLengthM = 2f;
        private const float LaneWidthM = 3.25f;
        private const float DashLengthM = 3f;
        private const float DashGapM = 5f;
        private const float LineHalfWidthM = 0.06f;
        private const float ZebraPitchM = 1f;
        private const float ZebraBarHalfWidthM = 0.25f;
        private const float ZebraHalfLengthM = 2f;
        private const float StopLineHalfDepthM = 0.25f;
        private const float SignEdgeOffsetM = 0.8f;
        private const float MaxSignSnapM = 20f;
        private const float JunctionSearchM = 30f;
        private const float AtJunctionM = 3f;
        private const float TrafficLightSetbackM = 6f;
        private const float MinBuildingAreaM2 = 4f;
        private const float DefaultBuildingHeightM = 12f;
        private const float FogStartM = 250f;
        private const float FogEndM = 550f;
        private const float FarClipM = 600f;

        private static readonly HashSet<string> NoSidewalkHighways = new()
        {
            "motorway", "motorway_link", "trunk", "trunk_link", "primary_link", "secondary_link",
        };

        [MenuItem("PermisVR/Map/Import Argenteuil")]
        public static void Import()
        {
            if (!File.Exists(MapJsonPath))
            {
                Debug.LogError($"ArgenteuilMapImporter: {MapJsonPath} not found. Run tools/osm/fetch_area.py " +
                               "then tools/osm/build_map.py first.");
                return;
            }

            var data = JsonUtility.FromJson<MapData>(File.ReadAllText(MapJsonPath));
            ApplyOverrides(data, LoadOverrides());

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = OpenOrCreateScene();
            if (!scene.IsValid())
            {
                return;
            }

            RemoveRootObjects(scene, RootName, "Ground", "Markers", "Boundary");
            var root = new GameObject(RootName).transform;

            var roadTiles = new Dictionary<Vector2Int, MeshBuilder>();
            var sidewalkTiles = new Dictionary<Vector2Int, MeshBuilder>();
            var markingTiles = new Dictionary<Vector2Int, MeshBuilder>();
            var buildingTiles = new Dictionary<Vector2Int, MeshBuilder>();

            BuildGround(root);
            BuildRoads(data, roadTiles, sidewalkTiles, markingTiles);
            BuildBuildings(data, buildingTiles);
            var signCount = BuildSigns(root, data, markingTiles);

            EmitTiles(root, "Roads", roadTiles, MapMaterialLibrary.Get("Road", new Color(0.2f, 0.2f, 0.22f)), false);
            EmitTiles(root, "Sidewalks", sidewalkTiles, MapMaterialLibrary.Get("Sidewalk", new Color(0.62f, 0.62f, 0.6f)), true);
            EmitTiles(root, "Markings", markingTiles, MapMaterialLibrary.Get("Marking", new Color(0.95f, 0.95f, 0.92f)), false);
            EmitTiles(root, "Buildings", buildingTiles, MapMaterialLibrary.Get("Building", new Color(0.83f, 0.77f, 0.66f)), true);

            PlaceCarAtStart(data);
            ConfigureView();
            AddAttribution();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            RegisterBuildScenes();

            Debug.Log($"ArgenteuilMapImporter: {data.roads.Length} roads, {data.buildings.Length} buildings, " +
                      $"{signCount} signs/markings imported into {M2ScenePath}. Sign directions are heuristic: " +
                      "verify each junction against street-level imagery before using it on a route.");
        }

        private static MapOverrides LoadOverrides()
        {
            return File.Exists(OverridesPath)
                ? JsonUtility.FromJson<MapOverrides>(File.ReadAllText(OverridesPath)) ?? new MapOverrides()
                : new MapOverrides();
        }

        private static void ApplyOverrides(MapData data, MapOverrides overrides)
        {
            if (overrides.removeSignOsmIds is { Length: > 0 })
            {
                var removed = new HashSet<long>(overrides.removeSignOsmIds);
                data.signs = data.signs.Where(s => !removed.Contains(s.osmId)).ToArray();
            }

            if (overrides.addSigns is { Length: > 0 })
            {
                data.signs = data.signs.Concat(overrides.addSigns).ToArray();
            }

            foreach (var fix in overrides.roadOverrides ?? new RoadOverride[0])
            {
                foreach (var road in data.roads.Where(r => r.osmId == fix.osmId))
                {
                    if (fix.maxspeedKmh > 0)
                    {
                        road.maxspeedKmh = fix.maxspeedKmh;
                    }

                    if (fix.lanes > 0)
                    {
                        road.lanes = fix.lanes;
                        road.widthM = fix.lanes * LaneWidthM;
                    }
                }
            }
        }

        private static Scene OpenOrCreateScene()
        {
            if (!File.Exists(M2ScenePath) && !AssetDatabase.CopyAsset(M1ScenePath, M2ScenePath))
            {
                Debug.LogError($"ArgenteuilMapImporter: couldn't create {M2ScenePath} from {M1ScenePath}.");
                return default;
            }

            return EditorSceneManager.OpenScene(M2ScenePath, OpenSceneMode.Single);
        }

        private static void RemoveRootObjects(Scene scene, params string[] names)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (names.Contains(root.name))
                {
                    Object.DestroyImmediate(root);
                }
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

        private static void EmitTiles(Transform root, string layerName, Dictionary<Vector2Int, MeshBuilder> tiles,
            Material material, bool withCollider)
        {
            var layer = new GameObject(layerName).transform;
            layer.SetParent(root, false);
            foreach (var tile in tiles)
            {
                if (tile.Value.IsEmpty)
                {
                    continue;
                }

                var go = new GameObject($"{layerName}_{tile.Key.x}_{tile.Key.y}");
                go.transform.SetParent(layer, false);
                var mesh = tile.Value.Build(go.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                if (withCollider)
                {
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                }

                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        private static void BuildGround(Transform root)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            // Unity's Plane primitive is 10 m wide at scale 1.
            ground.transform.localScale = new Vector3(GroundSizeM / 10f, 1f, GroundSizeM / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = MapMaterialLibrary.Get("Ground", new Color(0.36f, 0.44f, 0.3f));
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);

            var barrier = MapMaterialLibrary.Get("Barrier", new Color(0.85f, 0.1f, 0.1f));
            var edge = GroundSizeM / 2f;
            var y = BarrierHeightM / 2f;
            CreateBarrier(root, barrier, new Vector3(0f, y, edge), new Vector3(GroundSizeM, BarrierHeightM, 1f));
            CreateBarrier(root, barrier, new Vector3(0f, y, -edge), new Vector3(GroundSizeM, BarrierHeightM, 1f));
            CreateBarrier(root, barrier, new Vector3(edge, y, 0f), new Vector3(1f, BarrierHeightM, GroundSizeM));
            CreateBarrier(root, barrier, new Vector3(-edge, y, 0f), new Vector3(1f, BarrierHeightM, GroundSizeM));
        }

        private static void CreateBarrier(Transform root, Material material, Vector3 position, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Barrier";
            wall.transform.SetParent(root, false);
            wall.transform.localPosition = position;
            wall.transform.localScale = size;
            wall.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void BuildRoads(MapData data, Dictionary<Vector2Int, MeshBuilder> roadTiles,
            Dictionary<Vector2Int, MeshBuilder> sidewalkTiles, Dictionary<Vector2Int, MeshBuilder> markingTiles)
        {
            var junctions = new HashSet<Vector2Int>(data.intersections.Select(j => PointKey(j.position)));

            foreach (var road in data.roads)
            {
                if (road.points == null || road.points.Length < 2)
                {
                    continue;
                }

                var points = road.points.Select(p => p.ToVector3(RoadSurfaceY)).ToList();
                var halfWidth = road.widthM * 0.5f;
                var left = MapGeometry.Offset(points, -halfWidth);
                var right = MapGeometry.Offset(points, halfWidth);
                for (var i = 0; i < points.Count - 1; i++)
                {
                    TileFor(roadTiles, (points[i] + points[i + 1]) * 0.5f)
                        .AddQuad(left[i], left[i + 1], right[i + 1], right[i], Vector3.up);
                }

                if (!road.oneWay && road.lanes >= 2)
                {
                    AddCenterDashes(markingTiles, points);
                }

                if (NoSidewalkHighways.Contains(road.highway))
                {
                    continue;
                }

                // Raised sidewalks are cut back at junctions so they don't run across the crossing street.
                var trimStart = junctions.Contains(PointKey(road.points[0])) ? JunctionTrimM : 0f;
                var trimEnd = junctions.Contains(PointKey(road.points[road.points.Length - 1])) ? JunctionTrimM : 0f;
                var trimmed = MapGeometry.Trim(points, trimStart, trimEnd, MinSidewalkLengthM);
                if (trimmed != null)
                {
                    AddSidewalk(sidewalkTiles, trimmed, halfWidth, 1f);
                    AddSidewalk(sidewalkTiles, trimmed, halfWidth, -1f);
                }
            }
        }

        private static Vector2Int PointKey(MapPoint p) =>
            new(Mathf.RoundToInt(p.x * 100f), Mathf.RoundToInt(p.z * 100f));

        private static void AddSidewalk(Dictionary<Vector2Int, MeshBuilder> tiles, List<Vector3> points, float halfWidth, float side)
        {
            var inner = MapGeometry.Offset(points, side * halfWidth);
            var outer = MapGeometry.Offset(points, side * (halfWidth + SidewalkWidthM));
            var raise = new Vector3(0f, CurbHeightM - RoadSurfaceY, 0f);

            for (var i = 0; i < points.Count - 1; i++)
            {
                var direction = points[i + 1] - points[i];
                direction.y = 0f;
                if (direction.sqrMagnitude < 1e-6f)
                {
                    continue;
                }

                var builder = TileFor(tiles, (points[i] + points[i + 1]) * 0.5f);
                builder.AddQuad(inner[i] + raise, inner[i + 1] + raise, outer[i + 1] + raise, outer[i] + raise, Vector3.up);

                var towardRoad = MapGeometry.Right(direction.normalized) * -side;
                builder.AddQuad(inner[i], inner[i + 1], inner[i + 1] + raise, inner[i] + raise, towardRoad);
            }
        }

        private static void AddCenterDashes(Dictionary<Vector2Int, MeshBuilder> tiles, List<Vector3> points)
        {
            var length = MapGeometry.Length(points);
            for (var distance = DashGapM * 0.5f; distance + DashLengthM <= length; distance += DashLengthM + DashGapM)
            {
                var start = MapGeometry.PointAt(points, distance);
                var end = MapGeometry.PointAt(points, distance + DashLengthM);
                var along = end - start;
                along.y = 0f;
                if (along.sqrMagnitude < 1e-4f)
                {
                    continue;
                }

                var direction = along.normalized;
                TileFor(tiles, start).AddRect((start + end) * 0.5f, direction, MapGeometry.Right(direction),
                    along.magnitude * 0.5f, LineHalfWidthM, MarkingY);
            }
        }

        private static void BuildBuildings(MapData data, Dictionary<Vector2Int, MeshBuilder> tiles)
        {
            foreach (var building in data.buildings)
            {
                if (building.footprint == null || building.footprint.Length < 3)
                {
                    continue;
                }

                var ring = building.footprint.Select(p => new Vector2(p.x, p.z)).ToList();
                var area = MapGeometry.SignedArea(ring);
                if (Mathf.Abs(area) < MinBuildingAreaM2)
                {
                    continue;
                }

                var height = building.heightM > 0f ? building.heightM : DefaultBuildingHeightM;
                var centroid = ring.Aggregate(Vector2.zero, (sum, p) => sum + p) / ring.Count;
                var builder = TileFor(tiles, new Vector3(centroid.x, 0f, centroid.y));
                var counterclockwise = area > 0f;

                for (var i = 0; i < ring.Count; i++)
                {
                    var p = ring[i];
                    var q = ring[(i + 1) % ring.Count];
                    var edge = q - p;
                    if (edge.sqrMagnitude < 1e-4f)
                    {
                        continue;
                    }

                    var outward = counterclockwise ? new Vector3(edge.y, 0f, -edge.x) : new Vector3(-edge.y, 0f, edge.x);
                    builder.AddQuad(new Vector3(p.x, 0f, p.y), new Vector3(p.x, height, p.y),
                        new Vector3(q.x, height, q.y), new Vector3(q.x, 0f, q.y), outward);
                }

                var roof = MapGeometry.Triangulate(ring);
                for (var t = 0; t < roof.Count; t += 3)
                {
                    builder.AddTriangle(RoofPoint(ring[roof[t]], height), RoofPoint(ring[roof[t + 1]], height),
                        RoofPoint(ring[roof[t + 2]], height), Vector3.up);
                }
            }
        }

        private static Vector3 RoofPoint(Vector2 p, float height) => new(p.x, height, p.y);

        private static int BuildSigns(Transform root, MapData data, Dictionary<Vector2Int, MeshBuilder> markingTiles)
        {
            var signsRoot = new GameObject("Signs").transform;
            signsRoot.SetParent(root, false);
            var junctions = data.intersections.Select(j => j.position.ToVector3(0f)).ToList();
            var placed = 0;

            foreach (var sign in data.signs)
            {
                var position = sign.position.ToVector3(0f);
                if (!TryNearestRoad(data.roads, position, out var road, out var onRoad, out var roadDirection, out var distance)
                    || distance > MaxSignSnapM)
                {
                    continue;
                }

                var travel = TravelDirection(position, roadDirection, junctions, sign.direction);
                var halfWidth = road.widthM * 0.5f;
                var right = MapGeometry.Right(travel);
                var markings = TileFor(markingTiles, onRoad);

                switch (sign.type)
                {
                    case "crossing":
                        AddZebra(markings, onRoad, roadDirection, halfWidth);
                        break;
                    case "stop":
                        AddStopLine(markings, onRoad, travel, road);
                        CreateStopSign(signsRoot, sign, onRoad + right * (halfWidth + SignEdgeOffsetM), travel);
                        break;
                    case "give_way":
                        AddGiveWayLine(markings, onRoad, travel, road);
                        CreateGiveWaySign(signsRoot, sign, onRoad + right * (halfWidth + SignEdgeOffsetM), travel);
                        break;
                    case "traffic_signals":
                        // OSM often puts the signal node in the junction itself; set the line back from it.
                        var stopAt = onRoad - travel * TrafficLightSetbackM;
                        AddStopLine(markings, stopAt, travel, road);
                        CreateTrafficLight(signsRoot, sign, stopAt + right * (halfWidth + SignEdgeOffsetM), travel);
                        break;
                    default:
                        continue;
                }

                placed++;
            }

            return placed;
        }

        private static bool TryNearestRoad(MapRoad[] roads, Vector3 position, out MapRoad nearest, out Vector3 onRoad,
            out Vector3 direction, out float distance)
        {
            nearest = null;
            onRoad = position;
            direction = Vector3.forward;
            distance = float.MaxValue;

            foreach (var road in roads)
            {
                if (road.points == null)
                {
                    continue;
                }

                for (var i = 0; i < road.points.Length - 1; i++)
                {
                    var a = road.points[i].ToVector3(0f);
                    var ab = road.points[i + 1].ToVector3(0f) - a;
                    var lengthSq = ab.sqrMagnitude;
                    if (lengthSq < 1e-6f)
                    {
                        continue;
                    }

                    var t = Mathf.Clamp01(Vector3.Dot(position - a, ab) / lengthSq);
                    var candidate = a + ab * t;
                    var d = Vector3.Distance(candidate, position);
                    if (d < distance)
                    {
                        distance = d;
                        nearest = road;
                        onRoad = candidate;
                        direction = ab.normalized;
                    }
                }
            }

            return nearest != null;
        }

        /// Signs face traffic heading toward the nearest junction; OSM direction tags are the
        /// fallback because they're relative to way order, which the graph doesn't preserve.
        private static Vector3 TravelDirection(Vector3 position, Vector3 roadDirection, List<Vector3> junctions, string osmDirection)
        {
            var nearest = Vector3.zero;
            var nearestDistance = float.MaxValue;
            foreach (var junction in junctions)
            {
                var d = Vector3.Distance(junction, position);
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearest = junction;
                }
            }

            if (nearestDistance > AtJunctionM && nearestDistance < JunctionSearchM)
            {
                return Vector3.Dot(nearest - position, roadDirection) >= 0f ? roadDirection : -roadDirection;
            }

            return osmDirection == "backward" ? -roadDirection : roadDirection;
        }

        private static void AddZebra(MeshBuilder builder, Vector3 at, Vector3 roadDirection, float halfWidth)
        {
            var across = MapGeometry.Right(roadDirection);
            for (var offset = -halfWidth + ZebraBarHalfWidthM * 2f; offset <= halfWidth - ZebraBarHalfWidthM * 2f; offset += ZebraPitchM)
            {
                builder.AddRect(at + across * offset, roadDirection, across, ZebraHalfLengthM, ZebraBarHalfWidthM, MarkingY);
            }
        }

        private static void AddStopLine(MeshBuilder builder, Vector3 at, Vector3 travel, MapRoad road)
        {
            var halfWidth = road.widthM * 0.5f;
            var right = MapGeometry.Right(travel);
            var from = road.oneWay ? -halfWidth : 0f;
            builder.AddRect(at + right * ((from + halfWidth) * 0.5f), travel, right, StopLineHalfDepthM,
                (halfWidth - from) * 0.5f, MarkingY);
        }

        private static void AddGiveWayLine(MeshBuilder builder, Vector3 at, Vector3 travel, MapRoad road)
        {
            var halfWidth = road.widthM * 0.5f;
            var right = MapGeometry.Right(travel);
            var from = road.oneWay ? -halfWidth : 0f;
            for (var offset = from + 0.3f; offset < halfWidth; offset += 0.9f)
            {
                builder.AddRect(at + right * offset, travel, right, 0.2f, 0.3f, MarkingY);
            }
        }

        /// Sign root's +Z is the travel direction, so the face points back at approaching drivers (-Z).
        private static Transform CreateSignRoot(Transform parent, string name, Vector3 position, Vector3 travel)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(travel, Vector3.up));
            return go.transform;
        }

        private static void CreatePart(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale,
            Quaternion rotation, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateLabel(Transform parent, string text, Vector3 position, Color color, float characterSize)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static Material Post => MapMaterialLibrary.Get("SignPost", new Color(0.55f, 0.55f, 0.55f));
        private static Material SignRed => MapMaterialLibrary.Get("SignRed", new Color(0.8f, 0.05f, 0.05f));
        private static Material SignWhite => MapMaterialLibrary.Get("SignWhite", Color.white);

        private static readonly Quaternion FacingDriver = Quaternion.Euler(90f, 0f, 0f);

        private static void CreateStopSign(Transform parent, MapSign sign, Vector3 position, Vector3 travel)
        {
            var root = CreateSignRoot(parent, $"Stop_{sign.osmId}", position, travel);
            CreatePart(PrimitiveType.Cylinder, root, new Vector3(0f, 1.1f, 0f), new Vector3(0.08f, 1.1f, 0.08f), Quaternion.identity, Post);
            CreatePart(PrimitiveType.Cylinder, root, new Vector3(0f, 2.4f, 0f), new Vector3(0.75f, 0.02f, 0.75f), FacingDriver, SignRed);
            CreateLabel(root, "STOP", new Vector3(0f, 2.4f, -0.03f), Color.white, 0.02f);
        }

        private static void CreateGiveWaySign(Transform parent, MapSign sign, Vector3 position, Vector3 travel)
        {
            var root = CreateSignRoot(parent, $"GiveWay_{sign.osmId}", position, travel);
            CreatePart(PrimitiveType.Cylinder, root, new Vector3(0f, 1.1f, 0f), new Vector3(0.08f, 1.1f, 0.08f), Quaternion.identity, Post);
            CreatePart(PrimitiveType.Cylinder, root, new Vector3(0f, 2.4f, 0f), new Vector3(0.8f, 0.02f, 0.8f), FacingDriver, SignRed);
            CreatePart(PrimitiveType.Cylinder, root, new Vector3(0f, 2.4f, -0.025f), new Vector3(0.6f, 0.01f, 0.6f), FacingDriver, SignWhite);
            CreateLabel(root, "CÉDEZ LE\nPASSAGE", new Vector3(0f, 2.4f, -0.04f), new Color(0.8f, 0.05f, 0.05f), 0.008f);
        }

        private static void CreateTrafficLight(Transform parent, MapSign sign, Vector3 position, Vector3 travel)
        {
            var root = CreateSignRoot(parent, $"TrafficLight_{sign.osmId}", position, travel);
            CreatePart(PrimitiveType.Cylinder, root, new Vector3(0f, 1.5f, 0f), new Vector3(0.1f, 1.5f, 0.1f), Quaternion.identity, Post);
            CreatePart(PrimitiveType.Cube, root, new Vector3(0f, 3.4f, 0f), new Vector3(0.35f, 0.95f, 0.25f), Quaternion.identity,
                MapMaterialLibrary.Get("LightHead", new Color(0.08f, 0.08f, 0.08f)));
            CreatePart(PrimitiveType.Sphere, root, new Vector3(0f, 3.7f, -0.13f), Vector3.one * 0.22f, Quaternion.identity,
                MapMaterialLibrary.Get("LampRed", new Color(1f, 0.1f, 0.1f)));
            CreatePart(PrimitiveType.Sphere, root, new Vector3(0f, 3.4f, -0.13f), Vector3.one * 0.22f, Quaternion.identity,
                MapMaterialLibrary.Get("LampAmber", new Color(1f, 0.65f, 0f)));
            CreatePart(PrimitiveType.Sphere, root, new Vector3(0f, 3.1f, -0.13f), Vector3.one * 0.22f, Quaternion.identity,
                MapMaterialLibrary.Get("LampGreen", new Color(0.1f, 0.9f, 0.2f)));
        }

        /// The start address is the world origin; the car goes in the right-hand lane there (France drives on the right).
        private static void PlaceCarAtStart(MapData data)
        {
            var car = GameObject.Find("Car");
            if (car == null)
            {
                Debug.LogWarning("ArgenteuilMapImporter: no 'Car' in the scene, so it wasn't placed at the start point.");
                return;
            }

            if (!TryNearestRoad(data.roads, Vector3.zero, out var road, out var onRoad, out var direction, out _))
            {
                return;
            }

            var lateralM = road.oneWay ? 0f : road.widthM * 0.25f;
            var position = onRoad + MapGeometry.Right(direction) * lateralM;
            position.y = car.transform.position.y;
            car.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
        }

        private static void ConfigureView()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = FogStartM;
            RenderSettings.fogEndDistance = FogEndM;
            RenderSettings.fogColor = new Color(0.72f, 0.76f, 0.8f);

            var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (camera != null)
            {
                camera.farClipPlane = FarClipM;
            }
        }

        /// ODbL requires visible attribution; a credits screen replaces this later.
        private static void AddAttribution()
        {
            var cockpit = GameObject.Find("Car")?.transform.Find("Cockpit");
            if (cockpit == null || cockpit.Find("OsmAttribution") != null)
            {
                return;
            }

            var go = new GameObject("OsmAttribution");
            go.transform.SetParent(cockpit, false);
            go.transform.localPosition = new Vector3(0f, 0.09f, 0.6f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.text = "© OpenStreetMap contributors";
            text.fontSize = 100;
            text.characterSize = 0.0015f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(0.8f, 0.8f, 0.8f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static void RegisterBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(M2ScenePath, true),
                new EditorBuildSettingsScene(M1ScenePath, false),
            };
        }
    }
}

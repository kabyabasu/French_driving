#!/usr/bin/env python3
"""Parse cached OSM data (from fetch_area.py) and export argenteuil_map.json for Unity.

Usage:
    python build_map.py

Projects everything to Lambert-93 (EPSG:2154), then re-origins so the exam
start point (tools/osm/cache/origin.json) sits at (0, 0): +X = east, +Z = north,
1 Unity unit = 1 metre. Output goes to
Assets/_Project/Data/Maps/argenteuil_map.json, which the Unity importer
(PermisVR > Map > Import Argenteuil) consumes and fully regenerates
Map_Generated from on each run.

The JSON avoids nulls and nested arrays because Unity's JsonUtility can't read
them: unknown numbers are 0, unknown strings are "".
"""
import json
import math
import random
import re
from pathlib import Path

import geopandas as gpd
import osmnx as ox
from pyproj import Transformer
from shapely import affinity
from shapely.geometry import LineString, MultiLineString, Point, Polygon
from shapely.ops import substring, unary_union
from shapely.strtree import STRtree

CACHE_DIR = Path(__file__).parent / "cache"
OUTPUT_PATH = Path(__file__).parent.parent.parent / "Assets/_Project/Data/Maps/argenteuil_map.json"

LANE_WIDTH_M = 3.25
LEVEL_HEIGHT_M = 3.0
DEFAULT_LEVELS = 4
COORD_DECIMALS = 2

SIGN_RULE_BY_HIGHWAY_TAG = {
    "stop": "stop_sign",
    "give_way": "yield",
    "traffic_signals": "red_light",
    "crossing": "pedestrian_crossing",
}

# French implicit speed limits (maxspeed=FR:*).
IMPLICIT_MAXSPEED_KMH = {
    "fr:urban": 50,
    "fr:zone30": 30,
    "fr:rural": 80,
    "fr:motorway": 130,
    "fr:walk": 6,
}

# Scenery (visual only; no exam rule reads it).
SIDEWALK_WIDTH_M = 2.0           # matches the Unity importer
ROAD_CLEARANCE_M = 0.3           # walls/fences/trees stay this far beyond the generated sidewalk
BARRIER_HEIGHT_M = {"wall": 2.0, "fence": 1.6, "hedge": 1.4, "retaining_wall": 1.5}
MIN_BARRIER_PIECE_M = 1.0
TREE_ROW_SPACING_M = 8.0
TREE_AREA_M2 = {"wood": 60.0, "forest": 60.0, "scrub": 90.0,
                "park": 250.0, "garden": 250.0, "grass": 400.0, "village_green": 300.0,
                "meadow": 600.0, "recreation_ground": 400.0}
MAX_TREES = 6000
TREE_MAX_RADIUS_M = 1050.0       # the generated ground ends at 1100 m
UPPER_WIDTH_M = {"rail": 5.0, "footway": 3.0, "path": 3.0, "cycleway": 3.0, "pedestrian": 4.0, "steps": 3.0}
MIN_CROSSING_SIN = 0.4           # caps how long a very skewed crossing can make an underpass
MERGE_GAP_M = 6.0
TUNNEL_MOTORWAY_NEAR_M = 30.0                # crossings closer than this along a street form one underpass

_to_lambert93 = Transformer.from_crs("EPSG:4326", "EPSG:2154", always_xy=True)


def _point(lon: float, lat: float, origin_e: float, origin_n: float) -> dict:
    e, n = _to_lambert93.transform(lon, lat)
    return {"x": round(e - origin_e, COORD_DECIMALS), "z": round(n - origin_n, COORD_DECIMALS)}


def _first(value):
    """OSM attributes merged across simplified edges come back as lists."""
    if isinstance(value, list):
        return value[0] if value else None
    return value


def _is_missing(value) -> bool:
    return value is None or value != value  # NaN check


def _int_or(value, default: int) -> int:
    values = value if isinstance(value, list) else [value]
    parsed = []
    for v in values:
        if _is_missing(v):
            continue
        match = re.search(r"\d+", str(v))
        if match:
            parsed.append(int(match.group()))
    return max(parsed) if parsed else default


def _maxspeed_kmh(value) -> int:
    value = _first(value)
    if _is_missing(value):
        return 0
    text = str(value).strip().lower()
    if text in IMPLICIT_MAXSPEED_KMH:
        return IMPLICIT_MAXSPEED_KMH[text]
    match = re.match(r"(\d+)\s*(mph)?", text)
    if not match:
        return 0
    speed = int(match.group(1))
    return round(speed * 1.609) if match.group(2) else speed


def _osm_id(value) -> int:
    value = _first(value)
    return int(value) if not _is_missing(value) else 0


def _text(value) -> str:
    value = _first(value)
    return "" if _is_missing(value) else str(value)


def build_roads(graph, origin_e, origin_n) -> list:
    # Undirected so a two-way street becomes one road instead of two overlapping ones.
    undirected = ox.convert.to_undirected(graph)
    _, edges = ox.convert.graph_to_gdfs(undirected)

    roads = []
    for _, edge in edges.iterrows():
        one_way = str(_first(edge.get("oneway"))).lower() in ("true", "yes", "1")
        lanes = _int_or(edge.get("lanes"), 1 if one_way else 2)
        roads.append({
            "osmId": _osm_id(edge.get("osmid")),
            "name": _text(edge.get("name")),
            "highway": _text(edge.get("highway")),
            "points": [_point(lon, lat, origin_e, origin_n) for lon, lat in edge.geometry.coords],
            "lanes": lanes,
            "widthM": lanes * LANE_WIDTH_M,
            "oneWay": one_way,
            "maxspeedKmh": _maxspeed_kmh(edge.get("maxspeed")),
        })
    return roads


def build_intersections(graph, origin_e, origin_n) -> list:
    nodes = ox.convert.graph_to_gdfs(graph, edges=False)
    intersections = []
    for osm_id, node in nodes.iterrows():
        street_count = int(node.get("street_count", 0) or 0)
        if street_count >= 3:
            intersections.append({
                "osmId": int(osm_id),
                "position": _point(node["x"], node["y"], origin_e, origin_n),
                "streetCount": street_count,
                "highwayTag": _text(node.get("highway")),
            })
    return intersections


def build_features(origin_e, origin_n) -> tuple[list, list]:
    features = gpd.read_file(CACHE_DIR / "argenteuil_features.geojson")

    buildings = []
    signs = []
    for _, feature in features.iterrows():
        geom = feature.geometry
        if geom is None:
            continue

        if not _is_missing(feature.get("building")):
            polygons = list(geom.geoms) if geom.geom_type == "MultiPolygon" else [geom]
            for polygon in polygons:
                if polygon.geom_type != "Polygon":
                    continue
                # Drop the closing point, which repeats the first.
                coords = list(polygon.exterior.coords)[:-1]
                buildings.append({
                    "osmId": _osm_id(feature.get("id")),
                    "footprint": [_point(lon, lat, origin_e, origin_n) for lon, lat in coords],
                    "heightM": _int_or(feature.get("building:levels"), DEFAULT_LEVELS) * LEVEL_HEIGHT_M,
                })
            continue

        highway_tag = _text(feature.get("highway"))
        rule = SIGN_RULE_BY_HIGHWAY_TAG.get(highway_tag)
        if rule and geom.geom_type == "Point":
            signs.append({
                "osmId": _osm_id(feature.get("id")),
                "type": highway_tag,
                "examRule": rule,
                "position": _point(geom.x, geom.y, origin_e, origin_n),
                "direction": _text(feature.get("direction")),
            })
    return buildings, signs


def _local(geom, origin_e, origin_n):
    """Lambert-93 geometry moved so the exam start point is the origin."""
    return affinity.translate(geom, -origin_e, -origin_n)


def _xz(x, y) -> dict:
    return {"x": round(x, COORD_DECIMALS), "z": round(y, COORD_DECIMALS)}


def _line_points(line) -> list:
    return [_xz(x, y) for x, y in line.coords]


def _drive_edges(graph, origin_e, origin_n) -> list:
    """(local LineString, width m, highway) for every drivable street, as the importer draws them."""
    undirected = ox.convert.to_undirected(graph)
    _, edges = ox.convert.graph_to_gdfs(undirected)
    edges = edges.to_crs("EPSG:2154")
    result = []
    for _, edge in edges.iterrows():
        one_way = str(_first(edge.get("oneway"))).lower() in ("true", "yes", "1")
        lanes = _int_or(edge.get("lanes"), 1 if one_way else 2)
        result.append((_local(edge.geometry, origin_e, origin_n), lanes * LANE_WIDTH_M, _text(edge.get("highway"))))
    return result


def _upper_width(feature) -> float:
    if not _is_missing(feature.get("railway")):
        return UPPER_WIDTH_M["rail"]
    highway = _text(feature.get("highway"))
    if highway in UPPER_WIDTH_M:
        return UPPER_WIDTH_M[highway]
    return _int_or(feature.get("lanes"), 2) * LANE_WIDTH_M + 1.0


def _is_steel(feature) -> bool:
    return not _is_missing(feature.get("railway")) or _text(feature.get("highway")).startswith("motorway")


def build_underpasses(scenery, drive_edges) -> list:
    """Stretches of drivable street that pass under something: OSM tunnel segments, and
    streets crossed by a railway/road/foot bridge. The importer roofs them with a deck."""
    tunnels = scenery[(scenery["tunnel"] == "yes") & scenery["highway"].notna()]
    bridges = scenery[scenery["bridge"].notna() & scenery.geometry.geom_type.eq("LineString")]
    edge_tree = STRtree([geom for geom, _, _ in drive_edges])
    underpasses = []
    covered = []

    for _, tunnel in tunnels.iterrows():
        line = tunnel.geometry
        # Only tunnels on streets the game draws (not car-park ramps or footpaths).
        nearest = edge_tree.nearest(line.interpolate(0.5, normalized=True))
        edge, width, _ = drive_edges[nearest]
        if edge.distance(line.interpolate(0.5, normalized=True)) > 3.0:
            continue
        # What passes over: a bridge way, or a motorway nearby (the street graph's motorway lines
        # can stop short of the tunnel, e.g. the A15 over rue d'Épinay, so "nearby" not "crossing").
        above = bridges[bridges.intersects(line.buffer(width))]
        motorway_near = any(hw.startswith("motorway") and geom.distance(line) < TUNNEL_MOTORWAY_NEAR_M
                            for geom, _, hw in drive_edges)
        steel = any(_is_steel(b) for _, b in above.iterrows()) or motorway_near
        underpasses.append({"points": _line_points(line), "roadWidthM": width, "steel": steel, "source": "tunnel"})
        covered.append(line.buffer(width * 0.5 + 3.0))

    covered_area = unary_union(covered) if covered else Polygon()
    seen = set()
    for edge, width, _ in drive_edges:
        intervals = []
        for _, upper in bridges.iterrows():
            crossing = edge.intersection(upper.geometry)
            if crossing.is_empty or crossing.geom_type != "Point" or covered_area.contains(crossing):
                continue
            along = edge.project(crossing)
            # Skip where this street is itself the bridge deck (it overlaps a bridge way there).
            local = substring(edge, max(0.0, along - 2.0), min(edge.length, along + 2.0))
            if any(b.geometry.buffer(1.0).contains(local) for _, b in bridges.iterrows()):
                continue
            street_dir = _direction(edge, along)
            upper_dir = _direction(upper.geometry, upper.geometry.project(crossing))
            sin = max(MIN_CROSSING_SIN, abs(street_dir[0] * upper_dir[1] - street_dir[1] * upper_dir[0]))
            half = _upper_width(upper) * 0.5 / sin
            intervals.append([along - half, along + half, _is_steel(upper)])

        for start, end, steel in _merge(intervals):
            start, end = max(0.0, start), min(edge.length, end)
            if end - start < 1.0:
                continue
            part = substring(edge, start, end)
            key = tuple(round(v) for v in (*part.coords[0], *part.coords[-1]))
            if key in seen or key[2:] + key[:2] in seen:
                continue  # duplicate parallel graph edge
            seen.add(key)
            underpasses.append({"points": _line_points(part), "roadWidthM": width, "steel": steel, "source": "bridge"})
    return underpasses


def _direction(line, along):
    a = line.interpolate(max(0.0, along - 1.0))
    b = line.interpolate(min(line.length, along + 1.0))
    dx, dy = b.x - a.x, b.y - a.y
    length = math.hypot(dx, dy) or 1.0
    return dx / length, dy / length


def _merge(intervals):
    merged = []
    for start, end, steel in sorted(intervals):
        if merged and start - merged[-1][1] < MERGE_GAP_M:
            merged[-1][1] = max(merged[-1][1], end)
            merged[-1][2] = merged[-1][2] or steel
        else:
            merged.append([start, end, steel])
    return merged


def _keep_out_area(drive_edges):
    """Street + generated sidewalk + clearance: no wall, fence or tree may stand here."""
    return unary_union([geom.buffer(width * 0.5 + SIDEWALK_WIDTH_M + ROAD_CLEARANCE_M, cap_style="flat")
                        for geom, width, _ in drive_edges])


def build_barriers(scenery, keep_out) -> list:
    barriers = []
    for _, feature in scenery[scenery["barrier"].notna()].iterrows():
        kind = _text(feature.get("barrier"))
        if kind not in BARRIER_HEIGHT_M:
            continue
        geom = feature.geometry
        line = geom.exterior if geom.geom_type == "Polygon" else geom
        clipped = line.difference(keep_out)
        pieces = clipped.geoms if isinstance(clipped, MultiLineString) else [clipped]
        height = BARRIER_HEIGHT_M[kind]
        for piece in pieces:
            if piece.is_empty or piece.geom_type != "LineString" or piece.length < MIN_BARRIER_PIECE_M:
                continue
            barriers.append({"type": kind, "heightM": height, "points": _line_points(piece)})
    return barriers


def _tree(x, y, rng, scale=1.0) -> dict:
    height = rng.uniform(6.0, 12.0) * scale
    return {"position": _xz(x, y), "heightM": round(height, 1),
            "crownRadiusM": round(height * rng.uniform(0.25, 0.35), 1)}


def build_trees(scenery, keep_out, building_tree, underpass_area) -> list:
    candidates = []
    for _, feature in scenery.iterrows():
        geom = feature.geometry
        rng = random.Random(_osm_id(feature.get("id")))
        natural = _text(feature.get("natural"))
        area_kind = natural or _text(feature.get("landuse")) or _text(feature.get("leisure"))
        if natural == "tree" and geom.geom_type == "Point":
            candidates.append(_tree(geom.x, geom.y, rng))
        elif natural == "tree_row" and geom.geom_type == "LineString":
            steps = max(1, int(geom.length // TREE_ROW_SPACING_M))
            for i in range(steps + 1):
                p = geom.interpolate(i / steps, normalized=True)
                candidates.append(_tree(p.x, p.y, rng))
        elif area_kind in TREE_AREA_M2 and geom.geom_type in ("Polygon", "MultiPolygon"):
            spacing = math.sqrt(TREE_AREA_M2[area_kind])
            scale = 0.6 if area_kind == "scrub" else 1.0
            min_x, min_y, max_x, max_y = geom.bounds
            y = min_y + spacing * 0.5
            while y < max_y:
                x = min_x + spacing * 0.5
                while x < max_x:
                    jx = x + rng.uniform(-0.4, 0.4) * spacing
                    jy = y + rng.uniform(-0.4, 0.4) * spacing
                    if geom.contains(Point(jx, jy)):
                        candidates.append(_tree(jx, jy, rng, scale))
                    x += spacing
                y += spacing

    candidates = [t for t in candidates if math.hypot(t["position"]["x"], t["position"]["z"]) <= TREE_MAX_RADIUS_M]
    candidates.sort(key=lambda t: math.hypot(t["position"]["x"], t["position"]["z"]))
    trees = []
    for tree in candidates:
        p = Point(tree["position"]["x"], tree["position"]["z"])
        clearance = p.buffer(tree["crownRadiusM"] * 0.3)
        if keep_out.intersects(clearance) or underpass_area.intersects(p):
            continue
        if any(building_tree.geometries[i].intersects(clearance) for i in building_tree.query(clearance)):
            continue
        trees.append(tree)
        if len(trees) >= MAX_TREES:
            break
    return trees


def build_scenery(graph, buildings, origin_e, origin_n) -> tuple[list, list, list]:
    path = CACHE_DIR / "argenteuil_scenery.geojson"
    if not path.exists():
        print("No scenery cache (run fetch_area.py --scenery-only); exporting the map without scenery.")
        return [], [], []

    scenery = gpd.read_file(path).to_crs("EPSG:2154")
    scenery["geometry"] = scenery.geometry.map(lambda g: _local(g, origin_e, origin_n))
    for column in ("highway", "railway", "bridge", "tunnel", "barrier", "natural", "landuse", "leisure", "lanes"):
        if column not in scenery:
            scenery[column] = None

    drive_edges = _drive_edges(graph, origin_e, origin_n)
    keep_out = _keep_out_area(drive_edges)
    underpasses = build_underpasses(scenery, drive_edges)
    underpass_area = unary_union([LineString([(p["x"], p["z"]) for p in u["points"]]).buffer(u["roadWidthM"] * 0.5 + 4.0)
                                  for u in underpasses if len(u["points"]) >= 2]) if underpasses else Polygon()
    footprints = [Polygon([(p["x"], p["z"]) for p in b["footprint"]]) for b in buildings if len(b["footprint"]) >= 3]
    building_tree = STRtree([f if f.is_valid else f.buffer(0) for f in footprints])
    barriers = build_barriers(scenery, keep_out)
    trees = build_trees(scenery, keep_out, building_tree, underpass_area)
    return underpasses, barriers, trees


def build() -> None:
    origin = json.loads((CACHE_DIR / "origin.json").read_text())
    origin_e, origin_n = _to_lambert93.transform(origin["lon"], origin["lat"])

    graph = ox.load_graphml(CACHE_DIR / "argenteuil_graph.graphml")
    roads = build_roads(graph, origin_e, origin_n)
    intersections = build_intersections(graph, origin_e, origin_n)
    buildings, signs = build_features(origin_e, origin_n)
    underpasses, barriers, trees = build_scenery(graph, buildings, origin_e, origin_n)

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps({
        "origin": origin,
        "roads": roads,
        "intersections": intersections,
        "buildings": buildings,
        "signs": signs,
        "underpasses": underpasses,
        "barriers": barriers,
        "trees": trees,
    }))
    print(f"Wrote {len(roads)} roads, {len(intersections)} intersections, "
          f"{len(buildings)} buildings, {len(signs)} signs, {len(underpasses)} underpasses, "
          f"{len(barriers)} wall/fence pieces, {len(trees)} trees to {OUTPUT_PATH}")


if __name__ == "__main__":
    build()

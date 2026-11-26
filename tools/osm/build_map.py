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
import re
from pathlib import Path

import geopandas as gpd
import osmnx as ox
from pyproj import Transformer

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


def build() -> None:
    origin = json.loads((CACHE_DIR / "origin.json").read_text())
    origin_e, origin_n = _to_lambert93.transform(origin["lon"], origin["lat"])

    graph = ox.load_graphml(CACHE_DIR / "argenteuil_graph.graphml")
    roads = build_roads(graph, origin_e, origin_n)
    intersections = build_intersections(graph, origin_e, origin_n)
    buildings, signs = build_features(origin_e, origin_n)

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps({
        "origin": origin,
        "roads": roads,
        "intersections": intersections,
        "buildings": buildings,
        "signs": signs,
    }))
    print(f"Wrote {len(roads)} roads, {len(intersections)} intersections, "
          f"{len(buildings)} buildings, {len(signs)} signs to {OUTPUT_PATH}")


if __name__ == "__main__":
    build()

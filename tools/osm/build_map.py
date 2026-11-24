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
"""
import json
from pathlib import Path

import geopandas as gpd
import osmnx as ox
from pyproj import Transformer

CACHE_DIR = Path(__file__).parent / "cache"
OUTPUT_PATH = Path(__file__).parent.parent.parent / "Assets/_Project/Data/Maps/argenteuil_map.json"

LANE_WIDTH_M = 3.25
DEFAULT_LANES = 1
LEVEL_HEIGHT_M = 3.0
DEFAULT_LEVELS = 4

SIGN_RULE_BY_HIGHWAY_TAG = {
    "stop": "stop_sign",
    "give_way": "yield",
    "traffic_signals": "red_light",
    "crossing": "pedestrian_crossing",
}

_to_lambert93 = Transformer.from_crs("EPSG:4326", "EPSG:2154", always_xy=True)


def project(lon: float, lat: float, origin_e: float, origin_n: float) -> tuple[float, float]:
    e, n = _to_lambert93.transform(lon, lat)
    return e - origin_e, n - origin_n


def _lane_count(tags: dict) -> int:
    try:
        return int(tags.get("lanes", DEFAULT_LANES))
    except (TypeError, ValueError):
        return DEFAULT_LANES


def _levels(tags: dict) -> int:
    try:
        return int(tags.get("building:levels", DEFAULT_LEVELS))
    except (TypeError, ValueError):
        return DEFAULT_LEVELS


def build() -> None:
    origin = json.loads((CACHE_DIR / "origin.json").read_text())
    origin_e, origin_n = _to_lambert93.transform(origin["lon"], origin["lat"])

    graph = ox.load_graphml(CACHE_DIR / "argenteuil_graph.graphml")
    _, edges = ox.graph_to_gdfs(graph)

    roads = []
    for _, edge in edges.iterrows():
        coords = [project(lon, lat, origin_e, origin_n) for lon, lat in edge.geometry.coords]
        roads.append({
            "points": [{"x": x, "z": z} for x, z in coords],
            "widthM": _lane_count(edge) * LANE_WIDTH_M,
            "oneWay": bool(edge.get("oneway", False)),
            "maxspeedKmh": edge.get("maxspeed"),
        })

    features = gpd.read_file(CACHE_DIR / "argenteuil_features.geojson")

    buildings = []
    signs = []
    for _, feature in features.iterrows():
        tags = feature.to_dict()
        geom = feature.geometry

        if tags.get("building"):
            if geom.geom_type != "Polygon":
                continue
            footprint = [project(lon, lat, origin_e, origin_n) for lon, lat in geom.exterior.coords]
            buildings.append({
                "footprint": [{"x": x, "z": z} for x, z in footprint],
                "heightM": _levels(tags) * LEVEL_HEIGHT_M,
            })
            continue

        highway_tag = tags.get("highway")
        rule = SIGN_RULE_BY_HIGHWAY_TAG.get(highway_tag)
        if rule and geom.geom_type == "Point":
            x, z = project(geom.x, geom.y, origin_e, origin_n)
            signs.append({"type": highway_tag, "examRule": rule, "position": {"x": x, "z": z}})

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps({
        "origin": origin,
        "roads": roads,
        "buildings": buildings,
        "signs": signs,
    }, indent=2))
    print(f"Wrote {len(roads)} roads, {len(buildings)} buildings, {len(signs)} signs to {OUTPUT_PATH}")


if __name__ == "__main__":
    build()

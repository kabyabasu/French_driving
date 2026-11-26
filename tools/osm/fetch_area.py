#!/usr/bin/env python3
"""Download OSM data around the Argenteuil exam start point and cache it locally.

Usage:
    python fetch_area.py [--address "235 rue d'Epinay, 95100 Argenteuil, France"] [--radius-m 1000]

Geocodes the address via OSM Nominatim (through osmnx) rather than hardcoding
coordinates, since the exact start point must come from the real address, not
a guess. Writes two files to tools/osm/cache/ (gitignored):
    argenteuil_graph.graphml   - street network (osmnx graph)
    argenteuil_features.geojson - buildings, signs, crossings, etc. (osmnx features)
"""
import argparse
import json
from pathlib import Path

import osmnx as ox

CACHE_DIR = Path(__file__).parent / "cache"

DEFAULT_ADDRESS = "235 rue d'Epinay, 95100 Argenteuil, France"
DEFAULT_RADIUS_M = 1000

# Feature tags mapped to game objects / exam rules per CLAUDE.md's OSM table.
FEATURE_TAGS = {
    "highway": ["stop", "give_way", "traffic_signals", "crossing"],
    "junction": ["roundabout"],
    "building": True,
}

# Only these columns are kept: raw OSM features carry hundreds of tags, some
# list-valued, which the GeoJSON writer can't serialize.
KEPT_COLUMNS = ["element", "id", "building", "building:levels", "highway", "junction", "direction", "geometry"]


def _slim(features):
    features = features.reset_index()
    # osmnx 1.x names the index levels element_type/osmid; 2.x uses element/id.
    features = features.rename(columns={"element_type": "element", "osmid": "id"})
    features = features[[c for c in KEPT_COLUMNS if c in features.columns]].copy()
    for column in features.columns:
        if column != "geometry":
            features[column] = features[column].map(lambda v: None if v is None or v != v else str(v))
    return features


def fetch(address: str, radius_m: float) -> None:
    CACHE_DIR.mkdir(parents=True, exist_ok=True)

    center_point = ox.geocode(address)
    print(f"Geocoded '{address}' -> {center_point}")

    graph = ox.graph_from_point(center_point, dist=radius_m, network_type="drive")
    graph_path = CACHE_DIR / "argenteuil_graph.graphml"
    ox.save_graphml(graph, graph_path)
    print(f"Saved street graph to {graph_path}")

    features = _slim(ox.features_from_point(center_point, tags=FEATURE_TAGS, dist=radius_m))
    features_path = CACHE_DIR / "argenteuil_features.geojson"
    features.to_file(features_path, driver="GeoJSON")
    print(f"Saved features to {features_path}")

    origin_path = CACHE_DIR / "origin.json"
    origin_path.write_text(json.dumps({
        "address": address,
        "lat": center_point[0],
        "lon": center_point[1],
        "radiusM": radius_m,
    }, indent=2))
    print(f"Saved origin point to {origin_path}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--address", default=DEFAULT_ADDRESS)
    parser.add_argument("--radius-m", type=float, default=DEFAULT_RADIUS_M)
    args = parser.parse_args()
    fetch(args.address, args.radius_m)

#!/usr/bin/env python3
"""Download OSM data around the Argenteuil exam start point and cache it locally.

Usage:
    python fetch_area.py [--address "235 rue d'Epinay, 95100 Argenteuil, France"] [--radius-m 1000]

Geocodes the address via OSM Nominatim (through osmnx) rather than hardcoding
coordinates, since the exact start point must come from the real address, not
a guess. Writes these files to tools/osm/cache/ (gitignored):
    argenteuil_graph.graphml    - street network (osmnx graph)
    argenteuil_features.geojson - buildings, signs, crossings, etc. (osmnx features)
    argenteuil_scenery.geojson  - bridges/tunnels, railways, walls/fences/hedges, trees, green areas

--scenery-only refreshes just the scenery file, so the street graph, buildings and signs that
routes and exam zones were checked against stay exactly as they were.
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

# Visual-only features (no exam rule): bridges and underpasses, plot boundaries, trees and
# the green areas trees are scattered in.
SCENERY_TAGS = {
    "railway": ["rail", "light_rail", "subway", "tram"],
    "bridge": True,
    "tunnel": True,
    "man_made": ["bridge"],
    "barrier": ["wall", "fence", "hedge", "retaining_wall"],
    "natural": ["tree", "tree_row", "wood", "scrub"],
    "landuse": ["forest", "grass", "meadow", "village_green", "recreation_ground"],
    "leisure": ["park", "garden"],
}

# Only these columns are kept: raw OSM features carry hundreds of tags, some
# list-valued, which the GeoJSON writer can't serialize.
KEPT_COLUMNS = ["element", "id", "building", "building:levels", "highway", "junction", "direction", "geometry"]
SCENERY_COLUMNS = ["element", "id", "highway", "railway", "bridge", "tunnel", "layer", "lanes", "man_made",
                   "barrier", "natural", "landuse", "leisure", "height", "service", "geometry"]


def _slim(features, columns=None):
    columns = columns or KEPT_COLUMNS
    features = features.reset_index()
    # osmnx 1.x names the index levels element_type/osmid; 2.x uses element/id.
    features = features.rename(columns={"element_type": "element", "osmid": "id"})
    features = features[[c for c in columns if c in features.columns]].copy()
    for column in features.columns:
        if column != "geometry":
            features[column] = features[column].map(lambda v: None if v is None or v != v else str(v))
    return features


def fetch_scenery(center_point, radius_m: float) -> None:
    scenery = _slim(ox.features_from_point(center_point, tags=SCENERY_TAGS, dist=radius_m), SCENERY_COLUMNS)
    scenery_path = CACHE_DIR / "argenteuil_scenery.geojson"
    scenery.to_file(scenery_path, driver="GeoJSON")
    print(f"Saved {len(scenery)} scenery features to {scenery_path}")


def fetch(address: str, radius_m: float, scenery_only: bool = False) -> None:
    CACHE_DIR.mkdir(parents=True, exist_ok=True)

    if scenery_only:
        # Reuse the exact cached origin so new scenery lines up with the existing map.
        origin = json.loads((CACHE_DIR / "origin.json").read_text())
        fetch_scenery((origin["lat"], origin["lon"]), origin["radiusM"])
        return

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

    fetch_scenery(center_point, radius_m)

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
    parser.add_argument("--overpass-url", default=None,
                        help="Overpass API mirror to use when the default server is overloaded, e.g. "
                             "https://maps.mail.ru/osm/tools/overpass/api")
    parser.add_argument("--scenery-only", action="store_true",
                        help="only refresh the visual-only scenery file")
    args = parser.parse_args()
    if args.overpass_url:
        ox.settings.overpass_url = args.overpass_url
    fetch(args.address, args.radius_m, args.scenery_only)

#!/usr/bin/env python3
"""Turn an exam route spec (tools/osm/routes/<id>.json) into a drivable route for Unity.

Usage:
    python build_route.py routes/argenteuil_loop1.json

A spec names the streets the route follows, in order ("via"), plus a start and an
end point in map metres. The route is found on the cached OSM drive graph
(one-way streets respected), strongly preferring the named streets, so it never
wanders onto streets the spec doesn't name unless it has to (and then it says so).

Output: Assets/_Project/Data/Routes/<id>_route.json with
  - points:     the centreline polyline (map metres, +X east, +Z north)
  - junctions:  every junction the route passes, with whether a drivable side
                street leaves to the left/right (used for "prenez la deuxième à droite")
  - maneuvers:  turns the examiner announces (left / right / straight on at a
                junction where the street changes / roundabout exit n)

Exam routes are not published: specs must come from a driving instructor, a GPS
trace or the user, never be invented.
"""
import json
import math
import sys
from pathlib import Path

import networkx as nx
import osmnx as ox
from pyproj import Transformer
from shapely import wkt
from shapely.geometry import LineString, Point

CACHE_DIR = Path(__file__).parent / "cache"
OUTPUT_DIR = Path(__file__).parent.parent.parent / "Assets/_Project/Data/Routes"

OFF_SPEC_COST = 25.0          # metres on an unnamed street cost this many metres on a named one
TURN_MIN_DEG = 35.0           # smaller direction changes at a junction are "follow the road"
SIDE_MIN_DEG = 25.0           # a side street must leave at least this far from straight on
BEARING_SAMPLE_M = 12.0       # bearings are measured over this length either side of a node
CLUSTER_M = 20.0              # junction nodes closer than this along the route form one junction
COORD_DECIMALS = 2

_to_lambert93 = Transformer.from_crs("EPSG:4326", "EPSG:2154", always_xy=True)


def _local(lon, lat, origin):
    e, n = _to_lambert93.transform(lon, lat)
    return e - origin[0], n - origin[1]


def _edge_line(graph, u, v, data, origin):
    """The edge's geometry from u to v in map metres."""
    geometry = data.get("geometry")
    if isinstance(geometry, str):
        geometry = wkt.loads(geometry)
    if geometry is None:
        coords = [(graph.nodes[u]["x"], graph.nodes[u]["y"]), (graph.nodes[v]["x"], graph.nodes[v]["y"])]
    else:
        coords = list(geometry.coords)
        start = graph.nodes[u]
        if math.dist(coords[0], (start["x"], start["y"])) > math.dist(coords[-1], (start["x"], start["y"])):
            coords.reverse()
    return LineString([_local(x, y, origin) for x, y in coords])


def _names(data):
    name = data.get("name", "")
    if isinstance(name, list):
        return set(name)
    if isinstance(name, str) and name.startswith("["):
        return set(n.strip(" '\"") for n in name.strip("[]").split(","))
    return {name} if name else set()


def _best_edge(graph, u, v, origin):
    data = min(graph.get_edge_data(u, v).values(), key=lambda d: float(d.get("length", 0)))
    return data, _edge_line(graph, u, v, data, origin)


def _bearing(dx, dz):
    return math.degrees(math.atan2(dx, dz))  # 0 = north, 90 = east


def _signed_turn(a_deg, b_deg):
    """Turn from heading a to heading b, -180..180, positive = right (clockwise seen from above)."""
    return (b_deg - a_deg + 540.0) % 360.0 - 180.0


def _heading_at_end(line, length_m):
    end = line.interpolate(line.length)
    back = line.interpolate(max(line.length - length_m, 0.0))
    return _bearing(end.x - back.x, end.y - back.y)


def _heading_at_start(line, length_m):
    start = line.interpolate(0.0)
    ahead = line.interpolate(min(length_m, line.length))
    return _bearing(ahead.x - start.x, ahead.y - start.y)


def _nearest_named_edge(graph, lines, point, street):
    best = None
    for (u, v, k), line in lines.items():
        if street not in _names(graph.edges[u, v, k]):
            continue
        d = line.distance(point)
        if best is None or d < best[0]:
            best = (d, u, v, k)
    if best is None:
        sys.exit(f"No street named {street!r} in the graph.")
    return best


def _route_nodes(graph, lines, spec):
    """Node path following the spec's streets in order, from start to end."""
    via = spec["via"]
    start = Point(spec["start"]["x"], spec["start"]["z"])
    end = Point(spec["end"]["x"], spec["end"]["z"])

    # Weighted cost: preferred streets for each leg cost their length, any other street much more.
    def weight_for(allowed):
        def weight(u, v, edges):
            best = math.inf
            for data in edges.values():
                factor = 1.0 if _names(data) & allowed else OFF_SPEC_COST
                best = min(best, float(data.get("length", 1.0)) * factor)
            return best
        return weight

    _, su, sv, sk = _nearest_named_edge(graph, lines, start, via[0])
    _, eu, ev, ek = _nearest_named_edge(graph, lines, end, via[-1])

    def legs_from(first_node):
        path = [first_node]
        for i in range(1, len(via)):
            targets = {n for u, v, d in graph.edges(data=True) if via[i] in _names(d) for n in (u, v)}
            allowed = {via[i - 1], via[i]}
            lengths, paths = nx.single_source_dijkstra(graph, path[-1], weight=weight_for(allowed))
            reachable = [t for t in targets if t in lengths]
            if not reachable:
                return None, math.inf
            target = min(reachable, key=lengths.get)
            path += paths[target][1:]
        allowed = {via[-1]}
        try:
            tail = nx.dijkstra_path(graph, path[-1], eu, weight=weight_for(allowed))
        except nx.NetworkXNoPath:
            return None, math.inf
        path += tail[1:]
        if path[-1] != ev and graph.has_edge(eu, ev):
            path.append(ev)
        cost = sum(weight_for(set(via))(a, b, graph.get_edge_data(a, b)) for a, b in zip(path, path[1:]))
        return path, cost

    # The start lies on edge su -> sv; leave it towards whichever end makes the shorter route.
    options = []
    for first in (sv, su):
        if first == su and not graph.has_edge(sv, su):
            continue  # one-way: can't drive back towards su
        path, cost = legs_from(first)
        if path is not None:
            options.append((cost, first, path))
    if not options:
        sys.exit("No route found through the spec's streets.")
    _, first, path = min(options)
    other = su if first == sv else sv
    return [other] + path, start, end


def build(spec_path: Path) -> None:
    spec = json.loads(spec_path.read_text())
    origin_ll = json.loads((CACHE_DIR / "origin.json").read_text())
    origin = _to_lambert93.transform(origin_ll["lon"], origin_ll["lat"])
    graph = ox.load_graphml(CACHE_DIR / "argenteuil_graph.graphml")
    lines = {(u, v, k): _edge_line(graph, u, v, d, origin) for u, v, k, d in graph.edges(keys=True, data=True)}

    nodes, start, end = _route_nodes(graph, lines, spec)

    # Polyline: edges end to end, then trimmed to the start and end points.
    coords, edge_ranges, streets = [], [], []
    for a, b in zip(nodes, nodes[1:]):
        data, line = _best_edge(graph, a, b, origin)
        part = list(line.coords)
        if coords:
            part = part[1:]
        begin = len(coords)
        coords += part
        edge_ranges.append((begin, len(coords) - 1))
        streets.append(data)
    full = LineString(coords)
    s_start = full.project(start)
    s_end = full.project(end)

    node_s = [0.0]
    for (_, last), _data in zip(edge_ranges, streets):
        node_s.append(full.project(Point(coords[last])))

    off_spec = sorted({n for d in streets for n in _names(d)} - set(spec["via"]) - {""})
    if off_spec:
        print(f"Warning: the route also uses streets not in the spec: {', '.join(off_spec)}")

    # Junctions and maneuvers. Nodes a few metres apart along the route (a crossroads with a
    # dual carriageway, a short link) are one junction: judged from the way into the first node
    # to the way out of the last.
    def is_ring(data):
        return data.get("junction") == "roundabout"

    def is_junction_node(i):
        return int(graph.nodes[nodes[i]].get("street_count", 0)) >= 3

    junctions, maneuvers = [], []
    roundabout_entry = None
    i = 1
    while i < len(nodes) - 1:
        first = i
        if node_s[first] - s_start <= 0.0 or node_s[first] >= s_end:
            i += 1
            continue
        in_data, in_line = _best_edge(graph, nodes[first - 1], nodes[first], origin)
        last = first
        while (last + 1 < len(nodes) - 1 and is_junction_node(last) and is_junction_node(last + 1)
               and node_s[last + 1] - node_s[last] < CLUSTER_M and node_s[last + 1] < s_end
               and not is_ring(in_data) and not is_ring(_best_edge(graph, nodes[last], nodes[last + 1], origin)[0])
               and not is_ring(_best_edge(graph, nodes[last + 1], nodes[last + 2], origin)[0])):
            last += 1
        i = last + 1

        out_data, out_line = _best_edge(graph, nodes[last], nodes[last + 1], origin)
        heading_in = _heading_at_end(in_line, BEARING_SAMPLE_M)
        heading_out = _heading_at_start(out_line, BEARING_SAMPLE_M)
        turn = _signed_turn(heading_in, heading_out)
        in_ring = is_ring(in_data)
        out_ring = is_ring(out_data)

        # Drivable side streets of the whole junction (not the route's own edges), as turns from the way in.
        route_nodes = set(nodes[first - 1:last + 2])
        side_turns = []
        for k in range(first, last + 1):
            for succ in graph.successors(nodes[k]):
                if succ in route_nodes:
                    continue
                _, side_line = _best_edge(graph, nodes[k], succ, origin)
                side_turns.append(_signed_turn(heading_in, _heading_at_start(side_line, BEARING_SAMPLE_M)))
        # Sides are judged against the exit the route takes, so a fork counts too.
        left = any(t < turn - SIDE_MIN_DEG * 0.4 for t in side_turns)
        right = any(t > turn + SIDE_MIN_DEG * 0.4 for t in side_turns)
        is_junction = any(is_junction_node(k) for k in range(first, last + 1))

        s = node_s[first] - s_start
        if is_junction:
            junctions.append({"s": round(s, COORD_DECIMALS), "left": left, "right": right})

        position = Point(coords[edge_ranges[first - 1][1]])
        base = {"s": round(s, COORD_DECIMALS), "x": round(position.x, COORD_DECIMALS),
                "z": round(position.y, COORD_DECIMALS), "exit": 0,
                "street": ", ".join(sorted(_names(out_data))) or ""}

        if out_ring and not in_ring:
            roundabout_entry = (base, 0)
            continue
        if in_ring and out_ring:
            if side_turns and roundabout_entry is not None:
                roundabout_entry = (roundabout_entry[0], roundabout_entry[1] + 1)
            continue
        if in_ring and not out_ring:
            if roundabout_entry is not None:
                entry, passed = roundabout_entry
                maneuvers.append({**entry, "type": "roundabout", "exit": passed + 1, "street": base["street"]})
                roundabout_entry = None
            continue

        if not is_junction or not side_turns:
            continue  # nothing else to choose here: following the road needs no instruction
        straightest_other = min(side_turns, key=abs)
        same_street = bool(_names(in_data) & _names(out_data))
        if abs(turn) <= abs(straightest_other):
            # The route is the most straight-on choice: announce only if the street changes.
            if not same_street and abs(turn) < TURN_MIN_DEG:
                maneuvers.append({**base, "type": "straight"})
            elif abs(turn) >= TURN_MIN_DEG:
                maneuvers.append({**base, "type": "right" if turn > 0 else "left"})
        else:
            maneuvers.append({**base, "type": "right" if turn > straightest_other else "left"})

    trimmed = _trim(full, s_start, s_end)
    points = [{"x": round(x, COORD_DECIMALS), "z": round(z, COORD_DECIMALS)} for x, z in trimmed.coords]
    out = {
        "id": spec["id"],
        "name": spec["name"],
        "source": spec["source"],
        "lengthM": round(trimmed.length, COORD_DECIMALS),
        "points": points,
        "junctions": junctions,
        "maneuvers": maneuvers,
    }
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    out_path = OUTPUT_DIR / f"{spec['id']}_route.json"
    out_path.write_text(json.dumps(out, ensure_ascii=False, indent=1))

    print(f"Wrote {out_path} ({trimmed.length:.0f} m, {len(junctions)} junctions, {len(maneuvers)} maneuvers)")
    for m in maneuvers:
        label = f"roundabout exit {m['exit']}" if m["type"] == "roundabout" else m["type"]
        print(f"  {m['s']:7.0f} m  {label:<18} -> {m['street']}  at ({m['x']:.0f}, {m['z']:.0f})")


def _trim(line, s0, s1):
    from shapely.ops import substring
    return substring(line, s0, s1)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    build(Path(sys.argv[1]))

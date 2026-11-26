# CLAUDE.md — PermisVR

VR driving game for **Meta Quest 3**. The player drives a French *permis B* exam route and is graded like the real practical exam (CEPC grid): points per criterion, plus instant-fail *fautes éliminatoires*. Built in Unity, runs standalone on the headset.

## Priorities (in order)
1. **Grading is correct and explainable.** Every penalty points to a rule, a moment and a place on the route.
2. **Stable frame rate on Quest 3.** 72 Hz minimum, 90 Hz target. A dropped frame in VR is a bug.
3. **Comfort.** No motion-sickness triggers.
4. Visuals come last.

## Tech stack
- Unity `6000.6.10f1` with URP
- Meta XR SDK `com.meta.xr.sdk.core` `209.0.0` (Core only; the all-in-one bundle's Platform SDK breaks sideloaded installs)
- Android, ARM64, IL2CPP
- Unity Test Framework (EditMode tests for all grading logic)
- Git + Git LFS for binary assets
- Gearbox: automatic first (simpler in VR), manual later

## Commands
Set the Unity path once in your shell:
- macOS: `export UNITY="/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity"`
- Windows (Git Bash): `export UNITY="/c/Program Files/Unity/Hub/Editor/<version>/Editor/Unity.exe"`

```bash
# EditMode tests: run after every change to grading code (do NOT add -quit with -runTests)
"$UNITY" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
  -testResults TestResults/editmode.xml -logFile -

# Build the APK (create Assets/_Project/Scripts/Editor/BuildTools.cs if it doesn't exist)
"$UNITY" -batchmode -quit -projectPath . -executeMethod PermisVR.Editor.BuildTools.BuildQuest -logFile -

# Install and watch logs on the headset (USB, developer mode on)
adb install -r Builds/PermisVR.apk
adb logcat -s Unity
```

Batch mode fails if the project is open in the Unity Editor. If it is, ask me to run the tests from **Window > General > Test Runner** instead.

## Folder structure
```
Assets/_Project/
  Scripts/
    Exam/        # Pure C# grading logic. asmdef with noEngineReferences: true
    ExamUnity/   # MonoBehaviour adapters: trigger zones, sensors -> ExamEvents
    Vehicle/     # Car physics, gearbox, indicators, lights
    Input/       # Driving action map, virtual steering wheel, trigger curves, haptics
    Traffic/     # AI cars, pedestrians, traffic lights
    Route/       # Route definition, examiner instructions
    UI/          # In-car HUD, results screen (CEPC-style grid)
    Editor/      # Build tools, editor helpers
  Data/          # ScriptableObjects: grading grid, thresholds, routes
    Maps/        # Generated map JSON + manual overrides per city
  Scenes/
  Prefabs/
  Tests/EditMode/ # Tests for Exam/ (one test file per rule)
tools/osm/       # Python: download OpenStreetMap data, build map JSON
```
Each `Scripts/` subfolder has its own assembly definition (`.asmdef`).

## Input (current: Quest 3 controllers only)
A real wheel + pedals (Windows build via Quest Link) may come later. To keep that possible:
- Use the Unity Input System with one action map, `Driving`.
- **Vehicle code reads only these actions, never devices.** Adding a wheel later must not require changes in `Vehicle/`.
- No hand tracking for now (no analog input for pedals).

| Action | Type | Range | Quest binding |
|---|---|---|---|
| `Steer` | Value | -1 (left) to +1 (right) | Virtual steering wheel (see below) |
| `Throttle` | Value | 0 to 1 | Right trigger |
| `Brake` | Value | 0 to 1 | Left trigger |
| `IndicatorLeft` / `IndicatorRight` | Button | | Left thumbstick flick left / right; flick again to cancel |
| `Horn` | Button | | Right thumbstick press |
| `GearDrive` / `GearReverse` | Button | | A / B (only accepted when speed < 1 km/h) |
| `ParkingBrake` | Button (toggle) | | Y |
| `RecenterSeat` | Button | | X |
| `Pause` | Button | | Left menu button |

### Virtual steering wheel (`Input/VirtualSteeringWheel.cs`)
- Grabbed with the grip buttons; works with one or two hands.
- For each gripping hand, project the hand position onto the wheel plane, compute its angle around the wheel center, and add the frame-to-frame delta to the wheel angle. This allows hand-over-hand turning beyond 180°.
- `Steer = wheelAngle / maxWheelAngle`. `maxWheelAngle` lives in a ScriptableObject (default ±450°, i.e. 900° lock-to-lock); front wheels max ~35°.
- When released, the wheel self-centers at a rate that increases with car speed, like a real car.
- Indicators self-cancel once the wheel returns near center after a turn.

### Triggers and haptics
- Dead zone (~0.05) and a response curve on both triggers, configurable in data.
- Light controller haptics for curb strikes, rumble strips and collisions. Keep intensity low; it should inform, not annoy.

### Seat calibration
On start (and on `RecenterSeat`), the player holds both controllers where they want the wheel and presses X. The cockpit is positioned so the virtual wheel sits there.

## Map: Argenteuil (first map)
- **Start point:** practical-exam start point at 235 rue d'Épinay, 95100 Argenteuil (official DRIEAT Île-de-France list, April 2024; confirm with a local auto-école). This is the world origin (0, 0, 0).
- **Phase 1 area:** ~1 km radius around the start point. Expand later along real exam routes.
- **Exam routes are not published.** Inspectors choose them. Real routes come from a local driving instructor or from GPS traces I record; never invent them.
- **Source data:** OpenStreetMap (ODbL). The credits screen must show "© OpenStreetMap contributors".

### Pipeline
```
tools/osm/fetch_area.py  -> download OSM data for the area (cached in tools/osm/cache/, gitignored)
tools/osm/build_map.py   -> parse, project, export Assets/_Project/Data/Maps/argenteuil_map.json
Unity menu: PermisVR > Map > Import Argenteuil (Editor script)
                         -> generates roads, sidewalks, buildings, signs and exam zones
```
- Python deps (osmnx, pyproj, shapely) in `tools/osm/requirements.txt`; use a virtual environment.
- Projection: Lambert-93 (EPSG:2154), origin at the start point. 1 Unity unit = 1 m, +X = east, +Z = north.
- Everything the importer creates lives under a `Map_Generated` root and is fully regenerated on each import. Never hand-edit it.
- Manual fixes (missing signs, wrong speed limits, lane counts) go in `Data/Maps/argenteuil_overrides.json`, which the importer applies last.

### OSM tags -> game objects -> exam rules
| OSM tag | Game object | Exam rule |
|---|---|---|
| `highway=stop` | STOP sign + stop line | Stop-sign rule (eliminatory) |
| `highway=give_way` | Cédez-le-passage sign + dashed line | Yield rule |
| `highway=traffic_signals` | Traffic light + stop line | Red-light rule (eliminatory) |
| `highway=crossing` | Pedestrian crossing + pedestrian spawn | Pedestrian-yield rule |
| `junction=roundabout` | Roundabout ring | Yield at entry, indicator on exit |
| `maxspeed=*` | Speed zone + signs where it changes | Speed rule |
| `oneway=yes` | One-way direction | Wrong-way rule (eliminatory) |
| Intersection with none of the above | Nothing extra | **Priorité à droite** (French default) |
| `lanes`, `width` | Road width (default 3.25 m per lane) | Lane-position checks |
| `building` + `building:levels` | Extruded box (3 m per level, default 4 levels) | None |

OSM data can be incomplete. A missing tag does not prove there is no sign: before an intersection is used on a route, verify it against street-level imagery and fix it in the overrides file.

### Map performance
- Merge buildings per 200 m tile; one shared façade texture atlas; no interiors.
- Simple LOD boxes beyond ~300 m; fog and culling beyond ~500 m.
- Roads and sidewalks as merged meshes per tile; signs and lights as instanced prefabs.

## Architecture: how grading works
```
Sensors (trigger zones, head tracking, vehicle state)
  -> ExamEvent (plain C# struct: type, time, route position, payload)
  -> ExamEventBus
  -> Rule evaluators (one class per rule, implements IExamRule)
  -> ScoreSheet (criteria 0-3 or E, list of faults)
  -> Results screen + markers on the route map
```
- Rules never read scene objects. They only receive `ExamEvent`s and vehicle snapshots, so they can be tested without Unity.
- Every fault stores: rule id, criterion, severity (`Minor` / `Eliminatory`), timestamp, route position, and a short French explanation (e.g. "Arrêt non marqué au STOP").
- All thresholds (speeds, distances, angles, times) live in ScriptableObjects. No magic numbers in rule code.
- Adding a rule = new file in `Exam/Rules/` + tests in `Tests/EditMode/Rules/` + entry in the rules config asset.

## Grading model (French permis B)
Keep these values in data, not code. **Verify against the official Sécurité routière "Guide B" before release.**
- Exam lasts about 32 minutes, with at least 25 minutes of driving.
- 31 points total. Pass = at least 20 points AND no eliminatory fault.
- Each criterion is scored 0, 1, 2, 3, or E (eliminatory). Bonus points exist for courtesy and eco-driving.
- Competency families: know and control the vehicle; read the road; share the road; drive autonomously, safely and economically.

### Eliminatory faults (initial list)
| Fault | Detection idea |
|---|---|
| STOP not respected | Speed never drops below ~0.5 km/h inside the stop zone before the line |
| Red light run | Front axle crosses the stop line while the light is red |
| Priority refused (priorité à droite, cédez-le-passage, roundabout entry) | Entering the conflict zone while a priority vehicle's time-to-arrival is below threshold |
| Pedestrian not yielded at a crossing | Entering the crossing while a pedestrian is on it or about to step on |
| Crossing a continuous line | Wheel overlaps a solid-line collider |
| Curb strike / mounting the curb | Collision with a curb collider above an impact threshold |
| Collision or near-miss | Contact, or time-to-collision below threshold |
| Wrong way | Heading vs. lane direction above ~120° |
| Examiner intervention | The game's automatic "examiner brake" fires |
| Serious speeding | Above the limit by more than a configured margin |

### Minor faults (lower a criterion's score)
- Missing or late indicator before a turn or lane change.
- No mirror check: head yaw toward the mirror not detected within N seconds before a maneuver.
- No blind-spot (shoulder) check before a lane change or pulling out.
- Slight speeding, driving too slowly, poor lane position, late or harsh braking, harsh acceleration (eco-driving), stalling (manual only).

## French road rules to model
- Drive on the right. Unsigned intersections are **priorité à droite**.
- Roundabouts with "Cédez le passage" at the entry: entering traffic yields to traffic already on the ring.
- Speeds in km/h: 50 in town (30 zones are common), 80 on two-way roads without a central barrier (some departments use 90), 110 on dual carriageways, 130 on motorways.
- Signs needed first: STOP, Cédez le passage, traffic lights, speed limits, zone 30, pedestrian crossings.
- UI and examiner voice in French; English toggle later.

## Quest 3 performance rules
- Profile on the device, not in the Editor.
- Starting budget (adjust after profiling): ≤ 200 draw calls, ≤ 750k visible triangles per frame.
- Baked lighting + light probes. At most one realtime directional light; shadows off or one short cascade.
- Static batching or GPU instancing for buildings and props; LODs on meshes over ~1k triangles.
- ASTC textures, atlases where possible, minimal post-processing. Fixed foveated rendering on.
- No allocations in `Update`/`FixedUpdate`: no LINQ, no string building, no `new` collections. Cache `GetComponent`; no `Find*` at runtime.
- Rigidbody interpolation on the player car to avoid judder.

## Comfort rules (non-negotiable)
- Never move or rotate the camera independently of the car. No camera shake, no head bob.
- Seated play; the cockpit is always visible as a stable frame of reference.
- Smooth acceleration curves, no teleports. Recenter view on demand.
- Optional comfort vignette at high angular velocity.

## Working rules for Claude
- Read the relevant existing scripts before changing anything and follow their patterns.
- Any change to grading logic includes new or updated EditMode tests, and you run them.
- Do not hand-edit `.unity`, `.prefab`, `.asset` or `.mat` YAML. If scene or prefab wiring is needed, write an Editor script or give me numbered Editor steps (menu path, component, field).
- Never create `.meta` files by hand. When moving or renaming an asset, move its `.meta` with it (`git mv` both).
- Ignore `Library/`, `Temp/`, `Logs/`, `obj/`, `Builds/`, `UserSettings/`: generated, never read or edit.
- Ask before changing `Packages/manifest.json`, `ProjectSettings/`, or third-party folders (`Assets/Oculus`, `Assets/Plugins`, imported assets), and before adding any package or Asset Store dependency.
- If something can only be verified on the headset (comfort, frame rate, tracking), say so and tell me exactly what to test.
- Small, focused commits. Message format: `area: summary` (e.g. `exam: add stop-sign rule`).

## Code style
- Namespaces `PermisVR.<Folder>`. PascalCase types and methods, camelCase locals, `_camelCase` private fields.
- `[SerializeField] private` instead of public fields. One class per file; file name = class name.
- Units in names: `speedKmh`, `distanceM`, `timeS`. Physics stays in m/s; convert to km/h at the sensor boundary.
- Each rule class starts with a one-line XML doc comment stating the real-world rule.

## Roadmap (update as milestones are done)
- [x] M1 Drive a car in VR on a flat test plane (seated, controllers as wheel)
- [ ] M2 Argenteuil starter area: OSM pipeline, roads, buildings and signs within ~1 km of the start point
- [ ] M3 First rules on real Argenteuil intersections (STOP, lights, crossings, priorité à droite) + tests
- [ ] M4 ScoreSheet + results screen styled like the CEPC grid
- [ ] M5 Real exam route loop(s) from the start point + examiner voice instructions
- [ ] M6 AI traffic and pedestrians, roundabouts
- [ ] M7 On-device performance pass, comfort options

## Glossary
- **Permis B**: car licence. **CEPC**: exam result certificate/grid. **Inspecteur**: examiner.
- **Faute éliminatoire (E)**: instant fail. **Priorité à droite**: yield to traffic from the right.
- **Cédez le passage**: yield sign. **Angle mort**: blind spot. **Rétroviseur**: mirror. **Clignotant**: indicator.

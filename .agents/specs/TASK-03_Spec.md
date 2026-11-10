# TASK-03 Technical Specification: Drone Pet State Machine & FetchBot Mechanics (Tractor Beam)

**Project:** VR Rehab Gamification (ExoTao VR Rehab)  
**Task ID:** TASK-03  
**Module:** Drone Pet / Companheiro IA  
**Author:** Specialist Subagent  
**Unity Version:** 6000.0.85f2 (Unity 6)  
**XR Interaction Toolkit Version:** 2.6.5  
**Active Render Pipeline:** Built-in Render Pipeline (SRP/URP-agnostic via `MaterialShaderHelper`)

---

## 1. Clinical & Therapeutic Rationale

In neurorehabilitation and robotic gait training (such as the ExoTao exoskeleton platform), clinical therapy can be physically demanding, repetitive, and mentally fatiguing for patients recovering from stroke, spinal cord injury, or neuromuscular disorders.

### Companion Pet & Fetch Mechanics:
1. **Affective Engagement & Motivation:** An autonomous companion pet drone (the "FetchBot / ExoBot") transforms clinical gait training into an interactive, rewarding experience inspired by Valve's *The Lab* (FetchBot).
2. **Ergonomic Safety Envelope:** Patients in exoskeletons must never bend over or reach toward the floor to pick up dropped or thrown therapy objects. The Drone Pet solves this fundamentally: whenever a therapy object (such as the `CyberFetchBaton`, therapy balls, or virtual fruits) is thrown or dropped beyond reach (> 2.5m), the drone automatically flies out, locks onto the item with a magnetic tractor beam, and delivers it directly to the patient at optimal chest height (1.15m from the ground).
3. **Upper-Limb Throwing Therapy:** Patients are motivated to practice upper-limb ballistic release and coordination, knowing that the drone will joyfully retrieve the object and return it into their hands.

---

## 2. Finite State Machine (FSM) Architecture

```
                    ┌──────────────────────────────────────────────┐
                    │               Idle / Guiding                 │
                    │   (DroneGuiaIA standard hover / telemetry)    │
                    └──────────────────────┬───────────────────────┘
                                           │ Target Thrown / Dropped > 2.5m
                                           ▼
                    ┌──────────────────────────────────────────────┐
                    │               FetchState.Alert               │
                    │   - Drone plays 'drone_fetch_alert.wav'      │
                    │   - Rotates toward item                      │
                    │   - HUD: "Objeto detectado! Vou buscar!"     │
                    └──────────────────────┬───────────────────────┘
                                           │ Alert chime finishes / 0.4s
                                           ▼
                    ┌──────────────────────────────────────────────┐
                    │          FetchState.FlyingToTarget           │
                    │   - Smooth acceleration & banking curve      │
                    │   - Reaches 0.65m hover above object         │
                    └──────────────────────┬───────────────────────┘
                                           │ Arrives above object (< 0.2m)
                                           ▼
                    ┌──────────────────────────────────────────────┐
                    │       FetchState.TractorBeamEngage           │
                    │   - TractorBeamEmitter fires cyan beam       │
                    │   - Plays continuous 'tractor_beam_hum.wav'  │
                    │   - Particle levitation & physics lift       │
                    │   - Object docks 0.28m below emitter         │
                    └──────────────────────┬───────────────────────┘
                                           │ Object docked & secured
                                           ▼
                    ┌──────────────────────────────────────────────┐
                    │           FetchState.CarryingBack            │
                    │   - Drone returns toward patient zone        │
                    │   - Smooth flight with suspended cargo       │
                    └──────────────────────┬───────────────────────┘
                                           │ Reaches patient proximity
                                           ▼
                    ┌──────────────────────────────────────────────┐
                    │     FetchState.PresentingAtChestHeight       │
                    │   - Drone hovers at Y = 1.15m (chest level)  │
                    │   - 0.65m in front of patient                │
                    │   - HUD: "Aqui está! Pronto para arremessar!"│
                    └──────────────────────┬───────────────────────┘
                                           │ Patient grabs item / timeout
                                           ▼
                    ┌──────────────────────────────────────────────┐
                    │              FetchState.Release              │
                    │   - Tractor beam fades out, hum stops        │
                    │   - Success chime / soft haptic feedback     │
                    │   - Drone returns to Idle / Guiding state    │
                    └──────────────────────────────────────────────┘
```

---

## 3. Component Details & Interactions

### 3.1 `DroneFetchTarget.cs` (`Assets/Scripts/Interactive/Task03/`)
* **Inheritance:** `MonoBehaviour`
* **Attached To:** Any fetchable item (e.g., `CyberFetchBaton`, `ErgonomicInteractiveToy`, apples).
* **Key Properties:**
  - `float minThrowDistance = 2.5f`: Distance from player needed to trigger auto-fetch.
  - `float fetchTriggerDelay = 0.8f`: Grace period after coming to rest or passing the threshold before alerting drone.
  - `bool isBeingFetched = false`: Prevents multiple fetch controllers from latching.
  - `Transform dockPivot`: Optional specific attachment point.
* **Events / Integration:**
  - Subscribes to `XRGrabInteractable.selectExited` (from `ErgonomicGrabInteractable`).
  - Measures launch velocity and landing coordinates relative to the player/drone.
  - If landing distance > 2.5m, notifies `DroneFetchController.Instance.RequestFetch(this)`.

### 3.2 `DroneFetchController.cs` (`Assets/Scripts/Interactive/Task03/`)
* **Inheritance:** `MonoBehaviour`
* **Attached To:** Drone root (`[IC] Drone_Guia_IA_ExoBot` or `DronePetCompanion`).
* **Key Properties:**
  - `DroneFetchState CurrentState`: Current state of the fetch FSM.
  - `Transform tractorBeamAnchor`: Point on bottom of drone where the emitter module is docked.
  - `float flightSpeed = 3.2f`: Cruise velocity during fetch trips.
  - `float hoverHeightAboveTarget = 0.65f`: Altitude maintained directly above the ground target.
  - `float presentationHeight = 1.15f`: Absolute ergonomic presentation altitude above ground.
  - `float presentationDistanceFront = 0.70f`: Distance in front of patient chest.
* **Visual & Audio Subsystems:**
  - `TractorBeamVisual`: Manages a luminous tapered cyan `LineRenderer` + Point Light + Particle System of ascending energy rings/motes.
  - `AudioSource`: Plays `drone_fetch_alert.wav` upon detection and loops `tractor_beam_hum.wav` while beam is engaged.
* **DroneGuiaIA Coordination:**
  - Sets `DroneGuiaIA.sobControleFetch = true` while fetching so the standard patient-tracking locomotion does not conflict with fetch trajectories.
  - Restores `DroneGuiaIA.sobControleFetch = false` once the item is presented and released.

---

## 4. 3D Asset & Audio Specifications

### 4.1 3D Models (Blender 5.1 Headless)
1. **`TractorBeamEmitter.fbx`:**
   - **Scale:** ~0.10m diameter, 0.04m height.
   - **Form:** Futuristic circular emitter ring with central concave focal lens, cyan emissive circuit inlays, dark titanium metallic housing.
   - **Pivot:** Top-center `(0, 0, 0)` for flush mounting to the bottom of the drone chassis.
   - **Target Path:** `Assets/Models/Generated/Task03/TractorBeamEmitter.fbx`.

2. **`CyberFetchBaton.fbx`:**
   - **Scale:** 0.22m length, 0.038m central grip diameter, tapered dual bulbous rubberized ends (0.045m diameter).
   - **Form:** Ergonomic VR throwing baton with ribbed grip textures, dual cyan neon luminescent indicator rings, and durable shock-absorbing end caps.
   - **Pivot:** Exact geometric center of the central grip `(0, 0, 0)` for natural VR holding with HTC Vive / Meta Quest controllers.
   - **Target Path:** `Assets/Models/Generated/Task03/CyberFetchBaton.fbx`.

### 4.2 Procedural Audio Files (Python `wave` / Synth)
1. **`tractor_beam_hum.wav`:**
   - 44.1 kHz, 16-bit Mono, smooth looping magnetic harmonic drone with low-frequency flutter (95 Hz base, 190 Hz second harmonic, subtle phase modulation).
   - Saved in: `Assets/Audio/Generated/Task03/tractor_beam_hum.wav`.
2. **`drone_fetch_alert.wav`:**
   - 44.1 kHz, 16-bit Mono, cheerful two-tone ascending cybernetic chirp (880 Hz -> 1320 Hz with soft bell envelope).
   - Saved in: `Assets/Audio/Generated/Task03/drone_fetch_alert.wav`.

---

## 5. Visual QA Validation Criteria

1. **Alignment:** The `TractorBeamEmitter` must mount precisely at the bottom center of the Drone chassis with zero floating gap or inverted normals.
2. **Beam Geometry:** The conical tractor beam must emanate directly from the emitter lens toward the target object.
3. **No Mesh Clipping:** The fetched item (`CyberFetchBaton`) must levitate ~0.25m - 0.28m below the emitter lens, leaving clean clearance between the item and the drone hull.
4. **Ergonomic Presentation:** In `PresentingAtChestHeight`, the drone and baton must float at ~1.15m above the floor directly in front of the player's chest level.
5. **Desk Presentation:** In `Menu_Principal`, the `CyberFetchBaton` must rest stably on the workstation desk surface without penetrating the wood or hovering in air.

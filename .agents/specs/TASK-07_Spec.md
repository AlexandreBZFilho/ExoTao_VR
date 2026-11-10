# TASK-07 Technical Specification: Antigravity Balloon Dispenser Gun & Floating Popping Physics

**Project:** VR Rehab Gamification (ExoTao VR Rehab)  
**Task ID:** TASK-07  
**Module:** Main Menu Hub / Interactive Playroom  
**Author:** Specialist Subagent  
**Unity Version:** 6000.0.85f2 (Unity 6)  
**XR Interaction Toolkit Version:** 2.6.5  
**Active Render Pipeline:** Built-in Render Pipeline (SRP/URP-agnostic via `MaterialShaderHelper`)

---

## 1. Clinical & Therapeutic Rationale

In neurorehabilitation and robotic exoskeleton gait therapy (such as the ExoTao platform), interactive toys in the waiting/hub environment provide vital preparatory motor activation and positive emotional priming.

### 1.1 Motor & Visual Therapy Benefits:
1. **Trigger Flexion & Grasp Strength:** Squeezing and holding the VR controller trigger to inflate balloons trains finger flexor control, sustained isometric pinch force, and release timing without physical fatigue.
2. **Visual Tracking & Saccadic Eye Movements:** Once released, balloons float upward with gentle antigravity buoyancy and air drag. Tracking them in 3D space stimulates head mobility, cervical range of motion, and visual pursuit.
3. **Upper-Limb Reaching & Bimanual Coordination:** Patients can shoot or reach out with their other hand or laser to pop floating balloons, encouraging active shoulder flexion, elbow extension, and spatial depth judgment.
4. **Instant Gratification & Zero Frustration:** The balloon gun incorporates `ErgonomicGrabInteractable` with Force Grab and Auto-Respawn. If dropped, it glides back to the table within the patient's reachable envelope.

---

## 2. System Architecture & Component Interactions

```
  ┌────────────────────────────────────────────────────────────────────────┐
  │                           Main Menu Playroom                           │
  │                     Desk Surface (y ≈ 0.952m)                          │
  ├────────────────────────────────────────────────────────────────────────┤
  │    BalloonGunDispenser (ErgonomicGrabInteractable)                     │
  │    - Muzzle_Socket (Transform filho na ponta do bico)                  │
  │    - Lateral Helium Canister & Pressure Gauge                          │
  │    - Dynamic Balloon In-Muzzle Preview (Grows 0.05m -> 0.32m)          │
  │    - AudioSource (Stretch Loop & Sfx)                                  │
  └───────────────────┬────────────────────────────────────────────────────┘
                      │ Trigger Activate (Hold) -> Inflate with rubber stretch sound
                      │ Trigger Deactivate (Release) -> Launch Balloon
                      ▼
  ┌────────────────────────────────────────────────────────────────────────┐
  │                      FloatingBalloon (Prefab)                          │
  │  - Rigidbody (Mass: 0.01kg, Drag: 1.2, Custom Antigravity Upward Drift)│
  │  - Teardrop Balloon Mesh + Knot at Base + Hanging String (LineRenderer)│
  │  - Vibrant Multicolor Shaders (Glossy Red, Cyan, Yellow, Magenta, etc.)│
  │  - Collision / Trigger Detector (OnTriggerEnter / OnCollisionEnter)    │
  │  - Pop VFX (Burst Particle System) & High-Impact Pop Audio             │
  └────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Asset Specifications

### 3.1 3D Models (Blender 5.1 Headless)
* **`BalloonGunDispenser.fbx`**:
  - Sci-fi stylized balloon gun designed for VR ergonomics.
  - Overall length: `~0.22m`, height: `~0.16m`, body diameter: `~0.045m`.
  - Anatomic grip tilted at `75°` with hand trigger guard.
  - Tapered nozzle barrel with hollow bore ending at child transform `Muzzle_Socket`.
  - Cylindrical helium bottle on side/rear with metallic bracket and analog pressure gauge.
  - Origin/Pivot: Exactly at the grip center of mass for natural VR hand alignment.
* **`InflatableBalloon.fbx`**:
  - Classic teardrop elipsoid balloon (`0.30m` height, `0.24m` max width).
  - Pinched neck and tied knot at base (`y = 0.0m`).
  - Pivot at knot base (`y = 0.0m`) allowing realistic growth outward from the nozzle.

### 3.2 Shaders & Color Palette
* Materials created via `MaterialShaderHelper` (smoothness `0.85f`, metallic `0.05f` for glossy rubber finish):
  1. `M_Balloon_RubyRed` (`#FF1E40`)
  2. `M_Balloon_CyberCyan` (`#00E5FF`)
  3. `M_Balloon_SunnyYellow` (`#FFD600`)
  4. `M_Balloon_NeonMagenta` (`#FF007F`)
  5. `M_Balloon_LimeGreen` (`#76FF03`)
  6. `M_Balloon_RoyalPurple` (`#9D00FF`)
  7. `M_Balloon_Tangerine` (`#FF6D00`)
* Gun Materials:
  1. `M_BalloonGun_Body` (Matte Tech White `#F0F2F5`, smoothness 0.4)
  2. `M_BalloonGun_Accent` (Vibrant Cyan `#00B4D8`, metallic 0.3)
  3. `M_BalloonGun_Canister` (Chrome / Anodized Blue `#0077B6`, metallic 0.85, smoothness 0.9)
  4. `M_BalloonGun_Nozzle` (Dark Rubber Charcoal `#2B2D42`)

### 3.3 Audio Assets (Procedural Synthesized `.wav`)
1. `Assets/Audio/Generated/Task07/balloon_inflate_stretch.wav`: Dynamic rubber friction stretching loop/pitch-modulated sample.
2. `Assets/Audio/Generated/Task07/balloon_release_whoosh.wav`: Soft pneumatic release whoosh with air puff.
3. `Assets/Audio/Generated/Task07/balloon_pop_loud.wav`: Crisp, energetic, high-frequency balloon burst sound.

---

## 4. Engineering & Script Implementation

### 4.1 `BalloonInflaterGun.cs`
* Derived from `VRRehab.Ergonomics.ErgonomicGrabInteractable`.
* Fields:
  - `Transform muzzleSocket`: Attachment point at the tip of the barrel.
  - `GameObject balloonPrefab`: Reference to `InflatableBalloon.prefab`.
  - `Material[] balloonMaterials`: Array of vibrant rubber materials.
  - `float minScale = 0.05f`, `float maxScale = 0.32f`.
  - `float inflateDuration = 1.4f` (seconds from min to max).
  - `float launchSpeed = 0.75f` (m/s along forward vector).
  - Audio clips and `AudioSource` for stretch sound and release sound.
* Lifecycle:
  - `OnActivated`: Instantiates or activates inflating balloon on `muzzleSocket`. Starts inflation coroutine. Scales smoothly, modulates audio pitch `0.8 -> 1.4` and sends periodic haptic pulses to controller.
  - `OnDeactivated`: Finalizes balloon size, detaches from `muzzleSocket`, activates its `FloatingBalloon` script and physics, applies launch velocity, plays release whoosh sound.
  - Auto-Respawn: Reverts to designated table cradle if dropped or thrown far away.

### 4.2 `FloatingBalloon.cs`
* Component attached to `InflatableBalloon`.
* Physics properties:
  - Rigidbody with `mass = 0.012`, `drag = 1.4`, `angularDrag = 1.8`, `useGravity = false`.
  - Custom Buoyancy: Applies constant upward force (`Vector3.up * 0.18f`) counteracting gravity with gentle ascent (~`0.25 m/s`).
  - Wind Turbulence: Small pseudo-random Perlin perturbation vector to simulate ambient room air currents.
* String Visual (`LineRenderer`):
  - 6-segment hanging string descending from balloon knot (`length ~0.40m`).
  - Soft pendulum / sine oscillation in response to balloon movement.
* Popping Logic:
  - Detects collisions with VR hands, controllers, laser rays, or projectiles.
  - Also pops on direct grip/squeeze.
  - Disables renderer and collider, triggers burst particle explosion (pieces of rubber flying in balloon's color), plays loud pop audio clip, and destroys/recycles after 1.5s.

### 4.3 Scene Setup & Placement (`Task07_SceneAndPrefabBuilder.cs`)
* Scene: `Assets/Scenes/Menu_Principal.unity`.
* Desk position:
  - Table surface at `y ≈ 0.952m`.
  - Position: `(0.50f, 0.965f, 0.38f)`, angled `100°` facing the player.
  - Gun resting in a clean, clearly visible position next to the radio and cassette deck.
* Demo Floating Balloons:
  - Instantiates 3 floating balloons at varied heights (`y = 1.35m`, `1.65m`, `1.90m`) in room with vibrant colors and hanging strings.

---

## 5. Visual QA Validation Criteria

1. **Pistola no Desk (`TASK-07_BalloonGun_Desk.png`):**
   - Gun rests naturally on the table surface without clipping through wood.
   - Helium canister, pressure gauge, grip, and nozzle socket clearly defined.
   - Zero missing/pink shaders.
2. **Balão Sendo Inflado (`TASK-07_Balloon_Inflating.png`):**
   - Close-up showing balloon attached to the muzzle tip expanding outwards.
   - Knot aligned with the nozzle opening.
3. **Balões Flutuando na Sala (`TASK-07_Balloons_Floating_Room.png`):**
   - Wide view of the Menu room showing colorful balloons drifting gracefully at eye level.
   - Hanging strings visible and realistically dangling from knots.
   - Vibrant contrasting colors (red, cyan, yellow, magenta).

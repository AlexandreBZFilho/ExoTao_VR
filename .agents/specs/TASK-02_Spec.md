# TASK-02 Technical Specification: Exoskeleton Ergonomics — Force Grab, Auto-Respawn & Ballistic Aim Assist

**Project:** VR Rehab Gamification (ExoTao VR Rehab)  
**Task ID:** TASK-02  
**Module:** Core / Base Ergonomics  
**Author:** Specialist Subagent  
**Unity Version:** 6000.0.85f2 (Unity 6)  
**XR Interaction Toolkit Version:** 2.6.5  
**Active Render Pipeline:** Built-in Render Pipeline (SRP/URP-agnostic via `MaterialShaderHelper`)

---

## 1. Clinical Context & Ergonomic Rationale

Patients undergoing motor rehabilitation in robotic exoskeletons (e.g., lower-limb gait trainers, pelvic harnesses, or upper-limb assist systems like ExoTao) present distinct physical limitations:
1. **Restricted Trunk Flexion & Postural Stability:** Patients cannot and must not bend at the waist or crouch to retrieve dropped objects from the floor, as doing so compromises pelvic alignment, risks tripping or falling, and violates exoskeleton safety envelopes.
2. **Reduced Grip Strength & Motor Tremor:** Hand coordination, pinch grip stability, and release timing are frequently impaired (post-stroke hemiparesis, spinal cord injury, Parkinson's).
3. **Limited Throwing Velocity:** Joint stiffness or motor weakness restricts ballistic wrist flick and elbow extension, making conventional physics throws sluggish or disappointing.

### Therapeutic Solutions in TASK-02:
* **Assisted Force Grab:** Pulling objects effortlessly from across the room using controller rays (`useForceGrab = true`) with a smooth, predictable attraction trajectory.
* **Intelligent Auto-Respawn:** If an object is dropped, knocked off a desk, or thrown away, it detects ground contact / distance thresholds and smoothly transitions back to its original home dock or table surface after a short grace period (zero floor searching).
* **Ballistic Throw Aim Assist:** Multiplies release momentum by an ergonomic factor (1.45x) and gently steers throwing trajectories toward valid receptors (baskets, hoops, drone target), providing high-satisfaction motor reward.
* **3D Target Aim Reticle:** A futuristic holographic projection under hovered objects that grounds spatial perception and confirms ray targeting without visual ambiguity.

---

## 2. Component Architecture

```
                    ┌──────────────────────────────────────────────┐
                    │               XROrigin Player                │
                    │ Left/Right Controller (XRRayInteractor)     │
                    │   - useForceGrab = true                      │
                    │   - maxRaycastDistance = 30.0m               │
                    └──────────────────────┬───────────────────────┘
                                           │ Raycast Hover / Select
                                           ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        ErgonomicGrabInteractable (XRGrabInteractable)                  │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  • useForceGrab: true                                                                  │
│  • Smooth Attraction Curve during Ray Drag                                             │
│  • Multisensory Hooks: VRFeedbackManager (Hover, Grab, Release, Return)                │
│  • Auto-Respawn Engine:                                                                │
│      - Home Anchor (Vector3 pos, Quaternion rot)                                       │
│      - respawnMinY (floor boundary, e.g. < 0.35m)                                       │
│      - respawnMaxDistance (e.g. > 4.0m from desk)                                      │
│      - respawnDelaySeconds (e.g. 2.5s unheld grace time)                               │
│      - Smooth Elastic Interpolation return back to Home                                │
└───────────────────────┬──────────────────────────────────────────┬─────────────────────┘
                        │ Component                                │ Component
                        ▼                                          ▼
┌──────────────────────────────────────────────┐   ┌──────────────────────────────────────────────┐
│             ThrowAimAssist                   │   │              TargetAimReticle                │
├──────────────────────────────────────────────┤   ├──────────────────────────────────────────────┤
│ • throwVelocityMultiplier (1.45x)            │   │ • Holographic Ring (ReticleTargetRing.fbx)   │
│ • angularTolerance (30° - 40° cone)          │   │ • Projected onto floor / surface below item  │
│ • aimAssistStrength (0.0 to 1.0)             │   │ • Active during XR hover / focus             │
│ • Detects IAimAssistTarget in scene          │   │ • Cyan Emissive Material (#00F0FF)           │
│ • Corrects ballistic parabolic vector        │   │ • Smooth Scale & Emission Fade               │
└──────────────────────────────────────────────┘   └──────────────────────────────────────────────┘
```

### 2.1 `ErgonomicGrabInteractable.cs` (`Assets/Scripts/Interactive/Task02/`)
* **Inheritance:** `UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable`
* **Key Properties:**
  - `bool enableAutoRespawn = true`: Enables auto-recovery of dropped objects.
  - `float respawnDelaySeconds = 2.5f`: Time after release before checking respawn conditions.
  - `float respawnFloorThresholdY = 0.35f`: If object falls below this altitude, respawn is triggered.
  - `float respawnMaxDistance = 5.0f`: If object flies beyond this distance from home, respawn is triggered.
  - `float returnGlideDuration = 0.75f`: Duration of the smooth return glide back to table.
  - `bool smoothForceGrabApproach = true`: Provides smooth acceleration towards the hand instead of harsh snapping.
* **Integration with `VRFeedbackManager`:**
  - Automatically triggers `PlayHover`, `PlayGrab`, `PlayRelease`, and `PlaySuccess` on return.

### 2.2 `ThrowAimAssist.cs` (`Assets/Scripts/Interactive/Task02/`)
* **Inheritance:** `MonoBehaviour`
* **Key Properties:**
  - `float throwVelocityMultiplier = 1.45f`: Ergonomic speed multiplier applied to release velocity.
  - `float aimAssistStrength = 0.65f`: Blending ratio between original throw vector and calculated target vector (0 = raw throw, 1 = perfect laser arc).
  - `float maxTargetAngle = 35.0f`: Conical acceptance angle for detecting user intent toward a target.
  - `float maxAssistRange = 12.0f`: Maximum distance to scan for aim-assist receptors.
* **Target Interface (`IAimAssistTarget`):**
  - Targets register with `ThrowAimAssist` to provide their aim position (e.g. basket hoop center, drone chest zone).

### 2.3 `TargetAimReticle.cs` (`Assets/Scripts/Interactive/Task02/`)
* **Inheritance:** `MonoBehaviour`
* **Visual Presentation:**
  - Instantiates or controls `ReticleTargetRing.fbx` as a ground projection under the target object.
  - Casts downward ray to snap reticle precisely to table/floor surfaces (`LayerMask` excluding the interactable itself).
  - Fades in upon `firstHoverEntered` and smoothly fades out upon `lastHoverExited`.
  - Rotates subtly to provide animated cybernetic feedback.

---

## 3. Metric Scale & Spatial Context

* **VR Scale:** 1 Unity Unit = 1.0 Meter.
* **Reticle Dimensions:**
  - Outer Diameter: `0.25m` (radius `0.125m`)
  - Inner Diameter: `0.20m`
  - Ring Thickness: `0.005m`
* **Desk Placement in `Menu_Principal`:**
  - Desk Object: `full_desk_3 (1)` at `(0.63, 0.00, -0.02)`
  - Surface Height: `Y = 0.85m`
  - Demo Object Anchor: `(0.95, 0.90, 0.10)`
  - Floor Plane: `Y = 0.00m`

---

## 4. Verification & QA Criteria

- **Compilation:** 0 compiler errors (`CSxxxx`).
- **Asset Generation:** `ReticleTargetRing.fbx` properly created via Blender 5.1 in `Assets/Models/Generated/Task02/`.
- **Material Quality:** Cyan emissive material (`#00F0FF`) with zero pink/broken shaders.
- **Visual QA:** 3 screenshots generated in `.agents/visual_qa/`:
  1. `TASK-02_Reticle_Aim.png` (Reticle projection under item on desk/floor)
  2. `TASK-02_ForceGrab_Preview.png` (XR ray interacting with object)
  3. `TASK-02_AutoRespawn_Zone.png` (Item safely rested in home zone)

# TASK-04 Technical Specification: Drone Pet Affective System (Haptic Petting, Poke Reaction, LED Face & Music Dance)

**Project:** VR Rehab Gamification (ExoTao VR Rehab)  
**Task ID:** TASK-04  
**Module:** Drone Pet / Companheiro Robótico IA  
**Author:** Specialist Subagent  
**Unity Version:** 6000.0.85f2 (Unity 6)  
**XR Interaction Toolkit Version:** 2.6.5  
**Active Render Pipeline:** Built-in Render Pipeline (SRP/URP-agnostic via `MaterialShaderHelper`)

---

## 1. Clinical & Therapeutic Rationale

In neurorehabilitation for patients using robotic lower-limb exoskeletons (such as ExoTao), emotional engagement and cognitive stimulation play a crucial role in maintaining motivation, reducing anxiety, and boosting therapy adherence.

### Affective Companion Dynamics:
1. **Biofeedback & Stress Reduction:** Interacting with an empathetic robotic pet provides biofeedback, decreasing cortisol levels and alleviating therapy fatigue during prolonged gait trials.
2. **Encouraging Upper-Limb Range of Motion (ROM):** Petting the drone's head or playfully poking its snout motivates patients to lift, stabilize, and reach with their arms and hands, promoting upper-extremity coordination alongside lower-limb gait retraining.
3. **Multisensory Reward (Haptics, Sound & Visuals):** When the patient strokes the drone's head, the continuous gentle purring vibration in the VR controller combined with floating heart particles and loving LED facial expressions creates an instant dopamine reward loop.
4. **Music Rhythm & Gait Entrainment:** The drone's ability to groove and dance in sync with the scene's ambient music or radio reinforces auditory-motor synchronization, which is widely recognized in neurologic music therapy (NMT) for gait pacing.

---

## 2. System Architecture & State Breakdown

```
                             ┌───────────────────────────────────┐
                             │       Drone Affective State       │
                             │   (Managed by DroneAffectiveSystem)│
                             └─────────────────┬─────────────────┘
                                               │
             ┌─────────────────────────────────┼─────────────────────────────────┐
             │                                 │                                 │
             ▼                                 ▼                                 ▼
   ┌───────────────────┐             ┌───────────────────┐             ┌───────────────────┐
   │    PETTING MODE   │             │     POKE MODE     │             │    DANCING MODE   │
   │  (Head Trigger)   │             │   (Face Trigger)  │             │  (Audio Spectrum) │
   ├───────────────────┤             ├───────────────────┤             ├───────────────────┤
   │- Continuous hand  │             │- Rapid contact /  │             │- FFT on Radio     │
   │  stroke motion    │             │  finger poke      │             │  AudioSource      │
   │- Purring Haptics  │             │- Spring-Damper    │             │- Rhythmic roll /  │
   │  (25 Hz gentle)   │             │  wobble physical  │             │  yaw / bobbing    │
   │- Heart particles  │             │  recoil response  │             │- Musical note LED │
   │- 'drone_purr.wav' │             │- 'drone_boop.wav' │             │  expression       │
   │- '♥‿♥' LED Face   │             │- '@_@' LED Face   │             │  '♪‿♪'            │
   └───────────────────┘             └───────────────────┘             └───────────────────┘
```

---

## 3. Detailed Component Specifications

### 3.1 Drone Affective System (`Assets/Scripts/Interactive/Task04/DroneAffectiveSystem.cs`)
* **Namespace:** `VRRehab.DronePet`
* **Attached To:** Drone Root GameObject (`[IC] Drone_Guia_IA_ExoBot`).
* **Sub-components & Triggers:**
  - `headPettingTrigger`: A `SphereCollider` or `BoxCollider` situated on the top dome of the drone (`localPosition = (0f, 0.22f, 0f)`, `size = (0.32m, 0.16m, 0.32m)`, `isTrigger = true`).
  - `facePokeTrigger`: A `SphereCollider` situated over the frontal lens/visor (`localPosition = (0f, 0.005f, 0.225f)`, `radius = 0.095m`, `isTrigger = true`).
  - `visorRenderer`: Dedicated `MeshRenderer` attached to `Visor_LED_Frontal`.
  - `heartParticleSystem`: Configured procedural particle emitter releasing floating holographic hearts.
  - `audioSource`: Dedicated 3D `AudioSource` for vocal pet reactions (`drone_purr.wav`, `drone_boop.wav`, `drone_giggle.wav`, `drone_dance_groove.wav`).
* **Runtime Behaviors:**
  - **Petting Logic:**
    - Checks hand presence in `headPettingTrigger`.
    - Tracks hand displacement delta per frame (`velocity > 0.04m/s`).
    - Dispatches haptic pulses via `XRBaseController.SendHapticImpulse(0.25f, 0.05f)` at 20-30Hz intervals.
    - Emits heart particles with upward buoyancy and soft radial spread.
    - Transitions visor expression to `LED_Expression_Love.png` (`♥‿♥`).
  - **Poke / Boop Logic:**
    - Triggered upon rapid hand contact with `facePokeTrigger`.
    - Activates an underdamped spring-mass harmonic response on `transform` or `Modelo_3D_ExoBot`:
      $$z(t) = -Z_{max} \cdot e^{-\zeta \omega_n t} \cos(\omega_d t)$$
    - Plays `drone_boop.wav` followed by a gentle cheerful giggle.
    - Transitions visor expression to `LED_Expression_Poked.png` (`@_@`) for 2.2 seconds.
  - **Music Dance & Beat Tracking:**
    - Obtains audio samples via `musicSource.GetSpectrumData(samples, 0, FFTWindow.BlackmanHarris)`.
    - Accumulates energy in low-frequency bins (60 Hz - 250 Hz) for beat detection.
    - Drives rhythmic lateral tilt (roll angle $\pm 5^\circ$) and vertical bounce ($y \pm 0.03m$).
    - Sets visor expression to `LED_Expression_Dance.png` (`♪‿♪`) when music energy exceeds threshold.

---

## 4. Asset Generation Breakdown

### 4.1 Cybernetic LED Textures (`Assets/Textures/Generated/Task04/`)
High-contrast 512x512 PNG textures featuring cybernetic dot-matrix LED elements on pure dark backing:
1. `LED_Expression_Normal.png`: Friendly calm robot eyes with subtle cyber arches (`•‿•`).
2. `LED_Expression_Happy.png`: Cheerful curved LED arches with cheek blushes (`^‿^`).
3. `LED_Expression_Love.png`: Glowing heart eyes with radiant warmth (`♥‿♥`).
4. `LED_Expression_Poked.png`: Dizzy spiral / concentric ring eyes (`@_@`).
5. `LED_Expression_Dance.png`: Musical note eyes and happy upbeat smile (`♪‿♪`).

### 4.2 Procedural Audio SFX (`Assets/Audio/Generated/Task04/`)
Generated via mathematical wave synthesis (Python `numpy` + `wave`):
1. `drone_purr.wav`: Harmonic low-frequency frequency-modulated purring drone motor (45Hz carrier modulated with 18Hz tremolo and warm harmonics).
2. `drone_boop.wav`: Frequency sweep chirp downward from 880Hz to 320Hz with soft damping and rubber elasticity.
3. `drone_giggle.wav`: High-pitch harmonic arpeggio chuckles (520Hz, 660Hz, 784Hz, 1046Hz) with playful vibrato.
4. `drone_dance_groove.wav`: Syncopated rhythmic sci-fi blip beat suitable for companion groove.

---

## 5. Visual QA Verification Plan

Three dedicated high-resolution (1920x1080) camera shots will be rendered to `.agents/visual_qa/`:
1. `TASK-04_Drone_Visor_Happy.png`: Macro close-up directly in front of the drone's face, displaying the LED visor illuminated with cyan glow, crisp dot matrix pattern, and dark bezel frame.
2. `TASK-04_Drone_Petting_Feedback.png`: Medium perspective showing the drone receiving head petting, active floating heart particles ascending from the head dome, and the `♥‿♥` love face.
3. `TASK-04_Drone_Dancing_Pose.png`: Contextual perspective in the Menu scene showing the drone positioned next to the interactive desk and radio, tilting dynamically to music with dance expression `♪‿♪`.

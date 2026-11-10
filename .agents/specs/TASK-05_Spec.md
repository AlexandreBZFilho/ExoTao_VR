# TASK-05 Technical Specification: Drone Milestone Celebration System (Acrobatic Pirouette, Confetti Cannons, Fanfare & 3D Motivational Speech Bubble)

**Project:** VR Rehab Gamification (ExoTao VR Rehab - ReRobLab / IC)  
**Task ID:** TASK-05  
**Module:** Drone Pet / Companheiro Robótico IA  
**Author:** Specialist Subagent  
**Unity Version:** 6000.0.85f2 (Unity 6)  
**XR Interaction Toolkit Version:** 2.6.5  
**Active Render Pipeline:** Built-in Render Pipeline (SRP/URP-agnostic via `MaterialShaderHelper`)

---

## 1. Clinical & Therapeutic Rationale

In neurorehabilitation for patients operating lower-limb robotic exoskeletons (such as the ExoTao platform), extrinsic biofeedback and gamified milestone rewards are pivotal in driving neuroplasticity and combatting therapeutic fatigue.

### Celebration Dynamics:
1. **Dopamine Conditioning & Milestone Reinforcement:** Incremental motor goals (e.g., reaching 15, 25, or 50 continuous steps, or sustaining gait symmetry $> 80\%$) trigger immediate auditory, visual, and narrative celebrations. This positive reinforcement loop triggers dopamine release, promoting motor cortex reorganization.
2. **Clinical Encouragement & Self-Efficacy:** Rather than displaying cold numeric readouts alone, the Drone Pet speaks directly to the patient via a readable 3D speech bubble with scientifically backed, positive encouragement in Portuguese, reinforcing resilience, posture, and effort.
3. **Multisensory Climax:** Combining a triumphant fanfare, dual confetti cannon particle bursts, and an acrobatic aerial pirouette turns exhausting rehabilitation trials into joyful, engaging achievements.
4. **Adaptive Context Sensitivity:** Phrases are dynamically categorized by milestone type (`passos`, `simetria`, `distancia`, `geral`) so that the drone's praise accurately reflects whether the patient achieved higher step count, improved bilateral symmetry, or traversed greater distance.

---

## 2. System Architecture & State Breakdown

```
                             ┌───────────────────────────────────┐
                             │    Kinematic & Telemetry Inputs   │
                             │(ReceptorUDP / TelemetriaClinica)  │
                             └─────────────────┬─────────────────┘
                                               │
                                               ▼
                             ┌───────────────────────────────────┐
                             │       Milestone Evaluator         │
                             │  (DroneGuiaIA / Celebration)      │
                             └─────────────────┬─────────────────┘
                                               │ Trigger Milestone
                                               ▼
                             ┌───────────────────────────────────┐
                             │    DroneMilestoneCelebration      │
                             └─────────────────┬─────────────────┘
                                               │
              ┌────────────────────────────────┼────────────────────────────────┐
              │                                │                                │
              ▼                                ▼                                ▼
    ┌───────────────────┐            ┌───────────────────┐            ┌───────────────────┐
    │    CHOREOGRAPHY   │            │  CONFETTI CANNONS │            │  3D SPEECH BUBBLE │
    │   & SFX FANFARE   │            │   (DUAL SYSTEMS)  │            │  (MOTIVATION JSON)│
    ├───────────────────┤            ├───────────────────┤            ├───────────────────┤
    │- 360° Acrobatic   │            │- Left & Right     │            │- Auto-Billboard   │
    │  Pirouette        │            │  Angled Emitters  │            │  Facing Patient   │
    │- Vertical Swoop   │            │- Multicolor quads │            │- Pick from 50 PT  │
    │  Harmonic Motion  │            │  with drag & roll │            │  phrases by tag   │
    │- 'fanfare_triumph'│            │- 'confetti_pop'   │            │- High-contrast UI │
    │- LED Face: Happy  │            │- Gravity decay    │            │  Frame & TMP Font │
    └───────────────────┘            └───────────────────┘            └───────────────────┘
```

---

## 3. Detailed Component Specifications

### 3.1 Milestone Celebration Controller (`Assets/Scripts/Interactive/Task05/DroneMilestoneCelebration.cs`)
* **Namespace:** `VRRehab.DronePet`
* **Target GameObject:** Drone Pet root (`[IC] Drone_Guia_IA_ExoBot`).
* **Key Fields:**
  - `droneGuia`: Reference to `DroneGuiaIA`.
  - `telemetria`: Reference to `TelemetriaClinica`.
  - `affectiveSystem`: Reference to `DroneAffectiveSystem` (sets facial expression to `Happy` during celebration).
  - `speechBubbleRoot`: `GameObject` holding the speech bubble Canvas.
  - `speechText`: `TextMeshProUGUI` component displaying the motivational text.
  - `bubbleCanvas`: World-space Canvas positioned at `(0f, 0.48f, 0f)` above drone.
  - `leftConfettiCannon`, `rightConfettiCannon`: Dual `ParticleSystem` components positioned on the drone's lateral thrusters/shoulders.
  - `celebrationAudioSource`: Dedicated 3D `AudioSource` with spatial blend for fanfare and cannon pops.
  - `sfxFanfare`: `AudioClip` for `fanfare_triumph.wav`.
  - `sfxConfettiPop`: `AudioClip` for `confetti_cannon_pop.wav`.
* **Milestone Triggers:**
  - `Passos`: Evaluated every `passosPorMeta` (default 15/25 steps).
  - `Simetria`: Evaluated when `indiceSimetriaMarcha >= 80%` and steps $\ge 10$ after a period of asymmetric alert.
  - `Distancia`: Evaluated every milestone threshold (e.g., 25m, 50m, 100m).
  - `Manual`: Callable via public method `TriggerCelebration(CelebrationType type)`.
* **Celebration Sequencing (Duration ~3.8s):**
  - **$t = 0.0s$:** Play `confetti_cannon_pop.wav`, burst left and right confetti systems (35 particles each), start `fanfare_triumph.wav`, switch drone LED expression to `Happy`, activate speech bubble.
  - **$0.0s < t < 2.0s$:** Smooth acrobatic 360° pirouette:
    $$\theta_{yaw}(t) = 360^\circ \cdot \sin^2\left(\frac{\pi t}{4}\right)$$
    Vertical swoop bobbing:
    $$y_{offset}(t) = 0.12 \cdot \sin\left(\frac{\pi t}{2}\right)$$
  - **$2.0s < t < 3.8s$:** Drone gently settles back to standard hovering orientation facing the patient, speech bubble remains visible for easy reading.
  - **$t \ge 3.8s$:** Speech bubble smoothly scales down/fades out, drone transitions back to `GuiandoMarcha` or `AguardandoPaciente`.

### 3.2 3D Motivational Speech Bubble
* **Hierarchy:**
  - `SpeechBubble_Anchor` (Child of Drone Root, localPos `(0f, 0.48f, 0f)`)
    - `Bubble_Canvas` (Canvas World Space, size $500 \times 240$, scale `0.0012, 0.0012, 0.0012`)
      - `Panel_Background` (Rounded Sci-Fi bubble background with cyan/white border)
      - `Pointer_Tail` (Triangular pointer pointing downward to the drone's head)
      - `Txt_CategoryTag` (Small badge showing "[META DE PASSOS]", "[SIMETRIA EXCELENTE]", etc.)
      - `Txt_MotivationalSpeech` (TextMeshProUGUI, size 26-30pt, centered, bright white with subtle shadow, Portuguese characters UTF-8)
* **Auto-Billboard Algorithm:**
  - Ensures the speech bubble always faces the player headset/camera without being upside-down or horizontally inverted:
    $$\vec{V}_{cam \to bubble} = \vec{P}_{bubble} - \vec{P}_{headset}$$
    $$\mathbf{R}_{billboard} = \text{Quaternion.LookRotation}(\vec{V}_{cam \to bubble}, \vec{U}_{world})$$

### 3.3 Motivational Phrases Bank (`Assets/Resources/DroneEncouragementPhrases.json`)
* Structure:
  ```json
  {
    "passos": [ ... 15 phrases ... ],
    "simetria": [ ... 15 phrases ... ],
    "distancia": [ ... 10 phrases ... ],
    "geral": [ ... 10 phrases ... ]
  }
  ```
* Total: Exactly 50 clinically sound, uplifting phrases in Brazilian Portuguese designed for neurorehab patients.

### 3.4 Dual Confetti Cannon Particles
* Located at:
  - Left Thruster: `(-0.24f, 0.08f, 0f)`, rotated `(-35°, -25°, 0°)`
  - Right Thruster: `(+0.24f, 0.08f, 0f)`, rotated `(-35°, 25°, 0°)`
* Particle Properties:
  - Simulation Space: `World`
  - Shape: `Cone`, angle $18^\circ$, radius $0.03m$
  - Particle Count: 35 burst per cannon
  - Colors: Multicolor palette (cyan, gold, emerald green, magenta, coral, electric blue)
  - Physics: Upward impulse $2.8m/s$, gravity $1.8m/s^2$, angular rotation velocity $180^\circ/s$ with air drag $0.4$.

---

## 4. Visual QA & Verification Metrics

1. **TASK-05_Celebration_Pirouette.png:**
   - Drone mid-spin tilted dynamically in air.
   - Dual confetti particle bursts visible in flight.
2. **TASK-05_Celebration_SpeechBubble.png:**
   - High-resolution close-up of 3D speech bubble facing camera.
   - Verified that TextMeshPro font is crisp, letters are NOT mirrored/inverted, and text fits within panel boundaries.
3. **TASK-05_Confetti_Explosion.png:**
   - Wide shot showing celebration particles dispersing around the drone in the scene.

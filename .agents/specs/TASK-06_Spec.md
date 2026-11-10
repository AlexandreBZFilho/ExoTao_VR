# TASK-06 Technical Specification: Radio Cassette Deck System with Magnetic Sockets & Eject Mechanics

**Project:** VR Rehab Gamification (ExoTao VR Rehab)  
**Task ID:** TASK-06  
**Module:** Main Menu Hub / Interactive Playroom  
**Author:** Specialist Subagent  
**Unity Version:** 6000.0.85f2 (Unity 6)  
**XR Interaction Toolkit Version:** 2.6.5  
**Active Render Pipeline:** Built-in Render Pipeline (SRP/URP-agnostic via `MaterialShaderHelper`)

---

## 1. Clinical & Therapeutic Rationale

In neurorehabilitation and robotic gait training (such as the ExoTao exoskeleton platform), patients spend crucial preparation and debriefing time in the Main Menu Hub. 

### Audio-Tactile Engagement & Fine Motor Skills:
1. **Upper-Limb Motor Precision & Coordination:** Picking up, inspecting, orienting, and inserting physical cassette tapes into an angled deck slot provides engaging wrist-pronation, finger grasp, and depth-perception practice in a zero-stress, gamified environment.
2. **Empowerment through Ambient Agency:** Patients have direct agency over their acoustic environment. Selecting a cassette allows them to tailor tempo and vibe (from calming acoustic melodies to energizing rhythmic beats), enhancing motivation and decreasing therapeutic fatigue.
3. **Ergonomic Safety Envelope:** Incorporating `ErgonomicGrabInteractable` guarantees that if a cassette slips or is accidentally knocked off the table, it automatically glides back to its assigned slot on the table organizer rack. The patient never needs to strain or bend down.

---

## 2. System Architecture & Component Interactions

```
  ┌────────────────────────────────────────────────────────────────────────┐
  │                        Main Menu Desk Surface                          │
  │                  Position: (0.66, 0.952, 0.36)                         │
  ├───────────────────────────────────┬────────────────────────────────────┤
  │       [IC] Radio_Menu_3D          │     CassetteDeckSlot (Task 06)     │
  │  - Master AudioSource 3D          │  - XRSocketInteractor (Magnetic)   │
  │  - Volume & Spatial Rolloff       │  - CassetteEjectButton (Spring)    │
  │  - Illuminated Analog Dial        │  - Sfx: Insert / Eject             │
  └─────────────────▲─────────────────┴──────────────────┬─────────────────┘
                    │ Audio Track Swap & Spool Command    │ Magnetic Snap
                    │                                     │
                    │         ┌───────────────────────────▼────────────┐
                    └─────────┤   MusicCassetteTape (Ergonomic)        │
                              │   - Spool_Left / Spool_Right Rotation  │
                              │   - Metadata (Title, Genre, Track)     │
                              │   - Auto-Respawn to Rack               │
                              └────────────────────────────────────────┘
```

---

## 3. Cassette Tape Specifications & Catalog

### 3.1 Dimensions & Metric Proportions
* Standard Compact Audio Cassette scaled 1:1 for VR:
  - Width: `0.100m` (10 cm)
  - Height: `0.064m` (6.4 cm)
  - Thickness: `0.012m` (1.2 cm)
  - Spool Spacing: `0.042m` between left and right reel axles.
  - Spool Teeth: 6-pointed star/cog gear visible through clear window.

### 3.2 Four Thematic Tape Variants

| ID | Title | Genre / Mood | Shell Color / Accent | Audio Clip Source |
| :--- | :--- | :--- | :--- | :--- |
| **Tape 01** | *Cyber Horizon* | Sci-Fi Synthwave / Motivação | Neon Cyan / Deep Charcoal | `Assets/Resources/Audio/Music_SciFi_Menu.mp3` |
| **Tape 02** | *Luna Cantina* | Cantina Latina / Ritmo & Groove | Warm Gold / Mahogany Bronze | `Assets/FloatGrids/Demo/Music/Construyendo en el Espacio - Luna Cantina.mp3` |
| **Tape 03** | *Neon Sunset* | Lo-Fi Chillhop / Foco & Calma | Synth Magenta / Purple Twilight | `Assets/Audio/Generated/Task06/lofi_sunset_groove.wav` |
| **Tape 04** | *Emerald Zen* | Nature Ambient / Respiração | Emerald Green / Pearl Silver | `Assets/Audio/Generated/Task06/ambient_zen_flow.wav` |

---

## 4. Mechanical & Physics Design

### 4.1 Magnetic Socket (`RadioCassetteSocket.cs`)
* Based on `XRSocketInteractor` (XRI 2.6.5):
  - `socketActive = true` by default.
  - Custom `attachTransform` located inside the deck slot inclined at 15° for natural viewing.
  - Filter: checks for `MusicCassetteTape` component.
  - Snap feedback: plays `cassette_insert_click.wav`, triggers haptic impulse on controller, locks Rigidbody as kinematic while socketed.
  - Informs `RadioMenu3D` to crossfade to the cassette's track.

### 4.2 Mechanical Eject Button (`CassetteEjectButton.cs`)
* Spring-loaded physical button on the front of the deck:
  - Depresses 8mm inwards when pressed via Raycast / Direct Hand Poke.
  - Spring-damper return animation (`Mathf.SmoothDamp`).
  - Ejection routine:
    1. Plays `cassette_eject_snap.wav`.
    2. Socket interactor releases tape (`interactionManager.SelectExit`).
    3. Tape's Rigidbody switches to dynamic and receives an outward impulse (`0.65 m/s` forward-upward) landing smoothly on the table tray.
    4. Radio fades back to idle / default background audio.

### 4.3 Spool Rotation Controller (`MusicCassetteTape.cs`)
* Contains child transforms `Spool_Left` and `Spool_Right`.
* While tape is playing inside the deck (`isPlaying == true`):
  - Rotates both spools continuously around their local Z-axis (e.g. `180 deg/sec`).
  - Subtle speed differential simulating tape transfer from supply spool to take-up spool.
* Stops smoothly when paused or ejected.

---

## 5. Visual QA Validation Requirements

1. **Table Alignment & Grounding:**
   - Cassette rack and deck must be flush with the desk surface (`y = 0.952m`).
   - Tapes resting in the rack must not clip through the wood or float in mid-air.
2. **Material & Shader Integrity:**
   - Translucent acrylic window must use a proper transparent shader without alpha sorting artifacts.
   - Distinctive, readable labels for all 4 cassette themes.
   - 0 magenta / broken shader artifacts.
3. **Screenshots Required (1920x1080):**
   - `TASK-06_Radio_Deck_Overview.png`: Wide view showing existing retro radio, adjacent deck unit, and tape rack.
   - `TASK-06_Cassette_Tapes_Closeup.png`: High-detail close-up of the 4 tapes in the organizer rack.
   - `TASK-06_Tape_Inserted_Slot.png`: Tape locked into the deck slot with visible rotating spools and eject button.

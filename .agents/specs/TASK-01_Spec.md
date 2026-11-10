# TASK-01 Technical Specification: Multisensory Feedback & Visual QA Capture Utility

**Project:** VR Rehab Gamification (ExoTao VR Rehab)  
**Task ID:** TASK-01  
**Module:** Core / Base Infrastructure  
**Author:** Specialist Subagent  
**Unity Version:** 6000.0.85f2 (Unity 6)  
**XR Interaction Toolkit Version:** 2.6.5  
**Active Render Pipeline:** Built-in Render Pipeline (Fallback/Compatible with URP 14.0.12)

---

## 1. Overview & Objectives

TASK-01 establishes the foundational sensory and diagnostic layer for the entire project. It guarantees:
1. **Material and Shader Compatibility (`MaterialShaderHelper`):** Zero broken/magenta shaders across different render pipelines (Built-in standard vs URP lit).
2. **Multisensory Feedback Architecture (`VRFeedbackManager`):** Centralized 3D spatialized audio pooling (16 `AudioSource` nodes) combined with XR controller haptics (`SendHapticImpulse`) and pitch randomization.
3. **Ergonomic Visual Interaction (`InteractableHighlightFeedback`):** Elastic scale punch and GPU-friendly `MaterialPropertyBlock` emission highlights on XRI hover and selection events.
4. **Automated Visual QA Engine (`VisualQACaptureEditor`):** High-resolution (1920x1080) programmatic camera capture utility capable of framing bounding boxes, designated viewpoints, and persisting screenshots to `.agents/visual_qa/`.

---

## 2. Component Architecture

### 2.1 `MaterialShaderHelper.cs` (`Assets/Scripts/Interactive/Core/`)
* **Namespace:** `VRRehab.Core`
* **Responsibilities:**
  - Detect active render pipeline dynamically:
    - Checks `GraphicsSettings.currentRenderPipeline` and `QualitySettings.renderPipeline`.
    - If null -> Built-in Render Pipeline.
    - If contains "Universal" / "URP" -> Universal Render Pipeline.
  - Provide safe shader references:
    - Standard Lit: `Standard` (Built-in) or `Universal Render Pipeline/Lit` (URP).
    - Unlit: `Unlit/Color` (Built-in) or `Universal Render Pipeline/Unlit` (URP).
    - Transparent: Sets appropriate blend modes (`_Mode`, `_SrcBlend`, `_DstBlend`, `_ZWrite`).
  - Provide creation helpers:
    - `CreateLitMaterial(string name, Color baseColor, float smoothness, float metallic)`
    - `CreateEmissiveMaterial(string name, Color baseColor, Color emissionColor, float intensity)`
    - `EnsureValidShader(Material mat)` to repair any missing or broken shaders in runtime/editor.

### 2.2 `VRFeedbackManager.cs` (`Assets/Scripts/Interactive/Core/`)
* **Namespace:** `VRRehab.Core`
* **Pattern:** Singleton with DontDestroyOnLoad (when in Play mode) or Editor fallback.
* **Audio Pool:**
  - 16 pre-allocated `AudioSource` instances parented to `VRFeedbackManager`.
  - Configured with `spatialBlend = 1.0f` (full 3D), `minDistance = 0.5f`, `maxDistance = 25.0f`, `rolloffMode = AudioRolloffMode.Linear`.
  - Recycled using round-robin / free-slot pooling.
  - Pitch randomized per trigger within `[pitchMin, pitchMax]` (default `0.93f` to `1.07f`) to prevent acoustic fatigue.
* **XR Haptics Support:**
  - Overloads for `XRBaseControllerInteractor` and `XRBaseInteractable`.
  - Safely extracts `XRBaseController` and invokes `controller.SendHapticImpulse(amplitude, duration)`.
  - Handles null interactors gracefully without logging exceptions.
* **Preset Feedback Types:**
  - Hover: `PlayHover(Vector3 pos, XRBaseControllerInteractor interactor = null)`
  - Grab / Select: `PlayGrab(Vector3 pos, XRBaseControllerInteractor interactor = null)`
  - Release: `PlayRelease(Vector3 pos)`
  - Mech Click: `PlayButtonClick(Vector3 pos, XRBaseControllerInteractor interactor = null)`
  - Socket Snap: `PlaySocketSnap(Vector3 pos, XRBaseControllerInteractor interactor = null)`
  - Success Chime: `PlaySuccess(Vector3 pos)`

### 2.3 `InteractableHighlightFeedback.cs` (`Assets/Scripts/Interactive/Core/`)
* **Namespace:** `VRRehab.Core`
* **Inheritance:** `MonoBehaviour`
* **Events Integration:**
  - Hooked to `XRBaseInteractable.firstHoverEntered`, `lastHoverExited`, `selectEntered`, `selectExited`.
* **Visual Punch:**
  - Elastic scale punch on hover (e.g. `originalScale * 1.06f`) using `Mathf.SmoothDamp` or lerp coroutine.
  - Returns smoothly to `originalScale` on `lastHoverExited`.
* **Emission Highlight:**
  - Uses `MaterialPropertyBlock` applied to cached `Renderer` components to avoid material instancing.
  - Targets `_EmissionColor` (or standard diffuse tint if unlit).
  - Enables `_EMISSION` keyword if needed.
* **Audio & Haptics Trigger:**
  - Dispatches calls to `VRFeedbackManager.Instance` on hover and select.

### 2.4 `VisualQACaptureEditor.cs` (`Assets/Editor/VisualQA/`)
* **Namespace:** `VRRehab.EditorTools`
* **Capabilities:**
  - Dedicated inspection camera with adjustable FOV, high-resolution `RenderTexture` (1920x1080, 24-bit depth, sRGB, anti-aliasing = 8).
  - Automatically calculates camera distance and position to frame arbitrary `Renderer.bounds` or `GameObject` hierarchies.
  - Includes helper lighting to guarantee inspection visibility even in dark or unbaked corners.
  - Methods:
    - `CaptureActiveScene(string outputPath, Vector3 pos, Vector3 targetLookAt)`
    - `CaptureBounds(string outputPath, Bounds targetBounds, Vector3 angleOffset)`
    - `CaptureAllTestViews()`: generates `.agents/visual_qa/TASK-01_Menu_Overview.png`, `TASK-01_Menu_DeskZone.png`, and `TASK-01_Floresta_Overview.png`.
  - Exposed via `[MenuItem("VRRehab/VisualQA/Capture All Test Views")]` and static command line evaluation.

---

## 3. Metric Scale & Spatial Context

* **VR Scale:** 1 Unity Unit = 1.0 Meter.
* **Main Menu Hub Coordinates:**
  - Player Origin: `(4.50, 0.00, 2.31)`
  - Eye Height: `~1.67m`
  - Desk / Workstation Zone: `(0.66, 0.95, 0.36)` and `(4.50, 0.95, 7.40)`
  - Radio Deck Anchor: `(0.66, 0.95, 0.36)`
  - Drone Hologram Station: `(7.05, 0.00, 1.85)`
* **Floresta e Rio Coordinates:**
  - Pathway origin: `(0, 0, 0)` extending through natural terrain with riverbanks and interactive trees.

---

## 4. Verification & QA Criteria
- Zero compiler errors (`CSxxxx`).
- All 6 generated audio files present in `Assets/Audio/Generated/Core/` and registered by Unity.
- Screenshots generated in `.agents/visual_qa/` with zero missing shaders (no bright pink/magenta).
- Materials adaptively function under both Built-in and URP pipelines.

# 🦿 ExoTao VR Rehab - Virtual Reality Module

Virtual Reality platform developed in Unity to assist in lower limb motor rehabilitation using the **ExoTao** robotic exoskeleton (ReRobLab).

## 🎯 Project Objective
Create a gamified immersive environment where the patient's physical movements in the exoskeleton (knee and hip) are translated in real-time to a virtual avatar. The project utilizes **Gait-Driven Locomotion**, allowing the user to explore the virtual environment by walking on a treadmill, with synchronized visual feedback.

## 🛠️ Technologies Used
* **Engine:** Unity 2022.3 LTS (C#)
* **VR Hardware:** HTC Vive & Vive Trackers (OpenXR / SteamVR)
* **Communication:** UDP Protocol (Local Network)
* **Physical Control/Simulation:** MATLAB (ExoTao) / Python (Test Simulator)

## 🏗️ System Architecture
The system operates on a Client-Server architecture via local network:
1. The robot controller (or the Python simulator script) reads the joint angles and sends UDP packets.
2. Unity (C#) runs a background thread listening on the specified port.
3. The angles are applied to the avatar's forward/inverse kinematics, and camera displacement is calculated based on hip flexion.

## 🚀 How to run the project locally (Simulation Mode)

To test the communication without the physical robot:

1. Clone this repository.
2. Open the project in **Unity 2022.3 LTS**.
3. Open the main scene and press **Play** in the editor (Unity will start listening on port 5005).
4. In a terminal, run the Python simulator to start sending joint data:
   ```bash
   python simulador_robo.py
   ```
5. Watch the 3D model react in real-time to the generated angles!
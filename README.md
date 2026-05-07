# 🦿 ExoTao VR Rehab - Módulo de Realidade Virtual

Plataforma de Realidade Virtual desenvolvida na Unity para auxiliar na reabilitação motora de membros inferiores utilizando o exoesqueleto robótico **ExoTao** (ReRobLab).

## 🎯 Objetivo do Projeto
Criar um ambiente imersivo gamificado onde os movimentos físicos do paciente no exoesqueleto (joelho e quadril) são traduzidos em tempo real para um avatar virtual. O projeto utiliza **Gait-Driven Locomotion**, permitindo que o usuário explore o cenário virtual caminhando em uma esteira, com feedback visual sincronizado.

## 🛠️ Tecnologias Utilizadas
* **Engine:** Unity 2022.3 LTS (C#)
* **Hardware VR:** HTC Vive & Vive Trackers (OpenXR / SteamVR)
* **Comunicação:** Protocolo UDP (Rede Local)
* **Controle Físico/Simulação:** MATLAB (ExoTao) / Python (Simulador de testes)

## 🏗️ Arquitetura do Sistema
O sistema opera em uma arquitetura Cliente-Servidor via rede local:
1. O controlador do robô (ou o script simulador em Python) lê os ângulos das articulações e envia pacotes UDP.
2. A Unity (C#) roda uma thread em *background* escutando a porta estipulada.
3. Os ângulos são aplicados à cinemática inversa/direta do avatar, e o deslocamento da câmera é calculado com base na flexão do quadril.

## 🚀 Como rodar o projeto localmente (Modo Simulação)

Para testar a comunicação sem o robô físico:

1. Clone este repositório.
2. Abra o projeto na **Unity 2022.3 LTS**.
3. Abra a cena principal e dê o **Play** no editor (A Unity começará a escutar a porta 5005).
4. Em um terminal, execute o simulador Python para começar a enviar os dados articulares:
   ```bash
   python simulador_robo.py
5. Observe o modelo 3D reagindo em tempo real aos ângulos gerados!
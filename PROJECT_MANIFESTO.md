# 🚀 ExoTao VR Rehab - Manifesto de Desenvolvimento & Diretrizes de Engenharia

Este documento define os princípios de engenharia, visão de produto e fluxo de trabalho autônomo entre o **Desenvolvedor / Pesquisador** e o **Agente de IA (Antigravity)** no laboratório **ReRobLab**.

---

## 🎯 1. Filosofia de Trabalho: Autonomia, Pesquisa e Excelência
* **Mentalidade de Equipe Proativa:** Não esperamos problemas aparecerem. Pesquisamos ativamente as ferramentas, pacotes e tecnologias mais modernas do ecossistema (Unity, VR/XR, Visão Computacional, MCPs).
* **Pesquisa Antes da Implementação:** Sempre que formos adicionar um recurso novo (modelagem 3D, áudio, cinemática, shaders), buscamos a solução de ponta da indústria (como fizemos com o *Unity CLI*, *Unity Pipeline* e *Render Graph*).
* **Automação Contínua:** Tarefas manuais repetitivas devem ser automatizadas via scripts de Editor (`[MenuItem]`, `[CliCommand]`), ferramentas Python e CLI.

---

## 🛠️ 2. Integrações de Ferramentas & Ecossistema de IA (MCP / CLI)
* **Unity CLI & Unity Pipeline:** Controle total do Editor sem intervenção manual no mouse — compilações, inspeção de hierarquias, captura de viewport e REPL em C#.
* **Blender MCP & Modelagem 3D:** Quando precisarmos de modelos 3D customizados (ex: novo exoesqueleto, adereços médicos, cenários), conectamos ao Blender via MCP/Python (`bpy`) ou repositórios abertos (Sketchfab, Poly Haven).
* **Visão Computacional (MediaPipe Pose):** Rastreamento de corpo inteiro por câmera do celular/webcam sem sensores caros adicionais.
* **Telemetria Clínica Automatizada:** Toda sessão gera logs científicos em CSV e gráficos de alta resolução para alimentar diretamente os artigos e relatórios da Iniciação Científica.

---

## 🎮 3. Pilares de Game Feel & Conforto em VR (90 Hz)
1. **Locomoção Orgânica (Sem "Skating"):** A marcha do avatar deve ter cadência, impulso e acomodação de passos discretos, eliminando a cinetose (enjoo vestibular).
2. **Biofeedback Audiovisual Rico ("Juice"):** Efeitos de partículas ao pisar, servos mecânicos com som responsivo e o Drone Guia acompanhando a cadência.
3. **Embodiment Total:** O corpo virtual deve responder com suavidade (`Quaternion.Slerp`), balanço contralateral de braços e simetria postural.

---

## 💻 4. Diagnóstico de Hardware da Estação do Laboratório
* **CPU:** Intel Core i7-7700 @ 3.60GHz (4c/8t)
* **GPU:** NVIDIA GeForce GTX 1070 (8GB VRAM) - Adequada para VR
* **RAM Atual:** 8 GB *(Gargalo Crítico)* -> **Meta:** 32 GB DDR4
* **Armazenamento Atual:** HDD Mecânico SATA (Seagate BarraCuda 1TB) *(Gargalo Crítico de IOPS)* -> **Meta:** SSD SATA ou NVMe

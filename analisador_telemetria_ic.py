import os
import glob
import csv
import sys

def analisar_ultima_sessao():
    pasta_relatorios = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Relatorios_Clinicos")
    if not os.path.exists(pasta_relatorios):
        print(f"[!] Pasta '{pasta_relatorios}' ainda não existe. Rode uma sessão na Unity primeiro!")
        return

    arquivos_csv = sorted(glob.glob(os.path.join(pasta_relatorios, "*_Telemetria.csv")), key=os.path.getmtime)
    if not arquivos_csv:
        print("[!] Nenhum arquivo CSV de telemetria encontrado em Relatorios_Clinicos/")
        return

    ultimo_csv = arquivos_csv[-1]
    print("=" * 72)
    print(f" ANALISADOR BIOMECÂNICO IC - ReRobLab (ExoTao VR Rehab)")
    print(f" Arquivo: {os.path.basename(ultimo_csv)}")
    print("=" * 72)

    tempos = []
    quadril_d, joelho_d = [], []
    quadril_e, joelho_e = [], []
    velocidades, distancias = [], []
    cadencias, simetrias = [], []

    with open(ultimo_csv, mode="r", encoding="utf-8") as f:
        leitor = csv.DictReader(f)
        for linha in leitor:
            try:
                tempos.append(float(linha["Tempo_s"]))
                quadril_d.append(float(linha["Quadril_D_deg"]))
                joelho_d.append(float(linha["Joelho_D_deg"]))
                quadril_e.append(float(linha["Quadril_E_deg"]))
                joelho_e.append(float(linha["Joelho_E_deg"]))
                velocidades.append(float(linha["Velocidade_ms"]))
                distancias.append(float(linha["Distancia_m"]))
                cadencias.append(float(linha["Cadencia_SPM"]))
                simetrias.append(float(linha["Simetria_ASI_Pct"]))
            except (ValueError, KeyError):
                continue

    if not tempos:
        print("[!] O arquivo CSV está vazio (sessão muito curta).")
        return

    duracao = tempos[-1]
    dist_total = distancias[-1]
    vel_media = sum(velocidades) / len(velocidades)
    simetria_media = sum(simetrias) / len(simetrias)

    print(f" -> Amostras Coletadas   : {len(tempos)} linhas (Duração: {duracao:.1f} s)")
    print(f" -> Distância Total      : {dist_total:.2f} m")
    print(f" -> Velocidade Média     : {vel_media:.2f} m/s")
    print(f" -> ROM Máx Quadril (D/E): {max(quadril_d):.1f}° / {max(quadril_e):.1f}°")
    print(f" -> ROM Máx Joelho  (D/E): {min(joelho_d):.1f}° / {min(joelho_e):.1f}°")
    print(f" -> Simetria Média (ASI) : {simetria_media:.1f}%")
    print("=" * 72)

    # Tenta gerar gráfico científico em PNG usando matplotlib (se instalado)
    try:
        import matplotlib.pyplot as plt

        fig, axs = plt.subplots(2, 2, figsize=(14, 9))
        fig.suptitle(f"Análise Biomecânica de Marcha - ExoTao VR Rehab\n({os.path.basename(ultimo_csv)})", fontsize=14, fontweight="bold")

        # 1. Cinemática do Quadril
        axs[0, 0].plot(tempos, quadril_d, label="Quadril Direito (°)", color="#0077b6", linewidth=1.8)
        axs[0, 0].plot(tempos, quadril_e, label="Quadril Esquerdo (°)", color="#ef476f", linewidth=1.8, linestyle="--")
        axs[0, 0].set_title("Cinemática Articular - Quadril (Flexão/Extensão)")
        axs[0, 0].set_xlabel("Tempo (s)")
        axs[0, 0].set_ylabel("Ângulo (graus)")
        axs[0, 0].grid(True, alpha=0.3)
        axs[0, 0].legend()

        # 2. Cinemática do Joelho
        axs[0, 1].plot(tempos, joelho_d, label="Joelho Direito (°)", color="#00b4d8", linewidth=1.8)
        axs[0, 1].plot(tempos, joelho_e, label="Joelho Esquerdo (°)", color="#f77f00", linewidth=1.8, linestyle="--")
        axs[0, 1].set_title("Cinemática Articular - Joelho")
        axs[0, 1].set_xlabel("Tempo (s)")
        axs[0, 1].set_ylabel("Ângulo (graus)")
        axs[0, 1].grid(True, alpha=0.3)
        axs[0, 1].legend()

        # 3. Simetria Bilateral (ASI %)
        axs[1, 0].plot(tempos, simetrias, label="Índice de Simetria (%)", color="#06d6a0", linewidth=2.0)
        axs[1, 0].axhline(80, color="orange", linestyle=":", label="Limiar Clínico (80%)")
        axs[1, 0].set_ylim(0, 105)
        axs[1, 0].set_title("Evolução da Simetria Bilateral de Marcha (ASI)")
        axs[1, 0].set_xlabel("Tempo (s)")
        axs[1, 0].set_ylabel("Simetria (%)")
        axs[1, 0].grid(True, alpha=0.3)
        axs[1, 0].legend()

        # 4. Velocidade e Cadência
        ax_vel = axs[1, 1]
        ax_cad = ax_vel.twinx()
        p1 = ax_vel.plot(tempos, velocidades, label="Velocidade (m/s)", color="#7209b7", linewidth=1.8)
        p2 = ax_cad.plot(tempos, cadencias, label="Cadência (SPM)", color="#3a86ff", linewidth=1.5, linestyle="-.")
        ax_vel.set_title("Velocidade de Locomoção e Cadência (SPM)")
        ax_vel.set_xlabel("Tempo (s)")
        ax_vel.set_ylabel("Velocidade (m/s)", color="#7209b7")
        ax_cad.set_ylabel("Cadência (Passos/min)", color="#3a86ff")
        ax_vel.grid(True, alpha=0.3)

        plt.tight_layout(rect=[0, 0.03, 1, 0.94])
        caminho_png = ultimo_csv.replace("_Telemetria.csv", "_Grafico_IC.png")
        plt.savefig(caminho_png, dpi=200)
        print(f"[✓] Gráfico científico salvo em alta resolução:\n    {caminho_png}")
        plt.show()

    except ImportError:
        print("[i] Dica: instale matplotlib ('pip install matplotlib') para gerar gráficos PNG automaticamente!")

if __name__ == "__main__":
    analisar_ultima_sessao()

import socket
import time
import math

# Configurações de Rede (Devem bater com a Unity)
UDP_IP = "127.0.0.1"
UDP_PORT = 5005

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
t = 0.0

# Parâmetros da marcha (Ajustáveis para simular diferentes perfis de pacientes)
velocidade_marcha = 2.0  # Multiplicador de velocidade
amplitude_quadril = 25.0 # Elevação máxima da perna
amplitude_joelho = 30.0  # Flexão do joelho

print(f"Simulador do ExoTao iniciado na porta {UDP_PORT}...")
print("Ordem do pacote: [Joelho_D, Quadril_D, Joelho_E, Quadril_E]")
print("Para parar a simulação, pressione Ctrl+C no terminal\n")

try:
    while True:
        # Cinemática da Perna Direita
        quadril_D = amplitude_quadril * math.sin(t * velocidade_marcha)
        joelho_D = - (amplitude_joelho * math.sin((t * velocidade_marcha) - 1.5) + amplitude_joelho)
        
        # Cinemática da Perna Esquerda (Defasada em 180 graus / PI)
        quadril_E = amplitude_quadril * math.sin((t * velocidade_marcha) + math.pi)
        joelho_E = - (amplitude_joelho * math.sin((t * velocidade_marcha) + math.pi - 1.5) + amplitude_joelho)
        
        # Empacota os dados garantindo o formato padrão (. em vez de , para os decimais)
        mensagem = f"{joelho_D:.2f},{quadril_D:.2f},{joelho_E:.2f},{quadril_E:.2f}".encode('utf-8')
        sock.sendto(mensagem, (UDP_IP, UDP_PORT))
        
        # Imprime na mesma linha do terminal para não poluir a tela
        print(f"Enviando -> {mensagem.decode('utf-8')}     ", end="\r")
        
        t += 0.05
        time.sleep(0.02) # Simula uma taxa de atualização de ~50Hz

except KeyboardInterrupt:
    print("\n\nSimulação do ExoTao encerrada.")
    sock.close()
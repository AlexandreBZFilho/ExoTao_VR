import socket
import time
import math

# Configurações da rede (Localhost e a mesma porta do C#)
UDP_IP = "127.0.0.1"
UDP_PORT = 5005

print(f"Simulador do ExoTao iniciado. Enviando dados para {UDP_IP}:{UDP_PORT}...")
print("Pressione Ctrl+C para parar.")

# Cria o socket UDP
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

t = 0.0
try:
    while True:
        # Simula o ângulo do joelho (movimento pendular de -45 a 45 graus)
        angulo = 45 * math.sin(t)
        
        # Converte o número para texto e codifica em bytes
        mensagem = f"{angulo:.2f}".encode('utf-8')
        
        # Dispara o pacote UDP para a Unity
        sock.sendto(mensagem, (UDP_IP, UDP_PORT))
        
        print(f"Enviando ângulo: {angulo:.2f}°")
        
        t += 0.05 # Incrementa o tempo
        time.sleep(0.02) # Aguarda 20ms (simula uma taxa de atualização de 50Hz do robô)

except KeyboardInterrupt:
    print("\nSimulação encerrada.")
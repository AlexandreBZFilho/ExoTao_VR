import socket
import time
import math

UDP_IP = "127.0.0.1"
UDP_PORT = 5005

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
t = 0.0

print(f"Simulador do ExoTao iniciado na porta {UDP_PORT}...")
print("Para parar a simulação, pressione Ctrl+C no terminal")

try:
    while True:
        quadril_D = 25 * math.sin(t)
        joelho_D = - (30 * math.sin(t - 1.5) + 30)
        
        quadril_E = 25 * math.sin(t + math.pi)
        joelho_E = - (30 * math.sin(t + math.pi - 1.5) + 30)
        
        mensagem = f"{joelho_D:.2f},{quadril_D:.2f},{joelho_E:.2f},{quadril_E:.2f}".encode('utf-8')
        sock.sendto(mensagem, (UDP_IP, UDP_PORT))
        
        t += 0.05
        time.sleep(0.02)

except KeyboardInterrupt:
    print("\nSimulação encerrada.")
#!/usr/bin/env bash
# Diagnóstico de performance de la VPS (8 GB) — ejecutar por SSH.
# No modifica nada: solo recopila la evidencia para decidir los límites.
set -euo pipefail

echo "===================== RAM / SWAP ====================="
free -h
echo
echo "Swappiness (si la VPS swappea, esto importa):"
cat /proc/sys/vm/swappiness
echo

echo "===================== CPU ============================"
nproc
echo

echo "============== CONSUMO POR CONTENEDOR ================"
# Arranca lo que esté parado solo si quieres medir en caliente.
docker stats --no-stream --format \
  "table {{.Name}}\t{{.MemUsage}}\t{{.MemPerc}}\t{{.CPUPerc}}"
echo

echo "================= OOM KILLS (kernel) ================="
# Exited 137 = OOM. Aquí se ve a quién mató el kernel.
dmesg -T 2>/dev/null | grep -iE "out of memory|killed process|oom-kill" | tail -20 || \
  echo "(sin permisos para dmesg; prueba: sudo dmesg | grep -i oom)"
echo

echo "============ LÍMITES ACTUALES POR CONTENEDOR ========="
# Si la columna MemLimit muestra el total del host (~8 GiB), ESE contenedor no tiene tope.
docker ps -a --format '{{.Names}}' | while read -r c; do
  lim=$(docker inspect -f '{{.HostConfig.Memory}}' "$c" 2>/dev/null || echo 0)
  if [ "$lim" = "0" ]; then
    printf "  %-32s SIN LÍMITE (peligro)\n" "$c"
  else
    printf "  %-32s %s MB\n" "$c" "$((lim/1024/1024))"
  fi
done

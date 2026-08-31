#!/usr/bin/env bash
# Copia los scripts de provisión de Tracker al VPS y los ejecuta contra AuthDb.
#
#   ./db/auth/deploy-vps.sh
#
# No pide la clave de SQL: usa la variable MSSQL_SA_PASSWORD que el propio
# contenedor ya expone, así la credencial nunca sale del VPS. Requiere acceso
# SSH. Los scripts son idempotentes: si algo falla a mitad de camino, se puede
# volver a ejecutar sin duplicar datos.

set -euo pipefail

VPS_HOST="${VPS_HOST:-jorgeadmin@45.7.229.192}"
VPS_PORT="${VPS_PORT:-21483}"
REMOTE_DIR="${REMOTE_DIR:-/opt/devsogu/db/tracker-auth}"
SQL_CONTAINER="${SQL_CONTAINER:-devsogu-sqlserver}"
SQL_USER="${SQL_USER:-sa}"
SQL_DB="${SQL_DB:-AuthDb}"
BACKUP_PATH="/var/opt/mssql/backup/${SQL_DB}_pre_tracker.bak"

SCRIPTS=(01_tracker_permisos.sql 02_tracker_tenant.sql 03_tracker_roles.sql 04_tracker_admin.sql)
LOCAL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# sqlcmd se invoca DENTRO del contenedor con comillas simples, para que
# $MSSQL_SA_PASSWORD lo expanda el shell remoto y no éste.
sqlcmd_remote() {
    ssh -p "$VPS_PORT" "$VPS_HOST" \
        "sudo docker exec '$SQL_CONTAINER' bash -c '/opt/mssql-tools18/bin/sqlcmd \
         -S localhost -U $SQL_USER -P \"\$MSSQL_SA_PASSWORD\" -C $1'"
}

echo ">> Copiando scripts a ${VPS_HOST}:${REMOTE_DIR}"
ssh -p "$VPS_PORT" "$VPS_HOST" \
    "sudo mkdir -p '$REMOTE_DIR' && sudo chown \$(id -un):\$(id -gn) '$REMOTE_DIR'"
scp -P "$VPS_PORT" "${SCRIPTS[@]/#/$LOCAL_DIR/}" "$VPS_HOST:$REMOTE_DIR/"

echo ">> Respaldando ${SQL_DB} en ${BACKUP_PATH}"
ssh -p "$VPS_PORT" "$VPS_HOST" "sudo docker exec '$SQL_CONTAINER' mkdir -p /var/opt/mssql/backup"
sqlcmd_remote "-b -Q \"BACKUP DATABASE [$SQL_DB] TO DISK=N''$BACKUP_PATH'' WITH INIT, COMPRESSION\""

for f in "${SCRIPTS[@]}"; do
    echo "===================== $f ====================="
    ssh -p "$VPS_PORT" "$VPS_HOST" "sudo docker cp '$REMOTE_DIR/$f' '$SQL_CONTAINER:/tmp/$f'"
    sqlcmd_remote "-d $SQL_DB -b -i /tmp/$f" \
        || { echo ">>> FALLO EN $f - se detiene la ejecucion" >&2; exit 1; }
done

ssh -p "$VPS_PORT" "$VPS_HOST" "sudo docker exec '$SQL_CONTAINER' bash -c 'rm -f /tmp/0*_tracker_*.sql'"

echo ">> Listo. Revise los SELECT de verificacion de cada script."

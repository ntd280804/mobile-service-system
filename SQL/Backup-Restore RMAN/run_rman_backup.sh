#!/bin/bash

# Thiết lập môi trường Oracle
export ORACLE_HOME=/opt/oracle/product/21c/dbhome_1
export PATH=$ORACLE_HOME/bin:$PATH

# PDB cần backup
PDB_HOST=localhost
PDB_PORT=1521
PDB_SERVICE=ORCLPDB1
RMAN_USER=rman
RMAN_PWD=rmanpwd

# Thư mục log
LOG_DIR="/home/oracle/backup_web/logs"
mkdir -p "${LOG_DIR}"
LOG_FILE="${LOG_DIR}/rman_backup_$(date +%F_%H%M%S).log"

# Chạy RMAN backup PDB + archivelog bằng username/password via EZCONNECT
$ORACLE_HOME/bin/rman target ${RMAN_USER}/${RMAN_PWD}@//${PDB_HOST}:${PDB_PORT}/${PDB_SERVICE} <<EOF > ${LOG_FILE} 2>&1
RUN {
  BACKUP DATABASE PLUS ARCHIVELOG;
}
EOF

# Kiểm tra exit code
RET=$?
if [ $RET -ne 0 ]; then
  echo "RMAN FAILED with code $RET. Kiểm tra log: ${LOG_FILE}" >&2
else
  echo "RMAN backup of ${PDB_SERVICE} completed successfully. Log: ${LOG_FILE}"
fi

exit $RET

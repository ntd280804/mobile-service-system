#!/bin/bash

export ORACLE_HOME=/opt/oracle/product/21c/dbhome_1
export PATH=$ORACLE_HOME/bin:$PATH
export ORACLE_SID=ORCLCDB

PDB_SERVICE=ORCLPDB1
POINTOFTIME="$1"

LOG_DIR="/home/oracle/backup_web/logs"
mkdir -p "${LOG_DIR}"
LOG_FILE="${LOG_DIR}/rman_restore_$(date +%F_%H%M%S).log"

echo "===> Closing PDB ${PDB_SERVICE}..."
sqlplus -s / as sysdba <<EOF
ALTER PLUGGABLE DATABASE ${PDB_SERVICE} CLOSE IMMEDIATE;
EOF

echo "===> Running RMAN restore..."
rman target / <<EOF >> ${LOG_FILE} 2>&1
RUN {
  RESTORE PLUGGABLE DATABASE ${PDB_SERVICE};
  RECOVER PLUGGABLE DATABASE ${PDB_SERVICE};
}
EOF

echo "===> Opening PDB ${PDB_SERVICE}..."
sqlplus -s / as sysdba <<EOF
ALTER PLUGGABLE DATABASE ${PDB_SERVICE} OPEN;
EOF
RET=$?
if [ $RET -ne 0 ]; then
  echo "❌ RESTORE FAILED — check log: ${LOG_FILE}"
else
  echo "✅ RESTORE SUCCESS — log: ${LOG_FILE}"
fi

exit $RET

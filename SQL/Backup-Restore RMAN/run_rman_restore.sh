#!/bin/bash

export ORACLE_HOME=/opt/oracle/product/21c/dbhome_1
export PATH=$ORACLE_HOME/bin:$PATH
export ORACLE_SID=ORCLCDB

PDB_SERVICE=ORCLPDB1
POINTOFTIME="$1"

if [ -z "$POINTOFTIME" ]; then
    echo "❌ Missing Point-In-Time value."
    echo "Usage: $0 \"YYYY-MM-DD HH24:MI:SS\""
    exit 1
fi

LOG_DIR="/home/oracle/backup_web/logs"
mkdir -p "${LOG_DIR}"
LOG_FILE="${LOG_DIR}/rman_restore_$(date +%F_%H%M%S).log"

echo "===> Closing PDB ${PDB_SERVICE}..."
sqlplus -s / as sysdba <<EOF
ALTER PLUGGABLE DATABASE ${PDB_SERVICE} CLOSE IMMEDIATE;
EOF

echo "===> Running RMAN PITR restore..."
rman target / <<EOF >> ${LOG_FILE} 2>&1
RUN {
  SET UNTIL TIME "to_date('${POINTOFTIME}', 'YYYY-MM-DD HH24:MI:SS')";
  RESTORE PLUGGABLE DATABASE ${PDB_SERVICE};
  RECOVER PLUGGABLE DATABASE ${PDB_SERVICE};
  ALTER PLUGGABLE DATABASE ${PDB_SERVICE} OPEN RESETLOGS;
}
EOF

RET=$?
if [ $RET -ne 0 ]; then
  echo "❌ RESTORE FAILED — check log: ${LOG_FILE}"
else
  echo "✅ RESTORE SUCCESS (PITR) — log: ${LOG_FILE}"
fi

exit $RET

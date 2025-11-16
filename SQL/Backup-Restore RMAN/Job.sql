BEGIN
   DBMS_SCHEDULER.create_job (
      job_name        => 'RUN_RMAN_PDB_BACKUP',
      job_type        => 'EXECUTABLE',
      job_action      => '/home/oracle/backup_web/scripts/run_rman_backup.sh',
      start_date      => SYSTIMESTAMP,  -- bắt đầu từ thời điểm hiện tại
      repeat_interval => 'FREQ=DAILY; BYHOUR=2; BYMINUTE=0; BYSECOND=0', -- 2:00 AM hàng ngày
      enabled         => TRUE,
      auto_drop       => FALSE,
      comments        => 'Daily RMAN PDB backup at 2 AM'
   );
END;
CREATE OR REPLACE PROCEDURE RUN_RMAN_PDB_BACKUP_PROC AS
BEGIN
   DBMS_SCHEDULER.RUN_JOB('APP.RUN_RMAN_PDB_BACKUP');
END;
/


BEGIN
   DBMS_SCHEDULER.create_job (
      job_name        => 'RUN_RMAN_PDB_RESTORE',
      job_type        => 'EXECUTABLE',
      job_action      => '/home/oracle/backup_web/scripts/run_rman_restore.sh',
      enabled         => FALSE,  -- tạo nhưng chưa bật lịch
      auto_drop       => FALSE,
      comments        => 'Restore PDB on demand'
   );
END;
/
CREATE OR REPLACE PROCEDURE RUN_RMAN_PDB_RESTORE_PROC AS
BEGIN
   DBMS_SCHEDULER.RUN_JOB('APP.RUN_RMAN_PDB_RESTORE');
END;
/

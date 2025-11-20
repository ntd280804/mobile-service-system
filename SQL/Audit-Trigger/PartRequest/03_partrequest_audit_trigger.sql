-- Audit trigger for PART_REQUEST table to capture before/after values
-- Run as schema owner (e.g., APP)

CREATE OR REPLACE TRIGGER trg_audit_part_request
  AFTER INSERT OR UPDATE OR DELETE ON PART_REQUEST
  FOR EACH ROW
DECLARE
  v_old_values CLOB;
  v_new_values CLOB;
  v_changed_cols VARCHAR2(4000);
BEGIN
  -- Build old values (for UPDATE/DELETE)
  IF UPDATING OR DELETING THEN
    v_old_values := '{' ||
      '"REQUEST_ID":' || NVL(TO_CHAR(:OLD.REQUEST_ID), 'null') || ',' ||
      '"ORDER_ID":' || NVL(TO_CHAR(:OLD.ORDER_ID), 'null') || ',' ||
      '"EMP_ID":' || NVL(TO_CHAR(:OLD.EMP_ID), 'null') || ',' ||
      '"REQUEST_DATE":"' || TO_CHAR(:OLD.REQUEST_DATE, 'YYYY-MM-DD') || '",' ||
      '"STATUS":"' || REPLACE(NVL(:OLD.STATUS, ''), '"', '\"') || '"' ||
      '}';
  END IF;
  
  -- Build new values (for INSERT/UPDATE)
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"REQUEST_ID":' || NVL(TO_CHAR(:NEW.REQUEST_ID), 'null') || ',' ||
      '"ORDER_ID":' || NVL(TO_CHAR(:NEW.ORDER_ID), 'null') || ',' ||
      '"EMP_ID":' || NVL(TO_CHAR(:NEW.EMP_ID), 'null') || ',' ||
      '"REQUEST_DATE":"' || TO_CHAR(:NEW.REQUEST_DATE, 'YYYY-MM-DD') || '",' ||
      '"STATUS":"' || REPLACE(NVL(:NEW.STATUS, ''), '"', '\"') || '"' ||
      '}';
  END IF;
  
  -- Track changed columns for UPDATE
  IF UPDATING THEN
    v_changed_cols := '';
    IF (:OLD.ORDER_ID != :NEW.ORDER_ID) THEN v_changed_cols := v_changed_cols || 'ORDER_ID,'; END IF;
    IF (:OLD.EMP_ID != :NEW.EMP_ID) THEN v_changed_cols := v_changed_cols || 'EMP_ID,'; END IF;
    IF (:OLD.REQUEST_DATE != :NEW.REQUEST_DATE) THEN v_changed_cols := v_changed_cols || 'REQUEST_DATE,'; END IF;
    IF ((:OLD.STATUS IS NULL AND :NEW.STATUS IS NOT NULL) OR 
        (:OLD.STATUS IS NOT NULL AND :NEW.STATUS IS NULL) OR
        (:OLD.STATUS != :NEW.STATUS)) THEN 
      v_changed_cols := v_changed_cols || 'STATUS,'; 
    END IF;
    IF LENGTH(v_changed_cols) > 0 THEN
      v_changed_cols := SUBSTR(v_changed_cols, 1, LENGTH(v_changed_cols) - 1);
    END IF;
  END IF;
  
  -- Log the operation
  IF INSERTING THEN
    audit_dml_pkg.log_insert('PART_REQUEST', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('PART_REQUEST', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('PART_REQUEST', v_old_values);
  END IF;
END;
/


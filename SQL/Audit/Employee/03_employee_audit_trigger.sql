-- Audit trigger for EMPLOYEE table to capture before/after values
-- Run as schema owner (e.g., APP)

CREATE OR REPLACE TRIGGER trg_audit_employee
  AFTER INSERT OR UPDATE OR DELETE ON EMPLOYEE
  FOR EACH ROW
DECLARE
  v_old_values CLOB;
  v_new_values CLOB;
  v_changed_cols VARCHAR2(4000);
BEGIN
  -- Build old values (for UPDATE/DELETE)
  IF UPDATING OR DELETING THEN
    v_old_values := '{' ||
      '"EMP_ID":' || NVL(TO_CHAR(:OLD.EMP_ID), 'null') || ',' ||
      '"FULL_NAME":"' || REPLACE(NVL(:OLD.FULL_NAME, ''), '"', '\"') || '",' ||
      '"USERNAME":"' || REPLACE(NVL(:OLD.USERNAME, ''), '"', '\"') || '",' ||
      '"EMAIL":"' || REPLACE(NVL(:OLD.EMAIL, ''), '"', '\"') || '",' ||
      '"PHONE":"' || REPLACE(NVL(:OLD.PHONE, ''), '"', '\"') || '",' ||
      '"CREATED_AT":"' || TO_CHAR(:OLD.CREATED_AT, 'YYYY-MM-DD HH24:MI:SS') || '"' ||
      '}';
  END IF;
  
  -- Build new values (for INSERT/UPDATE)
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"EMP_ID":' || NVL(TO_CHAR(:NEW.EMP_ID), 'null') || ',' ||
      '"FULL_NAME":"' || REPLACE(NVL(:NEW.FULL_NAME, ''), '"', '\"') || '",' ||
      '"USERNAME":"' || REPLACE(NVL(:NEW.USERNAME, ''), '"', '\"') || '",' ||
      '"EMAIL":"' || REPLACE(NVL(:NEW.EMAIL, ''), '"', '\"') || '",' ||
      '"PHONE":"' || REPLACE(NVL(:NEW.PHONE, ''), '"', '\"') || '",' ||
      '"CREATED_AT":"' || TO_CHAR(:NEW.CREATED_AT, 'YYYY-MM-DD HH24:MI:SS') || '"' ||
      '}';
  END IF;
  
  -- Track changed columns for UPDATE
  IF UPDATING THEN
    v_changed_cols := '';
    IF (:OLD.FULL_NAME IS NULL AND :NEW.FULL_NAME IS NOT NULL) OR 
       (:OLD.FULL_NAME IS NOT NULL AND :NEW.FULL_NAME IS NULL) OR
       (:OLD.FULL_NAME != :NEW.FULL_NAME) THEN
      v_changed_cols := v_changed_cols || 'FULL_NAME,';
    END IF;
    IF (:OLD.USERNAME IS NULL AND :NEW.USERNAME IS NOT NULL) OR 
       (:OLD.USERNAME IS NOT NULL AND :NEW.USERNAME IS NULL) OR
       (:OLD.USERNAME != :NEW.USERNAME) THEN
      v_changed_cols := v_changed_cols || 'USERNAME,';
    END IF;
    IF (:OLD.EMAIL IS NULL AND :NEW.EMAIL IS NOT NULL) OR 
       (:OLD.EMAIL IS NOT NULL AND :NEW.EMAIL IS NULL) OR
       (:OLD.EMAIL != :NEW.EMAIL) THEN
      v_changed_cols := v_changed_cols || 'EMAIL,';
    END IF;
    IF (:OLD.PHONE IS NULL AND :NEW.PHONE IS NOT NULL) OR 
       (:OLD.PHONE IS NOT NULL AND :NEW.PHONE IS NULL) OR
       (:OLD.PHONE != :NEW.PHONE) THEN
      v_changed_cols := v_changed_cols || 'PHONE,';
    END IF;
    -- Remove trailing comma
    IF LENGTH(v_changed_cols) > 0 THEN
      v_changed_cols := SUBSTR(v_changed_cols, 1, LENGTH(v_changed_cols) - 1);
    END IF;
  END IF;
  
  -- Log the operation
  IF INSERTING THEN
    audit_dml_pkg.log_insert('EMPLOYEE', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('EMPLOYEE', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('EMPLOYEE', v_old_values);
  END IF;
END;
/


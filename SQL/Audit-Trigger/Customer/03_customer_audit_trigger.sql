-- Audit trigger for CUSTOMER table to capture before/after values
-- Run as schema owner (e.g., APP)

CREATE OR REPLACE TRIGGER trg_audit_customer
  AFTER INSERT OR UPDATE OR DELETE ON CUSTOMER
  FOR EACH ROW
DECLARE
  v_old_values CLOB;
  v_new_values CLOB;
  v_changed_cols VARCHAR2(4000);
BEGIN
  -- Build old values (for UPDATE/DELETE)
  IF UPDATING OR DELETING THEN
    v_old_values := '{' ||
      '"PHONE":"' || REPLACE(NVL(:OLD.PHONE, ''), '"', '\"') || '",' ||
      '"FULL_NAME":"' || REPLACE(NVL(:OLD.FULL_NAME, ''), '"', '\"') || '",' ||
      '"EMAIL":"' || REPLACE(NVL(:OLD.EMAIL, ''), '"', '\"') || '",' ||
      '"ADDRESS":"' || REPLACE(NVL(:OLD.ADDRESS, ''), '"', '\"') || '",' ||
      '"CREATED_AT":"' || TO_CHAR(:OLD.CREATED_AT, 'YYYY-MM-DD HH24:MI:SS') || '"' ||
      '}';
  END IF;
  
  -- Build new values (for INSERT/UPDATE)
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"PHONE":"' || REPLACE(NVL(:NEW.PHONE, ''), '"', '\"') || '",' ||
      '"FULL_NAME":"' || REPLACE(NVL(:NEW.FULL_NAME, ''), '"', '\"') || '",' ||
      '"EMAIL":"' || REPLACE(NVL(:NEW.EMAIL, ''), '"', '\"') || '",' ||
      '"ADDRESS":"' || REPLACE(NVL(:NEW.ADDRESS, ''), '"', '\"') || '",' ||
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
    IF (:OLD.EMAIL IS NULL AND :NEW.EMAIL IS NOT NULL) OR 
       (:OLD.EMAIL IS NOT NULL AND :NEW.EMAIL IS NULL) OR
       (:OLD.EMAIL != :NEW.EMAIL) THEN
      v_changed_cols := v_changed_cols || 'EMAIL,';
    END IF;
    IF (:OLD.ADDRESS IS NULL AND :NEW.ADDRESS IS NOT NULL) OR 
       (:OLD.ADDRESS IS NOT NULL AND :NEW.ADDRESS IS NULL) OR
       (:OLD.ADDRESS != :NEW.ADDRESS) THEN
      v_changed_cols := v_changed_cols || 'ADDRESS,';
    END IF;
    IF LENGTH(v_changed_cols) > 0 THEN
      v_changed_cols := SUBSTR(v_changed_cols, 1, LENGTH(v_changed_cols) - 1);
    END IF;
  END IF;
  
  -- Log the operation
  IF INSERTING THEN
    audit_dml_pkg.log_insert('CUSTOMER', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('CUSTOMER', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('CUSTOMER', v_old_values);
  END IF;
END;
/


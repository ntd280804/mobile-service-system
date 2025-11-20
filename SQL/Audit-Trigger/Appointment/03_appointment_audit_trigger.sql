-- Audit trigger for CUSTOMER_APPOINTMENT table to capture before/after values
-- Run as schema owner (e.g., APP)

CREATE OR REPLACE TRIGGER trg_audit_customer_appointment
  AFTER INSERT OR UPDATE OR DELETE ON CUSTOMER_APPOINTMENT
  FOR EACH ROW
DECLARE
  v_old_values CLOB;
  v_new_values CLOB;
  v_changed_cols VARCHAR2(4000);
BEGIN
  -- Build old values (for UPDATE/DELETE)
  IF UPDATING OR DELETING THEN
    v_old_values := '{' ||
      '"APPOINTMENT_ID":' || NVL(TO_CHAR(:OLD.APPOINTMENT_ID), 'null') || ',' ||
      '"CUSTOMER_PHONE":"' || REPLACE(NVL(:OLD.CUSTOMER_PHONE, ''), '"', '\"') || '",' ||
      '"APPOINTMENT_DATE":"' || TO_CHAR(:OLD.APPOINTMENT_DATE, 'YYYY-MM-DD') || '",' ||
      '"STATUS":"' || REPLACE(NVL(:OLD.STATUS, ''), '"', '\"') || '",' ||
      '"DESCRIPTION":"' || REPLACE(NVL(:OLD.DESCRIPTION, ''), '"', '\"') || '"' ||
      '}';
  END IF;
  
  -- Build new values (for INSERT/UPDATE)
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"APPOINTMENT_ID":' || NVL(TO_CHAR(:NEW.APPOINTMENT_ID), 'null') || ',' ||
      '"CUSTOMER_PHONE":"' || REPLACE(NVL(:NEW.CUSTOMER_PHONE, ''), '"', '\"') || '",' ||
      '"APPOINTMENT_DATE":"' || TO_CHAR(:NEW.APPOINTMENT_DATE, 'YYYY-MM-DD') || '",' ||
      '"STATUS":"' || REPLACE(NVL(:NEW.STATUS, ''), '"', '\"') || '",' ||
      '"DESCRIPTION":"' || REPLACE(NVL(:NEW.DESCRIPTION, ''), '"', '\"') || '"' ||
      '}';
  END IF;
  
  -- Track changed columns for UPDATE
  IF UPDATING THEN
    v_changed_cols := '';
    IF ((:OLD.CUSTOMER_PHONE IS NULL AND :NEW.CUSTOMER_PHONE IS NOT NULL) OR 
        (:OLD.CUSTOMER_PHONE IS NOT NULL AND :NEW.CUSTOMER_PHONE IS NULL) OR
        (:OLD.CUSTOMER_PHONE != :NEW.CUSTOMER_PHONE)) THEN 
      v_changed_cols := v_changed_cols || 'CUSTOMER_PHONE,'; 
    END IF;
    IF (:OLD.APPOINTMENT_DATE != :NEW.APPOINTMENT_DATE) THEN v_changed_cols := v_changed_cols || 'APPOINTMENT_DATE,'; END IF;
    IF ((:OLD.STATUS IS NULL AND :NEW.STATUS IS NOT NULL) OR 
        (:OLD.STATUS IS NOT NULL AND :NEW.STATUS IS NULL) OR
        (:OLD.STATUS != :NEW.STATUS)) THEN 
      v_changed_cols := v_changed_cols || 'STATUS,'; 
    END IF;
    IF ((:OLD.DESCRIPTION IS NULL AND :NEW.DESCRIPTION IS NOT NULL) OR 
        (:OLD.DESCRIPTION IS NOT NULL AND :NEW.DESCRIPTION IS NULL) OR
        (:OLD.DESCRIPTION != :NEW.DESCRIPTION)) THEN 
      v_changed_cols := v_changed_cols || 'DESCRIPTION,'; 
    END IF;
    IF LENGTH(v_changed_cols) > 0 THEN
      v_changed_cols := SUBSTR(v_changed_cols, 1, LENGTH(v_changed_cols) - 1);
    END IF;
  END IF;
  
  -- Log the operation
  IF INSERTING THEN
    audit_dml_pkg.log_insert('CUSTOMER_APPOINTMENT', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('CUSTOMER_APPOINTMENT', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('CUSTOMER_APPOINTMENT', v_old_values);
  END IF;
END;
/


-- Audit trigger for ORDERS table to capture before/after values
-- Run as schema owner (e.g., APP)

CREATE OR REPLACE TRIGGER trg_audit_orders
  AFTER INSERT OR UPDATE OR DELETE ON ORDERS
  FOR EACH ROW
DECLARE
  v_old_values CLOB;
  v_new_values CLOB;
  v_changed_cols VARCHAR2(4000);
BEGIN
  -- Build old values (for UPDATE/DELETE)
  IF UPDATING OR DELETING THEN
    v_old_values := '{' ||
      '"ORDER_ID":' || NVL(TO_CHAR(:OLD.ORDER_ID), 'null') || ',' ||
      '"CUSTOMER_PHONE":"' || REPLACE(NVL(:OLD.CUSTOMER_PHONE, ''), '"', '\"') || '",' ||
      '"RECEIVER_EMP":' || NVL(TO_CHAR(:OLD.RECEIVER_EMP), 'null') || ',' ||
      '"HANDLER_EMP":' || NVL(TO_CHAR(:OLD.HANDLER_EMP), 'null') || ',' ||
      '"ORDER_TYPE":"' || REPLACE(NVL(:OLD.ORDER_TYPE, ''), '"', '\"') || '",' ||
      '"RECEIVED_DATE":"' || TO_CHAR(:OLD.RECEIVED_DATE, 'YYYY-MM-DD') || '",' ||
      '"STATUS":"' || REPLACE(NVL(:OLD.STATUS, ''), '"', '\"') || '",' ||
      '"DESCRIPTION":"' || REPLACE(NVL(:OLD.DESCRIPTION, ''), '"', '\"') || '"' ||
      '}';
  END IF;
  
  -- Build new values (for INSERT/UPDATE)
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"ORDER_ID":' || NVL(TO_CHAR(:NEW.ORDER_ID), 'null') || ',' ||
      '"CUSTOMER_PHONE":"' || REPLACE(NVL(:NEW.CUSTOMER_PHONE, ''), '"', '\"') || '",' ||
      '"RECEIVER_EMP":' || NVL(TO_CHAR(:NEW.RECEIVER_EMP), 'null') || ',' ||
      '"HANDLER_EMP":' || NVL(TO_CHAR(:NEW.HANDLER_EMP), 'null') || ',' ||
      '"ORDER_TYPE":"' || REPLACE(NVL(:NEW.ORDER_TYPE, ''), '"', '\"') || '",' ||
      '"RECEIVED_DATE":"' || TO_CHAR(:NEW.RECEIVED_DATE, 'YYYY-MM-DD') || '",' ||
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
    IF (:OLD.RECEIVER_EMP != :NEW.RECEIVER_EMP) THEN v_changed_cols := v_changed_cols || 'RECEIVER_EMP,'; END IF;
    IF (:OLD.HANDLER_EMP != :NEW.HANDLER_EMP) THEN v_changed_cols := v_changed_cols || 'HANDLER_EMP,'; END IF;
    IF ((:OLD.ORDER_TYPE IS NULL AND :NEW.ORDER_TYPE IS NOT NULL) OR 
        (:OLD.ORDER_TYPE IS NOT NULL AND :NEW.ORDER_TYPE IS NULL) OR
        (:OLD.ORDER_TYPE != :NEW.ORDER_TYPE)) THEN 
      v_changed_cols := v_changed_cols || 'ORDER_TYPE,'; 
    END IF;
    IF (:OLD.RECEIVED_DATE != :NEW.RECEIVED_DATE) THEN v_changed_cols := v_changed_cols || 'RECEIVED_DATE,'; END IF;
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
    audit_dml_pkg.log_insert('ORDERS', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('ORDERS', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('ORDERS', v_old_values);
  END IF;
END;
/


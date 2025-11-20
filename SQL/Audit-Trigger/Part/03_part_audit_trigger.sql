-- Audit trigger for PART table to capture before/after values
-- Run as schema owner (e.g., APP)

CREATE OR REPLACE TRIGGER trg_audit_part
  AFTER INSERT OR UPDATE OR DELETE ON PART
  FOR EACH ROW
DECLARE
  v_old_values CLOB;
  v_new_values CLOB;
  v_changed_cols VARCHAR2(4000);
BEGIN
  -- Build old values (for UPDATE/DELETE)
  IF UPDATING OR DELETING THEN
    v_old_values := '{' ||
      '"PART_ID":' || NVL(TO_CHAR(:OLD.PART_ID), 'null') || ',' ||
      '"NAME":"' || REPLACE(NVL(:OLD.NAME, ''), '"', '\"') || '",' ||
      '"MANUFACTURER":"' || REPLACE(NVL(:OLD.MANUFACTURER, ''), '"', '\"') || '",' ||
      '"SERIAL":"' || REPLACE(NVL(:OLD.SERIAL, ''), '"', '\"') || '",' ||
      '"STATUS":"' || REPLACE(NVL(:OLD.STATUS, ''), '"', '\"') || '",' ||
      '"STOCK_IN_ID":' || NVL(TO_CHAR(:OLD.STOCK_IN_ID), 'null') || ',' ||
      '"ORDER_ID":' || NVL(TO_CHAR(:OLD.ORDER_ID), 'null') || ',' ||
      '"PRICE":' || NVL(TO_CHAR(:OLD.PRICE), 'null') ||
      '}';
  END IF;
  
  -- Build new values (for INSERT/UPDATE)
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"PART_ID":' || NVL(TO_CHAR(:NEW.PART_ID), 'null') || ',' ||
      '"NAME":"' || REPLACE(NVL(:NEW.NAME, ''), '"', '\"') || '",' ||
      '"MANUFACTURER":"' || REPLACE(NVL(:NEW.MANUFACTURER, ''), '"', '\"') || '",' ||
      '"SERIAL":"' || REPLACE(NVL(:NEW.SERIAL, ''), '"', '\"') || '",' ||
      '"STATUS":"' || REPLACE(NVL(:NEW.STATUS, ''), '"', '\"') || '",' ||
      '"STOCK_IN_ID":' || NVL(TO_CHAR(:NEW.STOCK_IN_ID), 'null') || ',' ||
      '"ORDER_ID":' || NVL(TO_CHAR(:NEW.ORDER_ID), 'null') || ',' ||
      '"PRICE":' || NVL(TO_CHAR(:NEW.PRICE), 'null') ||
      '}';
  END IF;
  
  -- Track changed columns for UPDATE
  IF UPDATING THEN
    v_changed_cols := '';
    IF ((:OLD.NAME IS NULL AND :NEW.NAME IS NOT NULL) OR 
        (:OLD.NAME IS NOT NULL AND :NEW.NAME IS NULL) OR
        (:OLD.NAME != :NEW.NAME)) THEN 
      v_changed_cols := v_changed_cols || 'NAME,'; 
    END IF;
    IF ((:OLD.MANUFACTURER IS NULL AND :NEW.MANUFACTURER IS NOT NULL) OR 
        (:OLD.MANUFACTURER IS NOT NULL AND :NEW.MANUFACTURER IS NULL) OR
        (:OLD.MANUFACTURER != :NEW.MANUFACTURER)) THEN 
      v_changed_cols := v_changed_cols || 'MANUFACTURER,'; 
    END IF;
    IF ((:OLD.SERIAL IS NULL AND :NEW.SERIAL IS NOT NULL) OR 
        (:OLD.SERIAL IS NOT NULL AND :NEW.SERIAL IS NULL) OR
        (:OLD.SERIAL != :NEW.SERIAL)) THEN 
      v_changed_cols := v_changed_cols || 'SERIAL,'; 
    END IF;
    IF ((:OLD.STATUS IS NULL AND :NEW.STATUS IS NOT NULL) OR 
        (:OLD.STATUS IS NOT NULL AND :NEW.STATUS IS NULL) OR
        (:OLD.STATUS != :NEW.STATUS)) THEN 
      v_changed_cols := v_changed_cols || 'STATUS,'; 
    END IF;
    IF (:OLD.STOCK_IN_ID != :NEW.STOCK_IN_ID) THEN v_changed_cols := v_changed_cols || 'STOCK_IN_ID,'; END IF;
    IF (:OLD.ORDER_ID IS NULL AND :NEW.ORDER_ID IS NOT NULL) OR 
       (:OLD.ORDER_ID IS NOT NULL AND :NEW.ORDER_ID IS NULL) OR
       (:OLD.ORDER_ID != :NEW.ORDER_ID) THEN
      v_changed_cols := v_changed_cols || 'ORDER_ID,';
    END IF;
    IF (:OLD.PRICE IS NULL AND :NEW.PRICE IS NOT NULL) OR 
       (:OLD.PRICE IS NOT NULL AND :NEW.PRICE IS NULL) OR
       (:OLD.PRICE != :NEW.PRICE) THEN
      v_changed_cols := v_changed_cols || 'PRICE,';
    END IF;
    IF LENGTH(v_changed_cols) > 0 THEN
      v_changed_cols := SUBSTR(v_changed_cols, 1, LENGTH(v_changed_cols) - 1);
    END IF;
  END IF;
  
  -- Log the operation
  IF INSERTING THEN
    audit_dml_pkg.log_insert('PART', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('PART', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('PART', v_old_values);
  END IF;
END;
/


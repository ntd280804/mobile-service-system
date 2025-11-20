-- Audit trigger for STOCK_IN_ITEM table to capture before/after values
-- Run as schema owner (e.g., APP)

CREATE OR REPLACE TRIGGER trg_audit_stock_in_item
  AFTER INSERT OR UPDATE OR DELETE ON STOCK_IN_ITEM
  FOR EACH ROW
DECLARE
  v_old_values CLOB;
  v_new_values CLOB;
  v_changed_cols VARCHAR2(4000);
BEGIN
  -- Build old values (for UPDATE/DELETE)
  IF UPDATING OR DELETING THEN
    v_old_values := '{' ||
      '"STOCKIN_ITEM_ID":' || NVL(TO_CHAR(:OLD.STOCKIN_ITEM_ID), 'null') || ',' ||
      '"STOCKIN_ID":' || NVL(TO_CHAR(:OLD.STOCKIN_ID), 'null') || ',' ||
      '"PART_NAME":"' || REPLACE(NVL(:OLD.PART_NAME, ''), '"', '\"') || '",' ||
      '"MANUFACTURER":"' || REPLACE(NVL(:OLD.MANUFACTURER, ''), '"', '\"') || '",' ||
      '"SERIAL":"' || REPLACE(NVL(:OLD.SERIAL, ''), '"', '\"') || '",' ||
      '"PRICE":' || NVL(TO_CHAR(:OLD.PRICE), 'null') ||
      '}';
  END IF;
  
  -- Build new values (for INSERT/UPDATE)
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"STOCKIN_ITEM_ID":' || NVL(TO_CHAR(:NEW.STOCKIN_ITEM_ID), 'null') || ',' ||
      '"STOCKIN_ID":' || NVL(TO_CHAR(:NEW.STOCKIN_ID), 'null') || ',' ||
      '"PART_NAME":"' || REPLACE(NVL(:NEW.PART_NAME, ''), '"', '\"') || '",' ||
      '"MANUFACTURER":"' || REPLACE(NVL(:NEW.MANUFACTURER, ''), '"', '\"') || '",' ||
      '"SERIAL":"' || REPLACE(NVL(:NEW.SERIAL, ''), '"', '\"') || '",' ||
      '"PRICE":' || NVL(TO_CHAR(:NEW.PRICE), 'null') ||
      '}';
  END IF;
  
  -- Track changed columns for UPDATE
  IF UPDATING THEN
    v_changed_cols := '';
    IF (:OLD.STOCKIN_ID != :NEW.STOCKIN_ID) THEN v_changed_cols := v_changed_cols || 'STOCKIN_ID,'; END IF;
    IF ((:OLD.PART_NAME IS NULL AND :NEW.PART_NAME IS NOT NULL) OR 
        (:OLD.PART_NAME IS NOT NULL AND :NEW.PART_NAME IS NULL) OR
        (:OLD.PART_NAME != :NEW.PART_NAME)) THEN 
      v_changed_cols := v_changed_cols || 'PART_NAME,'; 
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
    audit_dml_pkg.log_insert('STOCK_IN_ITEM', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('STOCK_IN_ITEM', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('STOCK_IN_ITEM', v_old_values);
  END IF;
END;
/


-- Audit trigger for STOCK_OUT_ITEM table to capture before/after values
-- Run as schema owner (e.g., APP)

CREATE OR REPLACE TRIGGER trg_audit_stock_out_item
  AFTER INSERT OR UPDATE OR DELETE ON STOCK_OUT_ITEM
  FOR EACH ROW
DECLARE
  v_old_values CLOB;
  v_new_values CLOB;
  v_changed_cols VARCHAR2(4000);
BEGIN
  -- Build old values (for UPDATE/DELETE)
  IF UPDATING OR DELETING THEN
    v_old_values := '{' ||
      '"STOCKOUT_ID":' || NVL(TO_CHAR(:OLD.STOCKOUT_ID), 'null') || ',' ||
      '"PART_ID":' || NVL(TO_CHAR(:OLD.PART_ID), 'null') ||
      '}';
  END IF;
  
  -- Build new values (for INSERT/UPDATE)
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"STOCKOUT_ID":' || NVL(TO_CHAR(:NEW.STOCKOUT_ID), 'null') || ',' ||
      '"PART_ID":' || NVL(TO_CHAR(:NEW.PART_ID), 'null') ||
      '}';
  END IF;
  
  -- Track changed columns for UPDATE
  IF UPDATING THEN
    v_changed_cols := '';
    IF (:OLD.STOCKOUT_ID != :NEW.STOCKOUT_ID) THEN v_changed_cols := v_changed_cols || 'STOCKOUT_ID,'; END IF;
    IF (:OLD.PART_ID != :NEW.PART_ID) THEN v_changed_cols := v_changed_cols || 'PART_ID,'; END IF;
    IF LENGTH(v_changed_cols) > 0 THEN
      v_changed_cols := SUBSTR(v_changed_cols, 1, LENGTH(v_changed_cols) - 1);
    END IF;
  END IF;
  
  -- Log the operation
  IF INSERTING THEN
    audit_dml_pkg.log_insert('STOCK_OUT_ITEM', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('STOCK_OUT_ITEM', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('STOCK_OUT_ITEM', v_old_values);
  END IF;
END;
/


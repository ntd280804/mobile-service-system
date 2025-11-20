-- Audit trigger for STOCK_OUT table to capture before/after values
-- Run as schema owner (e.g., APP)

CREATE OR REPLACE TRIGGER trg_audit_stock_out
  AFTER INSERT OR UPDATE OR DELETE ON STOCK_OUT
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
      '"ORDER_ID":' || NVL(TO_CHAR(:OLD.ORDER_ID), 'null') || ',' ||
      '"EMP_ID":' || NVL(TO_CHAR(:OLD.EMP_ID), 'null') || ',' ||
      '"OUT_DATE":"' || TO_CHAR(:OLD.OUT_DATE, 'YYYY-MM-DD') || '",' ||
      '"NOTE":"' || REPLACE(NVL(:OLD.NOTE, ''), '"', '\"') || '"' ||
      '}';
  END IF;
  
  -- Build new values (for INSERT/UPDATE)
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"STOCKOUT_ID":' || NVL(TO_CHAR(:NEW.STOCKOUT_ID), 'null') || ',' ||
      '"ORDER_ID":' || NVL(TO_CHAR(:NEW.ORDER_ID), 'null') || ',' ||
      '"EMP_ID":' || NVL(TO_CHAR(:NEW.EMP_ID), 'null') || ',' ||
      '"OUT_DATE":"' || TO_CHAR(:NEW.OUT_DATE, 'YYYY-MM-DD') || '",' ||
      '"NOTE":"' || REPLACE(NVL(:NEW.NOTE, ''), '"', '\"') || '"' ||
      '}';
  END IF;
  
  -- Track changed columns for UPDATE
  IF UPDATING THEN
    v_changed_cols := '';
    IF (:OLD.ORDER_ID != :NEW.ORDER_ID) THEN v_changed_cols := v_changed_cols || 'ORDER_ID,'; END IF;
    IF (:OLD.EMP_ID != :NEW.EMP_ID) THEN v_changed_cols := v_changed_cols || 'EMP_ID,'; END IF;
    IF (:OLD.OUT_DATE != :NEW.OUT_DATE) THEN v_changed_cols := v_changed_cols || 'OUT_DATE,'; END IF;
    IF ((:OLD.NOTE IS NULL AND :NEW.NOTE IS NOT NULL) OR 
        (:OLD.NOTE IS NOT NULL AND :NEW.NOTE IS NULL) OR
        (:OLD.NOTE != :NEW.NOTE)) THEN 
      v_changed_cols := v_changed_cols || 'NOTE,'; 
    END IF;
    IF LENGTH(v_changed_cols) > 0 THEN
      v_changed_cols := SUBSTR(v_changed_cols, 1, LENGTH(v_changed_cols) - 1);
    END IF;
  END IF;
  
  -- Log the operation
  IF INSERTING THEN
    audit_dml_pkg.log_insert('STOCK_OUT', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('STOCK_OUT', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('STOCK_OUT', v_old_values);
  END IF;
END;
/


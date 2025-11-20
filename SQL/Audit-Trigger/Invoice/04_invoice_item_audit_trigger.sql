-- Audit trigger for INVOICE_ITEM table to capture before/after values
-- Run as schema owner (e.g., APP)

CREATE OR REPLACE TRIGGER trg_audit_invoice_item
  AFTER INSERT OR UPDATE OR DELETE ON INVOICE_ITEM
  FOR EACH ROW
DECLARE
  v_old_values CLOB;
  v_new_values CLOB;
  v_changed_cols VARCHAR2(4000);
BEGIN
  -- Build old values (for UPDATE/DELETE)
  IF UPDATING OR DELETING THEN
    v_old_values := '{' ||
      '"INVOICE_ID":' || NVL(TO_CHAR(:OLD.INVOICE_ID), 'null') || ',' ||
      '"STOCKOUT_ID":' || NVL(TO_CHAR(:OLD.STOCKOUT_ID), 'null') || ',' ||
      '"PART_ID":' || NVL(TO_CHAR(:OLD.PART_ID), 'null') || ',' ||
      '"PRICE":' || NVL(TO_CHAR(:OLD.PRICE), 'null') ||
      '}';
  END IF;
  
  -- Build new values (for INSERT/UPDATE)
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"INVOICE_ID":' || NVL(TO_CHAR(:NEW.INVOICE_ID), 'null') || ',' ||
      '"STOCKOUT_ID":' || NVL(TO_CHAR(:NEW.STOCKOUT_ID), 'null') || ',' ||
      '"PART_ID":' || NVL(TO_CHAR(:NEW.PART_ID), 'null') || ',' ||
      '"PRICE":' || NVL(TO_CHAR(:NEW.PRICE), 'null') ||
      '}';
  END IF;
  
  -- Track changed columns for UPDATE
  IF UPDATING THEN
    v_changed_cols := '';
    IF (:OLD.INVOICE_ID != :NEW.INVOICE_ID) THEN v_changed_cols := v_changed_cols || 'INVOICE_ID,'; END IF;
    IF (:OLD.STOCKOUT_ID != :NEW.STOCKOUT_ID) THEN v_changed_cols := v_changed_cols || 'STOCKOUT_ID,'; END IF;
    IF (:OLD.PART_ID != :NEW.PART_ID) THEN v_changed_cols := v_changed_cols || 'PART_ID,'; END IF;
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
    audit_dml_pkg.log_insert('INVOICE_ITEM', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('INVOICE_ITEM', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('INVOICE_ITEM', v_old_values);
  END IF;
END;
/


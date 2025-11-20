-- Audit trigger for INVOICE table to capture before/after values
-- Run as schema owner (e.g., APP)

CREATE OR REPLACE TRIGGER trg_audit_invoice
  AFTER INSERT OR UPDATE OR DELETE ON INVOICE
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
      '"CUSTOMER_PHONE":"' || REPLACE(NVL(:OLD.CUSTOMER_PHONE, ''), '"', '\"') || '",' ||
      '"EMP_ID":' || NVL(TO_CHAR(:OLD.EMP_ID), 'null') || ',' ||
      '"INVOICE_DATE":"' || TO_CHAR(:OLD.INVOICE_DATE, 'YYYY-MM-DD') || '",' ||
      '"TOTAL_AMOUNT":' || NVL(TO_CHAR(:OLD.TOTAL_AMOUNT), 'null') || ',' ||
      '"STATUS":"' || REPLACE(NVL(:OLD.STATUS, ''), '"', '\"') || '"' ||
      '}';
  END IF;
  
  -- Build new values (for INSERT/UPDATE)
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"INVOICE_ID":' || NVL(TO_CHAR(:NEW.INVOICE_ID), 'null') || ',' ||
      '"STOCKOUT_ID":' || NVL(TO_CHAR(:NEW.STOCKOUT_ID), 'null') || ',' ||
      '"CUSTOMER_PHONE":"' || REPLACE(NVL(:NEW.CUSTOMER_PHONE, ''), '"', '\"') || '",' ||
      '"EMP_ID":' || NVL(TO_CHAR(:NEW.EMP_ID), 'null') || ',' ||
      '"INVOICE_DATE":"' || TO_CHAR(:NEW.INVOICE_DATE, 'YYYY-MM-DD') || '",' ||
      '"TOTAL_AMOUNT":' || NVL(TO_CHAR(:NEW.TOTAL_AMOUNT), 'null') || ',' ||
      '"STATUS":"' || REPLACE(NVL(:NEW.STATUS, ''), '"', '\"') || '"' ||
      '}';
  END IF;
  
  -- Track changed columns for UPDATE
  IF UPDATING THEN
    v_changed_cols := '';
    IF (:OLD.STOCKOUT_ID != :NEW.STOCKOUT_ID) THEN v_changed_cols := v_changed_cols || 'STOCKOUT_ID,'; END IF;
    IF ((:OLD.CUSTOMER_PHONE IS NULL AND :NEW.CUSTOMER_PHONE IS NOT NULL) OR 
        (:OLD.CUSTOMER_PHONE IS NOT NULL AND :NEW.CUSTOMER_PHONE IS NULL) OR
        (:OLD.CUSTOMER_PHONE != :NEW.CUSTOMER_PHONE)) THEN 
      v_changed_cols := v_changed_cols || 'CUSTOMER_PHONE,'; 
    END IF;
    IF (:OLD.EMP_ID != :NEW.EMP_ID) THEN v_changed_cols := v_changed_cols || 'EMP_ID,'; END IF;
    IF (:OLD.INVOICE_DATE != :NEW.INVOICE_DATE) THEN v_changed_cols := v_changed_cols || 'INVOICE_DATE,'; END IF;
    IF (:OLD.TOTAL_AMOUNT IS NULL AND :NEW.TOTAL_AMOUNT IS NOT NULL) OR 
       (:OLD.TOTAL_AMOUNT IS NOT NULL AND :NEW.TOTAL_AMOUNT IS NULL) OR
       (:OLD.TOTAL_AMOUNT != :NEW.TOTAL_AMOUNT) THEN
      v_changed_cols := v_changed_cols || 'TOTAL_AMOUNT,';
    END IF;
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
    audit_dml_pkg.log_insert('INVOICE', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('INVOICE', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('INVOICE', v_old_values);
  END IF;
END;
/


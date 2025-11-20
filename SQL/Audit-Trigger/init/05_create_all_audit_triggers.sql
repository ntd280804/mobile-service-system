-- Script to create audit triggers for all tables with before/after values
-- This is a template/example - you may need to customize for each table
-- Run as schema owner (e.g., APP)

-- Note: This script creates triggers for key tables
-- For production, you may want to create individual trigger files per table
-- to better handle table-specific column structures

-- ============================================
-- EMPLOYEE Trigger
-- ============================================
@SQL/Audit/Employee/03_employee_audit_trigger.sql

-- ============================================
-- CUSTOMER Trigger (example - customize as needed)
-- ============================================
CREATE OR REPLACE TRIGGER trg_audit_customer
  AFTER INSERT OR UPDATE OR DELETE ON CUSTOMER
  FOR EACH ROW
DECLARE
  v_old_values CLOB;
  v_new_values CLOB;
  v_changed_cols VARCHAR2(4000);
BEGIN
  IF UPDATING OR DELETING THEN
    v_old_values := '{' ||
      '"PHONE":"' || REPLACE(NVL(:OLD.PHONE, ''), '"', '\"') || '",' ||
      '"FULL_NAME":"' || REPLACE(NVL(:OLD.FULL_NAME, ''), '"', '\"') || '",' ||
      '"EMAIL":"' || REPLACE(NVL(:OLD.EMAIL, ''), '"', '\"') || '",' ||
      '"ADDRESS":"' || REPLACE(NVL(:OLD.ADDRESS, ''), '"', '\"') || '",' ||
      '"CREATED_AT":"' || TO_CHAR(:OLD.CREATED_AT, 'YYYY-MM-DD HH24:MI:SS') || '"' ||
      '}';
  END IF;
  
  IF INSERTING OR UPDATING THEN
    v_new_values := '{' ||
      '"PHONE":"' || REPLACE(NVL(:NEW.PHONE, ''), '"', '\"') || '",' ||
      '"FULL_NAME":"' || REPLACE(NVL(:NEW.FULL_NAME, ''), '"', '\"') || '",' ||
      '"EMAIL":"' || REPLACE(NVL(:NEW.EMAIL, ''), '"', '\"') || '",' ||
      '"ADDRESS":"' || REPLACE(NVL(:NEW.ADDRESS, ''), '"', '\"') || '",' ||
      '"CREATED_AT":"' || TO_CHAR(:NEW.CREATED_AT, 'YYYY-MM-DD HH24:MI:SS') || '"' ||
      '}';
  END IF;
  
  IF UPDATING THEN
    v_changed_cols := '';
    IF (:OLD.FULL_NAME != :NEW.FULL_NAME) THEN v_changed_cols := v_changed_cols || 'FULL_NAME,'; END IF;
    IF (:OLD.EMAIL != :NEW.EMAIL) THEN v_changed_cols := v_changed_cols || 'EMAIL,'; END IF;
    IF (:OLD.ADDRESS != :NEW.ADDRESS) THEN v_changed_cols := v_changed_cols || 'ADDRESS,'; END IF;
    IF LENGTH(v_changed_cols) > 0 THEN
      v_changed_cols := SUBSTR(v_changed_cols, 1, LENGTH(v_changed_cols) - 1);
    END IF;
  END IF;
  
  IF INSERTING THEN
    audit_dml_pkg.log_insert('CUSTOMER', v_new_values);
  ELSIF UPDATING THEN
    audit_dml_pkg.log_update('CUSTOMER', v_old_values, v_new_values, v_changed_cols);
  ELSIF DELETING THEN
    audit_dml_pkg.log_delete('CUSTOMER', v_old_values);
  END IF;
END;
/

-- Note: For other tables (STOCK_IN, STOCK_OUT, PART, etc.), 
-- create similar triggers following the same pattern.
-- Each trigger should:
-- 1. Build old_values CLOB for UPDATE/DELETE
-- 2. Build new_values CLOB for INSERT/UPDATE  
-- 3. Track changed columns for UPDATE
-- 4. Call appropriate audit_dml_pkg procedure


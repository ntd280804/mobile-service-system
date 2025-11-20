-- Add columns to store before/after values in audit_alert_log
-- Run as schema owner (e.g., APP)



-- Create package to log DML with before/after values
CREATE OR REPLACE PACKAGE audit_dml_pkg AS
  -- Log INSERT operation
  PROCEDURE log_insert(
    p_table_name   IN VARCHAR2,
    p_new_values   IN CLOB);
  
  -- Log UPDATE operation  
  PROCEDURE log_update(
    p_table_name   IN VARCHAR2,
    p_old_values   IN CLOB,
    p_new_values   IN CLOB,
    p_changed_cols IN VARCHAR2);
  
  -- Log DELETE operation
  PROCEDURE log_delete(
    p_table_name   IN VARCHAR2,
    p_old_values   IN CLOB);
    
  -- Helper: Convert rowid to JSON-like format (simplified)
  FUNCTION row_to_json(
    p_table_name IN VARCHAR2,
    p_rowid      IN ROWID) RETURN CLOB;
END audit_dml_pkg;
/

CREATE OR REPLACE PACKAGE BODY audit_dml_pkg AS
  
  FUNCTION row_to_json(
    p_table_name IN VARCHAR2,
    p_rowid      IN ROWID) RETURN CLOB IS
    v_result CLOB;
    v_sql    VARCHAR2(4000);
    v_cursor SYS_REFCURSOR;
    v_col_name VARCHAR2(128);
    v_col_value VARCHAR2(4000);
    v_first BOOLEAN := TRUE;
  BEGIN
    -- This is a simplified version
    -- For production, you might want to use DBMS_XMLGEN or dynamic SQL
    -- to get actual column values
    RETURN '{"rowid":"' || p_rowid || '"}';
  END row_to_json;
  
  PROCEDURE log_insert(
    p_table_name   IN VARCHAR2,
    p_new_values   IN CLOB) IS
    v_role VARCHAR2(100) := NVL(SYS_CONTEXT('APP_CTX','ROLE_NAME'),'ROLE_UNKNOWN');
  BEGIN
    INSERT INTO audit_alert_log(
      db_user,
      os_user,
      machine,
      module,
      app_role,
      emp_id,
      customer_phone,
      object_schema,
      object_name,
      policy_name,
      client_identifier,
      dml_type,
      old_values,
      new_values,
      note)
    VALUES(
      SYS_CONTEXT('USERENV','SESSION_USER'),
      SYS_CONTEXT('USERENV','OS_USER'),
      SYS_CONTEXT('USERENV','HOST'),
      SYS_CONTEXT('USERENV','MODULE'),
      v_role,
      SYS_CONTEXT('APP_CTX','EMP_ID'),
      SYS_CONTEXT('APP_CTX','CUSTOMER_PHONE'),
      USER,
      p_table_name,
      'AUD_' || p_table_name || '_INSERT',
      SYS_CONTEXT('USERENV','CLIENT_IDENTIFIER'),
      'INSERT',
      NULL,
      p_new_values,
      'INSERT operation on ' || p_table_name);
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('AUDIT_DML_PKG.LOG_INSERT failed: ' || SQLERRM);
  END log_insert;
  
  PROCEDURE log_update(
    p_table_name   IN VARCHAR2,
    p_old_values   IN CLOB,
    p_new_values   IN CLOB,
    p_changed_cols IN VARCHAR2) IS
    v_role VARCHAR2(100) := NVL(SYS_CONTEXT('APP_CTX','ROLE_NAME'),'ROLE_UNKNOWN');
  BEGIN
    INSERT INTO audit_alert_log(
      db_user,
      os_user,
      machine,
      module,
      app_role,
      emp_id,
      customer_phone,
      object_schema,
      object_name,
      policy_name,
      client_identifier,
      dml_type,
      old_values,
      new_values,
      changed_columns,
      note)
    VALUES(
      SYS_CONTEXT('USERENV','SESSION_USER'),
      SYS_CONTEXT('USERENV','OS_USER'),
      SYS_CONTEXT('USERENV','HOST'),
      SYS_CONTEXT('USERENV','MODULE'),
      v_role,
      SYS_CONTEXT('APP_CTX','EMP_ID'),
      SYS_CONTEXT('APP_CTX','CUSTOMER_PHONE'),
      USER,
      p_table_name,
      'AUD_' || p_table_name || '_UPDATE',
      SYS_CONTEXT('USERENV','CLIENT_IDENTIFIER'),
      'UPDATE',
      p_old_values,
      p_new_values,
      p_changed_cols,
      'UPDATE operation on ' || p_table_name || ' - Changed: ' || p_changed_cols);
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('AUDIT_DML_PKG.LOG_UPDATE failed: ' || SQLERRM);
  END log_update;
  
  PROCEDURE log_delete(
    p_table_name   IN VARCHAR2,
    p_old_values   IN CLOB) IS
    v_role VARCHAR2(100) := NVL(SYS_CONTEXT('APP_CTX','ROLE_NAME'),'ROLE_UNKNOWN');
  BEGIN
    INSERT INTO audit_alert_log(
      db_user,
      os_user,
      machine,
      module,
      app_role,
      emp_id,
      customer_phone,
      object_schema,
      object_name,
      policy_name,
      client_identifier,
      dml_type,
      old_values,
      new_values,
      note)
    VALUES(
      SYS_CONTEXT('USERENV','SESSION_USER'),
      SYS_CONTEXT('USERENV','OS_USER'),
      SYS_CONTEXT('USERENV','HOST'),
      SYS_CONTEXT('USERENV','MODULE'),
      v_role,
      SYS_CONTEXT('APP_CTX','EMP_ID'),
      SYS_CONTEXT('APP_CTX','CUSTOMER_PHONE'),
      USER,
      p_table_name,
      'AUD_' || p_table_name || '_DELETE',
      SYS_CONTEXT('USERENV','CLIENT_IDENTIFIER'),
      'DELETE',
      p_old_values,
      NULL,
      'DELETE operation on ' || p_table_name);
  EXCEPTION
    WHEN OTHERS THEN
      DBMS_OUTPUT.PUT_LINE('AUDIT_DML_PKG.LOG_DELETE failed: ' || SQLERRM);
  END log_delete;
  
END audit_dml_pkg;
/


-- Application Context for VPD on ORDERS
-- Run as schema owner of ORDERS

CREATE OR REPLACE CONTEXT APP_CTX USING APP_CTX_PKG;
/

CREATE OR REPLACE PACKAGE APP_CTX_PKG AS
  PROCEDURE set_role(p_role_name IN VARCHAR2);
  PROCEDURE set_emp(p_emp_id IN NUMBER);
  PROCEDURE set_customer(p_customer_phone IN VARCHAR2);
  PROCEDURE set_username(p_username IN VARCHAR2);
END APP_CTX_PKG;
/

CREATE OR REPLACE PACKAGE BODY APP_CTX_PKG AS
  PROCEDURE set_role(p_role_name IN VARCHAR2) IS
  BEGIN
    DBMS_SESSION.SET_CONTEXT('APP_CTX','ROLE_NAME', UPPER(p_role_name));
  END;

  PROCEDURE set_emp(p_emp_id IN NUMBER) IS
  BEGIN
    DBMS_SESSION.SET_CONTEXT('APP_CTX','EMP_ID', TO_CHAR(p_emp_id));
  END;

  PROCEDURE set_customer(p_customer_phone IN VARCHAR2) IS
  BEGIN
    DBMS_SESSION.SET_CONTEXT('APP_CTX','CUSTOMER_PHONE', p_customer_phone);
  END;
  PROCEDURE set_username(p_username IN VARCHAR2) IS
  BEGIN
    DBMS_SESSION.SET_CONTEXT('APP_CTX','USERNAME', p_username);
  END;
END APP_CTX_PKG;
/
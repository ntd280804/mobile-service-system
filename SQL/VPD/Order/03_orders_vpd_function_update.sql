-- VPD predicate function for UPDATE on ORDERS
-- Rules:
--  - ROLE_ADMIN: update any rows (1=1)
--  - ROLE_KITHUATVIEN: only rows where HANDLER_EMP = :EMP_ID
--  - Others (THUKHO, TIEPTAN, KHACHHANG): not allowed (1=0)
-- Run as schema owner of ORDERS

CREATE OR REPLACE FUNCTION ORDERS_VPD_PREDICATE_UPD(
  p_schema  IN VARCHAR2,
  p_object  IN VARCHAR2
) RETURN VARCHAR2
AS
  v_role VARCHAR2(100) := SYS_CONTEXT('APP_CTX','ROLE_NAME');
  v_emp  VARCHAR2(100) := SYS_CONTEXT('APP_CTX','EMP_ID');
BEGIN
  IF v_role IS NULL THEN
    RETURN '1=0';
  END IF;

  IF v_role = 'ROLE_ADMIN' THEN
    RETURN '1=1';
  END IF;

  IF v_role = 'ROLE_KITHUATVIEN' THEN
    IF v_emp IS NULL THEN
      RETURN '1=0';
    END IF;
    RETURN 'HANDLER_EMP = ' || TO_NUMBER(v_emp);
  END IF;

  RETURN '1=0';
END;
/



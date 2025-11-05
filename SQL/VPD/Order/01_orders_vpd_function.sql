-- VPD predicate function for ORDERS
-- Run as schema owner of ORDERS

CREATE OR REPLACE FUNCTION ORDERS_VPD_PREDICATE(
  p_schema  IN VARCHAR2,
  p_object  IN VARCHAR2
) RETURN VARCHAR2
AS
  v_role VARCHAR2(100) := SYS_CONTEXT('APP_CTX','ROLE_NAME');
  v_emp  VARCHAR2(100) := SYS_CONTEXT('APP_CTX','EMP_ID');
  v_cus  VARCHAR2(100) := SYS_CONTEXT('APP_CTX','CUSTOMER_PHONE');
BEGIN
  IF v_role IS NULL THEN
    RETURN '1=0';
  END IF;

  IF v_role IN ('ROLE_ADMIN','ROLE_TIEPTAN') THEN
    RETURN '1=1';
  END IF;

  IF v_role = 'ROLE_KITHUATVIEN' THEN
    IF v_emp IS NULL THEN
      RETURN '1=0';
    END IF;
    RETURN 'HANDLER_EMP = ' || TO_NUMBER(v_emp);
  END IF;

  IF v_role = 'ROLE_KHACHHANG' THEN
    IF v_cus IS NULL THEN
      RETURN '1=0';
    END IF;
    RETURN 'CUSTOMER_PHONE = ''' || REPLACE(v_cus,'''','''''') || '''';
  END IF;

  IF v_role = 'ROLE_THUKHO' THEN
    RETURN '1=0';
  END IF;

  RETURN '1=0';
END;
/



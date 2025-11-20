-- VPD predicate function for INVOICE and INVOICE_ITEM
-- Assumes CUSTOMER_PHONE column exists on INVOICE (and present on a join/view for items if needed)

CREATE OR REPLACE FUNCTION INVOICE_VPD_PREDICATE(
  p_schema IN VARCHAR2,
  p_object IN VARCHAR2
) RETURN VARCHAR2
AS
  v_role  VARCHAR2(100) := SYS_CONTEXT('APP_CTX','ROLE_NAME');
  v_cus   VARCHAR2(100) := SYS_CONTEXT('APP_CTX','CUSTOMER_PHONE');
BEGIN
  -- ADMIN, TIEPTAN, VERIFY: full
  IF v_role IN ('ROLE_ADMIN', 'ROLE_TIEPTAN', 'ROLE_VERIFY') THEN
    RETURN '1=1';
  END IF;

  -- Khách hàng: only own by phone
  IF v_role = 'ROLE_KHACHHANG' THEN
    RETURN 'CUSTOMER_PHONE = ''' || REPLACE(v_cus,'''','''''') || '''';
  END IF;

  -- THUKHO, KITHUATVIEN and others: deny
  RETURN '1=0';
END INVOICE_VPD_PREDICATE;
/



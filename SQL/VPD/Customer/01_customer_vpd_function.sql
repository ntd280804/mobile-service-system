-- VPD predicate function for CUSTOMER
-- Run as schema owner of CUSTOMER (e.g., APP)

CREATE OR REPLACE FUNCTION CUSTOMER_VPD_PREDICATE(
  p_schema IN VARCHAR2,
  p_object IN VARCHAR2
) RETURN VARCHAR2
AS
  v_role  VARCHAR2(100) := SYS_CONTEXT('APP_CTX','ROLE_NAME');
  v_cus   VARCHAR2(100) := SYS_CONTEXT('APP_CTX','CUSTOMER_PHONE');
BEGIN
  -- ADMIN, TIEPTAN: xem full
  IF v_role IN ('ROLE_ADMIN', 'ROLE_TIEPTAN','ROLE_VERIFY') THEN
    RETURN '1=1';
  END IF;

  -- THUKHO, KITHUATVIEN: không được xem
  IF v_role IN ('ROLE_THUKHO', 'ROLE_KITHUATVIEN') THEN
    RETURN '1=0';
  END IF;

  -- KHACHHANG: chỉ xem của cá nhân theo số điện thoại trong context
  IF v_role = 'ROLE_KHACHHANG' THEN
    -- Cột trên bảng CUSTOMER giả định là PHONE
    RETURN 'PHONE = ''' || REPLACE(v_cus,'''','''''') || '''';
  END IF;

  -- Mặc định: chặn
  RETURN '1=0';
END CUSTOMER_VPD_PREDICATE;
/



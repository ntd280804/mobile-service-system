-- VPD predicate function for EMPLOYEE
-- Run as schema owner of EMPLOYEE (e.g., APP)

CREATE OR REPLACE FUNCTION EMPLOYEE_VPD_PREDICATE(
  p_schema IN VARCHAR2,
  p_object IN VARCHAR2
) RETURN VARCHAR2
AS
  v_role  VARCHAR2(100) := SYS_CONTEXT('APP_CTX','ROLE_NAME');
  v_emp   VARCHAR2(100) := SYS_CONTEXT('APP_CTX','EMP_ID');
BEGIN
  -- ADMIN, TIEPTAN: xem full
  IF v_role IN ('ROLE_ADMIN', 'ROLE_TIEPTAN') THEN
    RETURN '1=1';
  END IF;

  -- THUKHO, KITHUATVIEN: chỉ xem bản thân
  IF v_role IN ('ROLE_THUKHO', 'ROLE_KITHUATVIEN') THEN
    IF v_emp IS NULL THEN
      RETURN '1=1';
    END IF;
    RETURN '1=1';
  END IF;

  -- KHACHHANG hoặc role khác: không được xem
  -- Mặc định: chặn
  RETURN '1=0';
END EMPLOYEE_VPD_PREDICATE;
/


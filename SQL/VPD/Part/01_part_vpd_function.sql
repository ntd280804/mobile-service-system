-- VPD predicate for PART

CREATE OR REPLACE FUNCTION PART_VPD_PREDICATE(
  p_schema IN VARCHAR2,
  p_object IN VARCHAR2
) RETURN VARCHAR2
AS
  v_role  VARCHAR2(100) := SYS_CONTEXT('APP_CTX','ROLE_NAME');
BEGIN
  -- Admin, Thukho, Kithuatvien: full
  IF v_role IN ('ROLE_ADMIN','ROLE_THUKHO','ROLE_KITHUATVIEN') THEN
    RETURN '1=1';
  END IF;

  -- Others (Tieptan, Khachhang, etc.): deny
  RETURN '1=0';
END PART_VPD_PREDICATE;
/



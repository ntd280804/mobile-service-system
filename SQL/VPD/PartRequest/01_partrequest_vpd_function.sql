-- VPD predicate for PART_REQUEST and PART_REQUEST_ITEM
-- Assumes PART_REQUEST has REQUEST_EMP_ID column indicating the employee who created/owns the request

CREATE OR REPLACE FUNCTION PARTREQUEST_VPD_PREDICATE(
  p_schema IN VARCHAR2,
  p_object IN VARCHAR2
) RETURN VARCHAR2
AS
  v_role   VARCHAR2(100) := SYS_CONTEXT('APP_CTX','ROLE_NAME');
  v_emp_id VARCHAR2(100) := SYS_CONTEXT('APP_CTX','EMP_ID');
BEGIN
  -- Admin, Thukho: full
  IF v_role IN ('ROLE_ADMIN','ROLE_THUKHO') THEN
    RETURN '1=1';
  END IF;

  -- Kithuatvien: own rows only
  IF v_role = 'ROLE_KITHUATVIEN' THEN
    RETURN 'EMP_ID = TO_NUMBER(''' || REPLACE(v_emp_id,'''','') || ''')';
  END IF;

  -- Tieptan, Khachhang, others: deny
  RETURN '1=0';
END PARTREQUEST_VPD_PREDICATE;
/



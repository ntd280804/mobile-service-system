-- VPD predicate function for UPDATE on ORDERS
-- Rules:
--  - ROLE_ADMIN: update any rows (1=1)
--  - ROLE_KITHUATVIEN: only rows where HANDLER_EMP = :EMP_ID
--  - Others (THUKHO, TIEPTAN, KHACHHANG): not allowed (1=0)
-- Run as schema owner of ORDERS
-- Fixed: Added error handling for TO_NUMBER and empty strings

CREATE OR REPLACE FUNCTION ORDERS_VPD_PREDICATE_UPD(
  p_schema  IN VARCHAR2,
  p_object  IN VARCHAR2
) RETURN VARCHAR2
AS
  v_role VARCHAR2(100) := SYS_CONTEXT('APP_CTX','ROLE_NAME');
  v_emp  VARCHAR2(100) := SYS_CONTEXT('APP_CTX','EMP_ID');
  v_emp_num NUMBER;
BEGIN
  -- Default deny if role is not set
  IF v_role IS NULL OR TRIM(v_role) = '' THEN
    RETURN '1=0';
  END IF;

  -- ADMIN: full update access
  IF v_role = 'ROLE_ADMIN' THEN
    RETURN '1=1';
  END IF;

  -- KITHUATVIEN: only own rows by HANDLER_EMP
  IF v_role = 'ROLE_KITHUATVIEN' THEN
    IF v_emp IS NULL OR TRIM(v_emp) = '' THEN
      RETURN '1=0';
    END IF;
    -- Safe conversion with error handling
    BEGIN
      v_emp_num := TO_NUMBER(v_emp);
      RETURN 'HANDLER_EMP = ' || v_emp_num;
    EXCEPTION
      WHEN VALUE_ERROR OR OTHERS THEN
        RETURN '1=0';
    END;
  END IF;

  -- All other roles: deny update
  RETURN '1=0';
EXCEPTION
  WHEN OTHERS THEN
    -- Return safe predicate on any error
    RETURN '1=0';
END;
/



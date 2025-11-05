-- VPD predicate function for DELETE on CUSTOMER_APPOINTMENT
-- Deny all roles from deleting any rows
-- Run as schema owner of CUSTOMER_APPOINTMENT

CREATE OR REPLACE FUNCTION APPOINTMENT_VPD_PREDICATE_DEL(
  p_schema  IN VARCHAR2,
  p_object  IN VARCHAR2
) RETURN VARCHAR2
AS
BEGIN
  RETURN '1=0';
END;
/



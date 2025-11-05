-- VPD predicate function for DELETE on ORDERS
-- Deny all roles from deleting any rows
-- Run as schema owner of ORDERS

CREATE OR REPLACE FUNCTION ORDERS_VPD_PREDICATE_DEL(
  p_schema  IN VARCHAR2,
  p_object  IN VARCHAR2
) RETURN VARCHAR2
AS
BEGIN
  RETURN '1=0';
END;
/



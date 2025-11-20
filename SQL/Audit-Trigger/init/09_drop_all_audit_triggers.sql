-- Drop all audit triggers
-- Run as schema owner (e.g., APP)

BEGIN
  -- Employee
  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_employee';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_employee');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_employee not found or already dropped');
  END;

  -- Customer
  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_customer';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_customer');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_customer not found or already dropped');
  END;

  -- Stock
  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_stock_in';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_stock_in');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_stock_in not found or already dropped');
  END;

  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_stock_in_item';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_stock_in_item');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_stock_in_item not found or already dropped');
  END;

  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_stock_out';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_stock_out');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_stock_out not found or already dropped');
  END;

  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_stock_out_item';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_stock_out_item');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_stock_out_item not found or already dropped');
  END;

  -- Part
  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_part';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_part');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_part not found or already dropped');
  END;

  -- PartRequest
  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_part_request';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_part_request');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_part_request not found or already dropped');
  END;

  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_part_request_item';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_part_request_item');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_part_request_item not found or already dropped');
  END;

  -- Invoice
  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_invoice';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_invoice');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_invoice not found or already dropped');
  END;

  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_invoice_item';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_invoice_item');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_invoice_item not found or already dropped');
  END;

  -- Order
  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_orders';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_orders');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_orders not found or already dropped');
  END;

  -- Appointment
  BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_audit_customer_appointment';
    DBMS_OUTPUT.PUT_LINE('Dropped: trg_audit_customer_appointment');
  EXCEPTION WHEN OTHERS THEN
    DBMS_OUTPUT.PUT_LINE('trg_audit_customer_appointment not found or already dropped');
  END;

  DBMS_OUTPUT.PUT_LINE('=== Finished dropping all audit triggers ===');
END;
/


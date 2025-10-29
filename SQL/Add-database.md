-- Kết nối database
sqlplus APP/password@192.168.26.138:1521/ORCLPDB1

-- 1. Thêm UPDATE_APPOINTMENT_STATUS procedure
CREATE OR REPLACE PROCEDURE "APP"."UPDATE_APPOINTMENT_STATUS" (
    p_appointment_id IN NUMBER,
    p_new_status IN VARCHAR2,
    p_result OUT VARCHAR2
) AS
    v_exists NUMBER;
    v_current_status VARCHAR2(20);
BEGIN
    SELECT COUNT(*), MAX(STATUS) 
    INTO v_exists, v_current_status
    FROM CUSTOMER_APPOINTMENT
    WHERE APPOINTMENT_ID = p_appointment_id;
    
    IF v_exists = 0 THEN
        p_result := 'ERROR: Appointment ID ' || p_appointment_id || ' không tồn tại';
        RETURN;
    END IF;
    
    IF p_new_status NOT IN ('SCHEDULED', 'COMPLETED', 'CANCELLED') THEN
        p_result := 'ERROR: Status không hợp lệ';
        RETURN;
    END IF;
    
    UPDATE CUSTOMER_APPOINTMENT
    SET STATUS = p_new_status
    WHERE APPOINTMENT_ID = p_appointment_id;
    
    p_result := 'SUCCESS';
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_result := 'ERROR: ' || SQLERRM;
END;
/

-- 2. Cập nhật CREATE_APPOINTMENT (thêm validation)
CREATE OR REPLACE PROCEDURE "APP"."CREATE_APPOINTMENT" (
    p_customer_phone IN VARCHAR2,
    p_appointment_date IN DATE,
    p_description IN VARCHAR2 DEFAULT NULL,
    p_status OUT VARCHAR2
) AS
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM CUSTOMER WHERE PHONE = p_customer_phone;
    IF v_exists = 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Khách hàng không tồn tại');
    END IF;
    
    INSERT INTO CUSTOMER_APPOINTMENT VALUES (
        APPOINTMENT_SEQ.NEXTVAL, p_customer_phone, p_appointment_date, 'SCHEDULED', p_description
    );
    p_status := 'SCHEDULED';
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(-20001, 'Không thể tạo lịch: ' || SQLERRM);
END;
/

-- 3. Cập nhật GET_ALL_APPOINTMENTS (thêm customer name)
CREATE OR REPLACE PROCEDURE "APP"."GET_ALL_APPOINTMENTS" (
    p_cursor OUT SYS_REFCURSOR
) AS
BEGIN
    OPEN p_cursor FOR
        SELECT a.APPOINTMENT_ID, a.CUSTOMER_PHONE, c.FULL_NAME AS CUSTOMER_NAME,
               a.APPOINTMENT_DATE, a.STATUS, a.DESCRIPTION
        FROM CUSTOMER_APPOINTMENT a
        LEFT JOIN CUSTOMER c ON a.CUSTOMER_PHONE = c.PHONE
        ORDER BY a.APPOINTMENT_DATE DESC;
END;
/

-- 4. GRANT quyền
GRANT EXECUTE ON APP.CREATE_APPOINTMENT TO PUBLIC;
GRANT EXECUTE ON APP.GET_APPOINTMENTS_BY_PHONE TO PUBLIC;
GRANT EXECUTE ON APP.GET_ALL_APPOINTMENTS TO PUBLIC;
GRANT EXECUTE ON APP.UPDATE_APPOINTMENT_STATUS TO PUBLIC;
GRANT SELECT, INSERT ON APP.CUSTOMER_APPOINTMENT TO PUBLIC;
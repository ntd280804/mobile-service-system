-- Stored Procedure để Generate OTP cho Customer quên mật khẩu
CREATE OR REPLACE PROCEDURE GENERATE_OTP_CUSTOMER(
    p_phone IN VARCHAR2,
    p_otp OUT VARCHAR2,
    p_result OUT VARCHAR2
)
AS
    v_customer_exists NUMBER;
    v_user_id NUMBER;
    v_otp_code VARCHAR2(10);
    v_expired_at TIMESTAMP;
BEGIN
    -- Kiểm tra customer có tồn tại không
    SELECT COUNT(*) INTO v_customer_exists
    FROM CUSTOMER
    WHERE PHONE = p_phone;
    
    IF v_customer_exists = 0 THEN
        p_result := 'Số điện thoại không tồn tại trong hệ thống.';
        p_otp := NULL;
        RETURN;
    END IF;
    
    -- Lấy USER_ID từ CUSTOMER (giả sử có mapping, nếu không thì dùng 0 hoặc tạo mapping)
    -- Vì USER_OTP_LOG cần USER_ID, ta sẽ dùng 0 hoặc tạo một mapping
    -- Ở đây ta sẽ dùng 0 vì customer không có EMP_ID
    v_user_id := 0;
    
    -- Generate OTP 6 chữ số
    v_otp_code := LPAD(TRUNC(DBMS_RANDOM.VALUE(100000, 999999)), 6, '0');
    v_expired_at := SYSTIMESTAMP + INTERVAL '10' MINUTE; -- OTP hết hạn sau 10 phút
    
    -- Insert OTP vào USER_OTP_LOG
    INSERT INTO USER_OTP_LOG (USER_ID, USERNAME, OTP, CREATED_AT, EXPIRED_AT, USED)
    VALUES (v_user_id, p_phone, v_otp_code, SYSTIMESTAMP, v_expired_at, 'N');
    
    p_otp := v_otp_code;
    p_result := 'OTP đã được tạo thành công.';
    
    COMMIT;
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_result := 'Lỗi khi tạo OTP: ' || SQLERRM;
        p_otp := NULL;
        RAISE;
END GENERATE_OTP_CUSTOMER;
/

-- Stored Procedure để Reset Password với OTP verification
CREATE OR REPLACE PROCEDURE RESET_PASSWORD_CUSTOMER(
    p_phone IN VARCHAR2,
    p_otp IN VARCHAR2,
    p_new_password IN VARCHAR2,
    p_result OUT VARCHAR2
)
AS
    v_customer_exists NUMBER;
    v_otp_valid NUMBER;
    v_otp_used CHAR(1);
    v_expired_at TIMESTAMP;
    v_new_hash VARCHAR2(256);
    v_pwd VARCHAR2(20);
BEGIN
    -- 1. Kiểm tra customer có tồn tại không
    SELECT COUNT(*) INTO v_customer_exists
    FROM CUSTOMER
    WHERE PHONE = p_phone;
    
    IF v_customer_exists = 0 THEN
        p_result := 'Số điện thoại không tồn tại trong hệ thống.';
        RETURN;
    END IF;
    
    -- 2. Kiểm tra OTP có hợp lệ không (chưa dùng, chưa hết hạn, đúng mã)
    SELECT COUNT(*), MAX(USED), MAX(EXPIRED_AT)
    INTO v_otp_valid, v_otp_used, v_expired_at
    FROM USER_OTP_LOG
    WHERE USERNAME = p_phone
      AND OTP = p_otp
      AND USED = 'N'
      AND EXPIRED_AT > SYSTIMESTAMP
    ORDER BY CREATED_AT DESC
    FETCH FIRST 1 ROW ONLY;
    
    IF v_otp_valid = 0 THEN
        p_result := 'OTP không hợp lệ, đã hết hạn hoặc đã được sử dụng.';
        RETURN;
    END IF;
    
    -- 3. Hash mật khẩu mới (tương tự như CHANGE_CUSTOMER_PASSWORD)
    v_new_hash := HASH_PASSWORD(p_new_password);
    v_pwd := HASH_PASSWORD_20CHARS(p_new_password);
    
    -- 4. Cập nhật mật khẩu trong bảng CUSTOMER
    BEGIN
        UPDATE CUSTOMER
        SET PASSWORD_HASH = v_new_hash
        WHERE PHONE = p_phone;
        
        IF SQL%ROWCOUNT = 0 THEN
            p_result := 'Không thể cập nhật mật khẩu.';
            RETURN;
        END IF;
    EXCEPTION
        WHEN OTHERS THEN
            ROLLBACK;
            p_result := 'Lỗi khi cập nhật mật khẩu: ' || SQLERRM;
            RETURN;
    END;
    
    -- 5. Cập nhật Oracle DB user password
    BEGIN
        EXECUTE IMMEDIATE 'ALTER USER "' || p_phone || '" IDENTIFIED BY "' || v_pwd || '"';
    EXCEPTION
        WHEN OTHERS THEN
            -- Nếu không thể update DB user password, vẫn tiếp tục (có thể user không tồn tại)
            NULL;
    END;
    
    -- 6. Đánh dấu OTP đã sử dụng
    UPDATE USER_OTP_LOG
    SET USED = 'Y'
    WHERE USERNAME = p_phone
      AND OTP = p_otp
      AND USED = 'N';
    
    p_result := 'Đặt lại mật khẩu thành công.';
    
    COMMIT;
    
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_result := 'Lỗi khi đặt lại mật khẩu: ' || SQLERRM;
        RAISE;
END RESET_PASSWORD_CUSTOMER;
/

-- Grant quyền thực thi
GRANT EXECUTE ON APP.GENERATE_OTP_CUSTOMER TO ROLE_KHACHHANG;
GRANT EXECUTE ON APP.RESET_PASSWORD_CUSTOMER TO ROLE_KHACHHANG;


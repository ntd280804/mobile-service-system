create or replace PROCEDURE REGISTER_EMPLOYEE(
    p_empid     IN NUMBER DEFAULT NULL,   -- Nếu NULL, để Oracle tự sinh
    p_full_name IN VARCHAR2,
    p_username  IN VARCHAR2,
    p_password  IN VARCHAR2,
    p_email     IN VARCHAR2,
    p_phone     IN VARCHAR2
)
AS
    v_hash        VARCHAR2(256);
    v_pub         CLOB;
    v_priv        CLOB;
    v_empid_used  NUMBER;
BEGIN
    -- Hash mật khẩu
    v_hash := HASH_PASSWORD(p_password);

    -- Sinh cặp RSA
    RSA_GENERATE_KEYPAIR(2048);
    v_pub  := RSA_GET_PUBLIC_KEY();
    v_priv := RSA_GET_PRIVATE_KEY();

    -- 1. Tạo Oracle DB user trước
    BEGIN
        EXECUTE IMMEDIATE 'CREATE USER ' || p_username ||
                  ' IDENTIFIED BY "' || p_password || '" DEFAULT TABLESPACE USERS QUOTA UNLIMITED ON USERS';
        EXECUTE IMMEDIATE 'GRANT CONNECT, RESOURCE TO ' || p_username;

        -- Gán quyền cần thiết
        EXECUTE IMMEDIATE 'GRANT SELECT, UPDATE ON EMPLOYEE TO ' || p_username;
        EXECUTE IMMEDIATE 'GRANT EXECUTE ON REGISTER_EMPLOYEE TO ' || p_username;
        EXECUTE IMMEDIATE 'GRANT EXECUTE ON GET_ALL_EMPLOYEES TO ' || p_username;
        EXECUTE IMMEDIATE 'GRANT EXECUTE ON GET_EMPLOYEE_BY_ID TO ' || p_username;

    EXCEPTION
        WHEN OTHERS THEN
            -- Nếu tạo user DB bị lỗi → dừng toàn bộ
            RAISE_APPLICATION_ERROR(-20001, 'Không thể tạo DB User cho nhân viên: ' || SQLERRM);
    END;

    -- 2. Nếu DB user tạo thành công → insert vào EMPLOYEE
    IF p_empid IS NULL THEN
        INSERT INTO EMPLOYEE(FULL_NAME, USERNAME, PASSWORD_HASH, PUBLIC_KEY, EMAIL, PHONE)
        VALUES (p_full_name, p_username, v_hash, v_pub, p_email, p_phone)
        RETURNING EMP_ID INTO v_empid_used;
    ELSE
        INSERT INTO EMPLOYEE(EMP_ID, FULL_NAME, USERNAME, PASSWORD_HASH, PUBLIC_KEY, EMAIL, PHONE)
        VALUES (p_empid, p_full_name, p_username, v_hash, v_pub, p_email, p_phone);
        v_empid_used := p_empid;
    END IF;

    -- 3. Lưu private key
    INSERT INTO EMPLOYEE_KEYS(EMP_ID, PRIVATE_KEY)
    VALUES (v_empid_used, v_priv);
    
    COMMIT;

EXCEPTION
    WHEN OTHERS THEN
        -- Nếu lỗi sau khi tạo user DB, xóa user để rollback sạch
        BEGIN
            EXECUTE IMMEDIATE 'DROP USER ' || p_username || ' CASCADE';
        EXCEPTION
            WHEN OTHERS THEN NULL;
        END;
        RAISE;
END;
/



create or replace PROCEDURE LOGIN_CUSTOMER(
    p_phone        IN  VARCHAR2,
    p_password     IN  VARCHAR2,
    p_out_phone    OUT VARCHAR2,
    p_out_result   OUT VARCHAR2
) AS
    PRAGMA AUTONOMOUS_TRANSACTION;

    v_hash       VARCHAR2(256);
    v_db_hash    VARCHAR2(256);
    v_status     VARCHAR2(10);
    v_fail_count NUMBER;
BEGIN
    v_hash := HASH_PASSWORD(p_password);

    BEGIN
        SELECT PASSWORD_HASH, STATUS
        INTO v_db_hash, v_status
        FROM CUSTOMER
        WHERE PHONE = p_phone;
        p_out_phone := p_phone;

        IF v_status = 'LOCKED' THEN
            p_out_result := 'ACCOUNT LOCKED';
            RETURN;
        END IF;

        IF v_db_hash = v_hash THEN
            p_out_result := 'SUCCESS';
        ELSE
            p_out_result := 'FAILED';
        END IF;

    EXCEPTION
        WHEN NO_DATA_FOUND THEN

            p_out_phone := NULL;
            
            p_out_result := 'FAILED';
    END;
END LOGIN_CUSTOMER;
/


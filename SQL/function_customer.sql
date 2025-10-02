CREATE OR REPLACE PROCEDURE REGISTER_CUSTOMER(
    p_phone     IN VARCHAR2,   -- dùng làm PK
    p_full_name IN VARCHAR2,
    p_password  IN VARCHAR2,
    p_email     IN VARCHAR2,
    p_address   IN VARCHAR2
)
AS
    v_hash VARCHAR2(256);
    v_pub  CLOB;
    v_priv CLOB;
BEGIN
    -- Hash mật khẩu
    v_hash := HASH_PASSWORD(p_password);

    -- Sinh cặp RSA
    RSA_GENERATE_KEYPAIR(2048);

    -- Lấy public/private key
    v_pub  := RSA_GET_PUBLIC_KEY();
    v_priv := RSA_GET_PRIVATE_KEY();

    -- Insert customer
    INSERT INTO CUSTOMER(PHONE, FULL_NAME, PASSWORD_HASH, PUBLIC_KEY, EMAIL, ADDRESS, STATUS)
    VALUES (p_phone, p_full_name, v_hash, v_pub, p_email, p_address, 'ACTIVE');
	
	BEGIN
        EXECUTE IMMEDIATE 'CREATE USER ' || p_phone || 
                  ' IDENTIFIED BY "' || p_password || '" DEFAULT TABLESPACE USERS QUOTA UNLIMITED ON USERS';
        EXECUTE IMMEDIATE 'GRANT CONNECT, RESOURCE TO ' || p_phone;
        
    EXCEPTION
        WHEN OTHERS THEN
            -- Nếu user đã tồn tại hoặc lỗi privilege, bỏ qua
            NULL;
    END;
    -- Lưu private key vào CUSTOMER_KEYS
    INSERT INTO CUSTOMER_KEYS(CUSTOMER_ID, PRIVATE_KEY)
    VALUES (p_phone, v_priv);

    -- Audit (cột USERNAME trong audit table thực ra sẽ lưu số điện thoại)
    INSERT INTO CUSTOMER_LOGIN_AUDIT(USERNAME, STATUS)
    VALUES (p_phone, 'REGISTERED');

    COMMIT;

EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        INSERT INTO CUSTOMER_LOGIN_AUDIT(USERNAME, STATUS)
        VALUES (p_phone, 'DUPLICATE');
        COMMIT;
    WHEN OTHERS THEN
        INSERT INTO CUSTOMER_LOGIN_AUDIT(USERNAME, STATUS)
        VALUES (p_phone, 'FAILED_REGISTER');
        COMMIT;
END REGISTER_CUSTOMER;
/



-- Login customer
CREATE OR REPLACE PROCEDURE LOGIN_CUSTOMER(
    p_phone        IN  VARCHAR2,
    p_password     IN  VARCHAR2,
    p_out_phone    OUT VARCHAR2,
    p_out_role     OUT VARCHAR2,
    p_out_result   OUT VARCHAR2
) AS
    PRAGMA AUTONOMOUS_TRANSACTION;

    v_hash       VARCHAR2(256);
    v_db_hash    VARCHAR2(256);
    v_role       VARCHAR2(20);
    v_status     VARCHAR2(10);
    v_fail_count NUMBER;
BEGIN
    v_hash := HASH_PASSWORD(p_password);

    BEGIN
        SELECT PASSWORD_HASH, 'CUSTOMER', STATUS
        INTO v_db_hash, v_role, v_status
        FROM CUSTOMER
        WHERE PHONE = p_phone;

        p_out_phone := p_phone;
        p_out_role := v_role;

        IF v_status = 'LOCKED' THEN
            INSERT INTO CUSTOMER_LOGIN_AUDIT(USERNAME, STATUS)
            VALUES (p_phone, 'LOCKED');
            COMMIT;
            p_out_result := 'ACCOUNT LOCKED';
            RETURN;
        END IF;

        IF v_db_hash = v_hash THEN
            INSERT INTO CUSTOMER_LOGIN_AUDIT(USERNAME, STATUS)
            VALUES (p_phone, 'SUCCESS');
            COMMIT;
            p_out_result := 'SUCCESS';
        ELSE
            INSERT INTO CUSTOMER_LOGIN_AUDIT(USERNAME, STATUS)
            VALUES (p_phone, 'FAIL');
            COMMIT;

            SELECT COUNT(*) INTO v_fail_count
            FROM (
                SELECT STATUS
                FROM CUSTOMER_LOGIN_AUDIT
                WHERE USERNAME = p_phone
                ORDER BY LOGIN_TIME DESC
            )
            WHERE ROWNUM <= 3 AND STATUS = 'FAIL';

            IF v_fail_count >= 3 THEN
                UPDATE CUSTOMER
                SET STATUS = 'LOCKED'
                WHERE PHONE = p_phone;
                COMMIT;
                p_out_result := 'ACCOUNT LOCKED';
            ELSE
                p_out_result := 'FAILED';
            END IF;
        END IF;

    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            INSERT INTO CUSTOMER_LOGIN_AUDIT(USERNAME, STATUS)
            VALUES (p_phone, 'NO CUSTOMER');
            COMMIT;
            p_out_phone := NULL;
            p_out_role := NULL;
            p_out_result := 'FAILED';
    END;
END LOGIN_CUSTOMER;
/


-- Unlock customer
CREATE OR REPLACE PROCEDURE UNLOCK_CUSTOMER(
    p_phone      IN  VARCHAR2,
    p_out_result OUT VARCHAR2
)
AS
BEGIN
    -- Kiểm tra customer tồn tại
    DECLARE
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM CUSTOMER
        WHERE PHONE = p_phone;

        IF v_count = 0 THEN
            p_out_result := 'NO CUSTOMER';
            RETURN;
        END IF;
    END;

    -- Cập nhật trạng thái ACTIVE
    UPDATE CUSTOMER
    SET STATUS = 'ACTIVE'
    WHERE PHONE = p_phone;
	INSERT INTO CUSTOMER_LOGIN_AUDIT(USERNAME, STATUS)
    VALUES (p_phone, 'UNLOCK');
    COMMIT;

    p_out_result := 'UNLOCKED';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_out_result := 'FAILED: ' || SQLERRM;
END UNLOCK_CUSTOMER;
/

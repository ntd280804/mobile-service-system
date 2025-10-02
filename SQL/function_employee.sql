create or replace PROCEDURE REGISTER_EMPLOYEE(
    p_empid     IN NUMBER DEFAULT NULL,   -- Nếu NULL, để Oracle tự sinh
    p_full_name IN VARCHAR2,
    p_username  IN VARCHAR2,
    p_password  IN VARCHAR2,
    p_email     IN VARCHAR2,
    p_phone     IN VARCHAR2,
    p_role      IN VARCHAR2
)
AS
    v_hash        VARCHAR2(256);
    v_pub         CLOB;
    v_priv        CLOB;
    v_empid_used  NUMBER;
BEGIN
    -- Hash mật khẩu
    v_hash := HASH_PASSWORD(p_password);

    -- Sinh cặp RSA (Java)
    RSA_GENERATE_KEYPAIR(2048);

    -- Lấy public/private key
    v_pub  := RSA_GET_PUBLIC_KEY();
    v_priv := RSA_GET_PRIVATE_KEY();

    -- Insert employee
    IF p_empid IS NULL THEN
        INSERT INTO EMPLOYEE(FULL_NAME, USERNAME, PASSWORD_HASH, PUBLIC_KEY, EMAIL, PHONE, ROLE)
        VALUES (p_full_name, p_username, v_hash, v_pub, p_email, p_phone, p_role)
        RETURNING EMP_ID INTO v_empid_used;
    ELSE
        INSERT INTO EMPLOYEE(EMP_ID, FULL_NAME, USERNAME, PASSWORD_HASH, PUBLIC_KEY, EMAIL, PHONE, ROLE)
        VALUES (p_empid, p_full_name, p_username, v_hash, v_pub, p_email, p_phone, p_role);
        v_empid_used := p_empid;
    END IF;

    -- Tạo Oracle DB user (chỉ tạo nếu insert thành công)
    BEGIN
        EXECUTE IMMEDIATE 'CREATE USER ' || p_username || 
                  ' IDENTIFIED BY "' || p_password || '" DEFAULT TABLESPACE USERS QUOTA UNLIMITED ON USERS';
        EXECUTE IMMEDIATE 'GRANT CONNECT, RESOURCE TO ' || p_username;
        EXECUTE IMMEDIATE 'GRANT SELECT, UPDATE ON EMPLOYEE TO ' || p_username;
        EXECUTE IMMEDIATE 'GRANT EXECUTE ON REGISTER_EMPLOYEE TO ' || p_username;
        EXECUTE IMMEDIATE 'GRANT EXECUTE ON GET_ALL_EMPLOYEES TO ' || p_username;
        EXECUTE IMMEDIATE 'GRANT EXECUTE ON GET_EMPLOYEE_BY_ID TO ' || p_username;
        
    EXCEPTION
        WHEN OTHERS THEN
            -- Nếu user đã tồn tại hoặc lỗi privilege, bỏ qua
            NULL;
    END;

    -- Lưu private key
    INSERT INTO EMPLOYEE_KEYS(EMP_ID, PRIVATE_KEY) VALUES (v_empid_used, v_priv);

    -- Audit đăng ký
    INSERT INTO EMPLOYEE_LOGIN_AUDIT(USERNAME, STATUS) VALUES (p_username, 'REGISTERED');
    COMMIT;

EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        INSERT INTO EMPLOYEE_LOGIN_AUDIT(USERNAME, STATUS) VALUES (p_username, 'DUPLICATE');
        COMMIT;
    WHEN OTHERS THEN
        INSERT INTO EMPLOYEE_LOGIN_AUDIT(USERNAME, STATUS) VALUES (p_username, 'FAILED_REGISTER');
        COMMIT;
END;
/

CREATE OR REPLACE PROCEDURE LOGIN_EMPLOYEE(
    p_username     IN  VARCHAR2,
    p_password     IN  VARCHAR2,
    p_out_username OUT VARCHAR2,
    p_out_role     OUT VARCHAR2,
    p_out_result   OUT VARCHAR2
) AS
    PRAGMA AUTONOMOUS_TRANSACTION;

    v_hash       VARCHAR2(256);
    v_db_hash    VARCHAR2(256);
    v_role       EMPLOYEE.ROLE%TYPE;
    v_status     EMPLOYEE.STATUS%TYPE;
    v_fail_count NUMBER;
BEGIN
    v_hash := HASH_PASSWORD(p_password);

    BEGIN
        SELECT PASSWORD_HASH, ROLE, STATUS
        INTO v_db_hash, v_role, v_status
        FROM EMPLOYEE
        WHERE USERNAME = p_username;

        p_out_username := p_username;
        p_out_role := v_role;

        IF v_status = 'LOCKED' THEN
            INSERT INTO EMPLOYEE_LOGIN_AUDIT(USERNAME, STATUS)
            VALUES (p_username, 'LOCKED');
            COMMIT;
            p_out_result := 'ACCOUNT LOCKED';
            RETURN;
        END IF;

        IF v_db_hash = v_hash THEN
            -- Login thành công: reset fail count logic không cần, chỉ insert SUCCESS
            INSERT INTO EMPLOYEE_LOGIN_AUDIT(USERNAME, STATUS)
            VALUES (p_username, 'SUCCESS');
            COMMIT;
            p_out_result := 'SUCCESS';
        ELSE
            -- Login fail: insert FAIL
            INSERT INTO EMPLOYEE_LOGIN_AUDIT(USERNAME, STATUS)
            VALUES (p_username, 'FAIL');
            COMMIT;

            -- Đếm 3 lần FAIL liên tiếp gần nhất
            SELECT COUNT(*) INTO v_fail_count
            FROM (
                SELECT STATUS
                FROM EMPLOYEE_LOGIN_AUDIT
                WHERE USERNAME = p_username
                ORDER BY LOGIN_TIME DESC
            )
            WHERE ROWNUM <= 3 AND STATUS = 'FAIL';

            -- Nếu >=3 lần fail liên tiếp, lock account
            IF v_fail_count >= 3 THEN
                UPDATE EMPLOYEE
                SET STATUS = 'LOCKED'
                WHERE USERNAME = p_username;
                COMMIT;
                p_out_result := 'ACCOUNT LOCKED';
            ELSE
                p_out_result := 'FAILED';
            END IF;
        END IF;

    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            INSERT INTO EMPLOYEE_LOGIN_AUDIT(USERNAME, STATUS)
            VALUES (p_username, 'NO EMPLOYEE');
            COMMIT;
            p_out_username := NULL;
            p_out_role := NULL;
            p_out_result := 'FAILED';
    END;
END LOGIN_EMPLOYEE;
/
CREATE OR REPLACE PROCEDURE UNLOCK_EMPLOYEE(
    p_username   IN  VARCHAR2,
    p_out_result OUT VARCHAR2
) AS
BEGIN
    -- Kiểm tra user tồn tại
    DECLARE
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM EMPLOYEE
        WHERE USERNAME = p_username;

        IF v_count = 0 THEN
            p_out_result := 'NO EMPLOYEE';
            RETURN;
        END IF;
    END;

    -- Cập nhật trạng thái ACTIVE
    UPDATE EMPLOYEE
    SET STATUS = 'ACTIVE'
    WHERE USERNAME = p_username;
	
	INSERT INTO EMPLOYEE_LOGIN_AUDIT(USERNAME, STATUS)
    VALUES (p_username, 'UNLOCK');
    COMMIT;

    p_out_result := 'UNLOCKED';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_out_result := 'FAILED: ' || SQLERRM;
END UNLOCK_EMPLOYEE;
/
CREATE OR REPLACE PROCEDURE LOCK_EMPLOYEE(
    p_username   IN  VARCHAR2,
    p_out_result OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    -- Kiểm tra tồn tại
    SELECT COUNT(*) INTO v_count
    FROM EMPLOYEE
    WHERE USERNAME = p_username;

    IF v_count = 0 THEN
        p_out_result := 'NO EMPLOYEE';
        RETURN;
    END IF;

    -- Cập nhật trạng thái LOCKED
    UPDATE EMPLOYEE
    SET STATUS = 'LOCKED'
    WHERE USERNAME = p_username;

    INSERT INTO EMPLOYEE_LOGIN_AUDIT(USERNAME, STATUS)
    VALUES (p_username, 'LOCK');
    COMMIT;

    p_out_result := 'LOCKED';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_out_result := 'FAILED: ' || SQLERRM;
END LOCK_EMPLOYEE;
/

CREATE OR REPLACE PROCEDURE GET_ALL_EMPLOYEES(
    p_cursor OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_cursor FOR
        SELECT EMP_ID,
               FULL_NAME,
               USERNAME,
               EMAIL,
               PHONE,
               ROLE,
               STATUS
        FROM APP.EMPLOYEE;
END;
/
CREATE OR REPLACE PROCEDURE GET_EMPLOYEE_BY_ID(
    p_empid  IN  NUMBER,
    p_cursor OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_cursor FOR
        SELECT EMP_ID,
               FULL_NAME,
               USERNAME,
               EMAIL,
               PHONE,
               ROLE,
               STATUS
        FROM APP.EMPLOYEE
        WHERE EMP_ID = p_empid;
END;
/

BEGIN
    REGISTER_EMPLOYEE(
        p_full_name => 'Admin1',
        p_username  => 'Admin1',
        p_password  => 'Admin1',
        p_email     => 'Admin1@example.com',
        p_phone     => '0909123456',
        p_role      => 'ADMIN'
    );
END;
/
CREATE USER AdminTest IDENTIFIED BY AdminTest
DEFAULT TABLESPACE USERS
TEMPORARY TABLESPACE TEMP
ACCOUNT UNLOCK;
GRANT CREATE SESSION TO AdminTest;
--------------------------------------------------------------------------------
-- A. Hash password function (DBMS_CRYPTO)
--    Trả về HEX string (64 ký tự)
--------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION HASH_PASSWORD(p_password VARCHAR2) RETURN VARCHAR2 IS
    v_raw RAW(32);
BEGIN
    v_raw := DBMS_CRYPTO.HASH(
        UTL_I18N.STRING_TO_RAW(p_password, 'AL32UTF8'),
        DBMS_CRYPTO.HASH_SH256
    );
    RETURN RAWTOHEX(v_raw);
END;
/

-- Nếu DBMS_CRYPTO không có quyền cho schema, grant bằng SYS:
-- GRANT EXECUTE ON DBMS_CRYPTO TO <schema>;

--------------------------------------------------------------------------------
-- B. Employee Registration Procedure (tạo RSA keypair, lưu public/private)
--------------------------------------------------------------------------------
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
    v_hash   VARCHAR2(256);
    v_pub    CLOB;
    v_priv   CLOB;
    v_empid_used NUMBER;
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
    -- EMP_ID tự sinh bằng IDENTITY / sequence
    INSERT INTO EMPLOYEE(FULL_NAME, USERNAME, PASSWORD_HASH, PUBLIC_KEY, EMAIL, PHONE, ROLE)
    VALUES (p_full_name, p_username, v_hash, v_pub, p_email, p_phone, p_role)
    RETURNING EMP_ID INTO v_empid_used;
ELSE
    -- EMP_ID truyền vào, không cần RETURNING
    INSERT INTO EMPLOYEE(EMP_ID, FULL_NAME, USERNAME, PASSWORD_HASH, PUBLIC_KEY, EMAIL, PHONE, ROLE)
    VALUES (p_empid, p_full_name, p_username, v_hash, v_pub, p_email, p_phone, p_role);
    v_empid_used := p_empid;
END IF;


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

--------------------------------------------------------------------------------
-- C. Function GET_PRIVATE_KEY (chỉ NGCHOAN mới xem được)
--------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION GET_PRIVATE_KEY(p_employeeid IN NUMBER) RETURN CLOB AS
    v_priv CLOB;
BEGIN
    IF SYS_CONTEXT('USERENV','SESSION_USER') <> 'NGCHOAN' THEN
        RAISE_APPLICATION_ERROR(-20001, 'Access denied: only NGCHOAN can view private keys');
    END IF;

    SELECT PRIVATE_KEY INTO v_priv FROM EMPLOYEE_KEYS WHERE EMP_ID = p_employeeid;
    RETURN v_priv;
END;
/

--------------------------------------------------------------------------------
-- D. LOGIN function + ghi audit
--------------------------------------------------------------------------------
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

    COMMIT;

    p_out_result := 'UNLOCKED';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_out_result := 'FAILED: ' || SQLERRM;
END UNLOCK_EMPLOYEE;
/


SET SERVEROUTPUT ON;

DECLARE
    v_hash VARCHAR2(256);
BEGIN
    v_hash := HASH_PASSWORD('Pass1234!');
    DBMS_OUTPUT.PUT_LINE('Hashed password: ' || v_hash);
END;
/
DECLARE
    v_role VARCHAR2(50);
BEGIN
    v_role := LOGIN_EMPLOYEE('nguyenvanf2', 'Pass1234!');
    DBMS_OUTPUT.PUT_LINE('Login role: ' || v_role);
END;
/


BEGIN
    -- Test 1: EMP_ID tự sinh
    REGISTER_EMPLOYEE(
    p_empid     => 3,
        p_full_name => 'Nguyen Van A',
        p_username  => 'nguyenvanc',
        p_password  => 'Pass1234!',
        p_email     => 'nguyenvanc@example.com',
        p_phone     => '0909123451',
        p_role      => 'ADMIN'
    );
END;
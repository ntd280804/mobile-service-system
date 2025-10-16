create or replace PROCEDURE REGISTER_CUSTOMER(
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
    v_pub  := RSA_GET_PUBLIC_KEY();
    v_priv := RSA_GET_PRIVATE_KEY();

    -- 1. Tạo Oracle DB user trước
    BEGIN
        EXECUTE IMMEDIATE 'CREATE USER "' || p_phone || '"' ||
                          ' IDENTIFIED BY "' || p_password || '"' ||
                          ' DEFAULT TABLESPACE USERS QUOTA UNLIMITED ON USERS';

        EXECUTE IMMEDIATE 'GRANT CONNECT, RESOURCE TO "' || p_phone || '"';
        -- Nếu cần có thể gán thêm quyền select/execute tùy mục đích sử dụng
    EXCEPTION
        WHEN OTHERS THEN
            RAISE_APPLICATION_ERROR(-20002, 'Không thể tạo DB User cho Customer ' || p_phone || ': ' || SQLERRM);
    END;

    -- 2. Insert vào bảng CUSTOMER (chỉ khi tạo DB user thành công)
    BEGIN
        INSERT INTO CUSTOMER(PHONE, FULL_NAME, PASSWORD_HASH, PUBLIC_KEY, EMAIL, ADDRESS, STATUS)
        VALUES (p_phone, p_full_name, v_hash, v_pub, p_email, p_address, 'ACTIVE');

        -- 3. Lưu private key
        INSERT INTO CUSTOMER_KEYS(CUSTOMER_ID, PRIVATE_KEY)
        VALUES (p_phone, v_priv);

        COMMIT;
    EXCEPTION
        WHEN OTHERS THEN
            -- Nếu lỗi ở bước insert thì rollback + xóa DB user vừa tạo
            ROLLBACK;
            BEGIN
                EXECUTE IMMEDIATE 'DROP USER "' || p_phone || '" CASCADE';
            EXCEPTION
                WHEN OTHERS THEN NULL;
            END;
            RAISE;
    END;

END REGISTER_CUSTOMER;
/

create or replace PROCEDURE LOGIN_EMPLOYEE(
    p_username     IN  VARCHAR2,
    p_password     IN  VARCHAR2,
    p_out_username OUT VARCHAR2,
    p_out_result   OUT VARCHAR2,
    p_out_role     OUT VARCHAR2
) AS
    PRAGMA AUTONOMOUS_TRANSACTION;

    v_hash       VARCHAR2(256);
    v_db_hash    VARCHAR2(256);
    v_status     VARCHAR2(30);
BEGIN
    v_hash := HASH_PASSWORD(p_password);

    BEGIN
        -- Lấy mật khẩu từ bảng EMPLOYEE
        SELECT PASSWORD_HASH
        INTO v_db_hash
        FROM EMPLOYEE
        WHERE USERNAME = p_username;

        p_out_username := p_username;

        -- Lấy trạng thái từ DBA_USERS
        BEGIN
            SELECT ACCOUNT_STATUS
            INTO v_status
            FROM DBA_USERS
            WHERE USERNAME = UPPER(p_username);
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                v_status := 'UNKNOWN';
        END;

        IF v_status LIKE 'LOCKED%' THEN
            p_out_result := 'ACCOUNT LOCKED';
            p_out_role := NULL;
            RETURN;
        END IF;

        IF v_db_hash = v_hash THEN
            p_out_result := 'SUCCESS';

            -- Lấy role của user
            BEGIN
                SELECT LISTAGG(GRANTED_ROLE, ', ') WITHIN GROUP (ORDER BY GRANTED_ROLE)
                INTO p_out_role
                FROM DBA_ROLE_PRIVS
                WHERE GRANTEE = UPPER(p_username);
            EXCEPTION
                WHEN NO_DATA_FOUND THEN
                    p_out_role := NULL;
            END;

        ELSE
            p_out_result := 'FAILED';
            p_out_role := NULL;
        END IF;

    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_out_username := NULL;
            p_out_result := 'FAILED';
            p_out_role := NULL;
    END;
END LOGIN_EMPLOYEE;
/

create or replace PROCEDURE GET_ALL_EMPLOYEES(
    p_cursor OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_cursor FOR
        SELECT e.EMP_ID,
               e.FULL_NAME,
               e.USERNAME,
               e.EMAIL,
               e.PHONE,
               -- Lấy trạng thái từ DBA_USERS
               (SELECT ACCOUNT_STATUS 
                FROM DBA_USERS d 
                WHERE d.USERNAME = UPPER(e.USERNAME)) AS STATUS
        FROM APP.EMPLOYEE e;
END;
/
create or replace PROCEDURE GET_EMPLOYEE_BY_ID(
    p_empid  IN  NUMBER,
    p_cursor OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_cursor FOR
        SELECT e.EMP_ID,
               e.FULL_NAME,
               e.USERNAME,
               e.EMAIL,
               e.PHONE,
               -- Lấy trạng thái từ DBA_USERS
               (SELECT d.ACCOUNT_STATUS
                FROM DBA_USERS d
                WHERE d.USERNAME = UPPER(e.USERNAME)) AS STATUS
        FROM APP.EMPLOYEE e
        WHERE e.EMP_ID = p_empid;
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
-- Procedure: UPDATE_EMPLOYEE_PUBLIC_KEY
-- Cập nhật public key của employee theo username
CREATE OR REPLACE PROCEDURE APP.UPDATE_EMPLOYEE_PUBLIC_KEY (
    p_username IN VARCHAR2,
    p_public_key IN CLOB
) AS
    v_count NUMBER;
BEGIN
    -- Kiểm tra employee có tồn tại không
    SELECT COUNT(*)
    INTO v_count
    FROM APP.EMPLOYEE
    WHERE USERNAME = p_username;

    IF v_count = 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Username không tồn tại: ' || p_username);
    END IF;

    -- Cập nhật public key
    UPDATE APP.EMPLOYEE
    SET PUBLIC_KEY = p_public_key
    WHERE USERNAME = p_username;

    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20002, 'Không thể cập nhật public key cho username: ' || p_username);
    END IF;

    COMMIT;
END UPDATE_EMPLOYEE_PUBLIC_KEY;
/

-- Grant execute permission
GRANT EXECUTE ON APP.UPDATE_EMPLOYEE_PUBLIC_KEY TO ROLE_ADMIN;
GRANT EXECUTE ON APP.UPDATE_EMPLOYEE_PUBLIC_KEY TO ROLE_TIEPTAN;
GRANT EXECUTE ON APP.UPDATE_EMPLOYEE_PUBLIC_KEY TO ROLE_THUKHO;
GRANT EXECUTE ON APP.UPDATE_EMPLOYEE_PUBLIC_KEY TO ROLE_KITHUATVIEN;


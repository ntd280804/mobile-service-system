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
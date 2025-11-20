
grant POL_EMPLOYEE_DBA to APP;
-- 1. Add the OLS label column to the business table
ALTER TABLE APP.EMPLOYEE ADD (OLS_LABEL NUMBER);
BEGIN
  SA_SYSDBA.CREATE_POLICY(
      policy_name  => 'POL_EMPLOYEE',
      column_name  => 'OLS_LABEL',
      default_options => 'NO_CONTROL'
  );
END;

BEGIN
    SA_COMPONENTS.CREATE_LEVEL(policy_name =>'POL_EMPLOYEE',long_name => 'SENSITIVE',short_name => 'SEN',level_num => 3);
    SA_COMPONENTS.CREATE_LEVEL(policy_name =>'POL_EMPLOYEE',long_name => 'CONFIDENTIAL',short_name => 'CONF',level_num => 2);
    SA_COMPONENTS.CREATE_LEVEL(policy_name =>'POL_EMPLOYEE',long_name => 'PUBLIC',short_name => 'PUB',level_num => 1);
END;
/


BEGIN
    SA_COMPONENTS.CREATE_COMPARTMENT('POL_EMPLOYEE',1001, 'ADMIN', 'ROLE_ADMIN');
    SA_COMPONENTS.CREATE_COMPARTMENT('POL_EMPLOYEE',1002, 'THUKHO', 'ROLE_THUKHO');
    SA_COMPONENTS.CREATE_COMPARTMENT('POL_EMPLOYEE',1003, 'KITHUATVIEN', 'ROLE_KITHUATVIEN');
    SA_COMPONENTS.CREATE_COMPARTMENT('POL_EMPLOYEE',1004, 'TIEPTAN', 'ROLE_TIEPTAN');
END;
/

-- 4. Define groups that reflect application roles
BEGIN
    SA_COMPONENTS.CREATE_GROUP(
        policy_name  => 'POL_EMPLOYEE',
        long_name    => 'ROLE_ADMIN',
        short_name   => 'ADMIN',
        group_num    => 1,
        parent_name  => NULL
    );

    SA_COMPONENTS.CREATE_GROUP(
        policy_name  => 'POL_EMPLOYEE',
        long_name    => 'ROLE_THUKHO',
        short_name   => 'THUKHO',
        group_num    => 2,
        parent_name  => NULL
    );

    SA_COMPONENTS.CREATE_GROUP(
        policy_name  => 'POL_EMPLOYEE',
        long_name    => 'ROLE_KITHUATVIEN',
        short_name   => 'KITHUATVIEN',
        group_num    => 3,
        parent_name  => NULL
    );

    SA_COMPONENTS.CREATE_GROUP(
        policy_name  => 'POL_EMPLOYEE',
        long_name    => 'ROLE_TIEPTAN',
        short_name   => 'TIEPTAN',
        group_num    => 4,
        parent_name  => NULL
    );
END;
/

BEGIN
    -- Admin: full access
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'POL_EMPLOYEE',
        label_tag   => 1001,
        label_value => 'SEN:ADMIN'
    );

    -- Admin: full access across all compartments
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'POL_EMPLOYEE',
        label_tag   => 1005,
        label_value => 'SEN:ADMIN,THUKHO,KITHUATVIEN,TIEPTAN'
    );

    -- Thủ kho: chỉ xem bản thân
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'POL_EMPLOYEE',
        label_tag   => 1002,
        label_value => 'CONF:THUKHO'
    );

    -- Kỹ thuật: chỉ xem bản thân
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'POL_EMPLOYEE',
        label_tag   => 1003,
        label_value => 'CONF:KITHUATVIEN'
    );

    -- Tiếp tân: xem bản thân + group KITHUATVIEN
    SA_LABEL_ADMIN.CREATE_LABEL(
        policy_name => 'POL_EMPLOYEE',
        label_tag   => 1004,
        label_value => 'CONF:TIEPTAN,KITHUATVIEN'
    );
END;
/
EXEC APP_CTX_PKG.set_role('ROLE_ADMIN');

EXECUTE SA_USER_ADMIN.SET_USER_LABELS('POL_EMPLOYEE','ADMIN','SEN:ADMIN,THUKHO,KITHUATVIEN,TIEPTAN');
EXECUTE SA_USER_ADMIN.SET_USER_LABELS('POL_EMPLOYEE','THUKHO','CONF:THUKHO');
EXECUTE SA_USER_ADMIN.SET_USER_LABELS('POL_EMPLOYEE','KITHUATVIEN','CONF:KITHUATVIEN');
EXECUTE SA_USER_ADMIN.SET_USER_LABELS('POL_EMPLOYEE','TIEPTAN','CONF:TIEPTAN,KITHUATVIEN');

UPDATE APP.EMPLOYEE
SET OLS_LABEL = CHAR_TO_LABEL('POL_EMPLOYEE','SEN:ADMIN')
WHERE EMP_ID = 1;
UPDATE APP.EMPLOYEE
SET OLS_LABEL = CHAR_TO_LABEL('POL_EMPLOYEE','CONF:THUKHO')
WHERE EMP_ID = 2;

UPDATE APP.EMPLOYEE
SET OLS_LABEL = CHAR_TO_LABEL('POL_EMPLOYEE','CONF:KITHUATVIEN')
WHERE EMP_ID = 3;

UPDATE APP.EMPLOYEE
SET OLS_LABEL = CHAR_TO_LABEL('POL_EMPLOYEE','CONF:TIEPTAN,KITHUATVIEN')
WHERE EMP_ID = 4;


BEGIN
    SA_USER_ADMIN.SET_LEVELS('POL_EMPLOYEE','ADMIN','SEN','PUB','SEN','SEN');
    SA_USER_ADMIN.SET_LEVELS('POL_EMPLOYEE','THUKHO','CONF','PUB','CONF','CONF');
    SA_USER_ADMIN.SET_LEVELS('POL_EMPLOYEE','KITHUATVIEN','CONF','PUB','CONF','CONF');
    SA_USER_ADMIN.SET_LEVELS('POL_EMPLOYEE','TIEPTAN','CONF','PUB','CONF','CONF');
END;
/

BEGIN
    SA_SYSDBA.ALTER_POLICY(
        policy_name     => 'POL_EMPLOYEE',
        default_options => 'READ_CONTROL,LABEL_DEFAULT'
    );

    SA_POLICY_ADMIN.REMOVE_TABLE_POLICY(
        policy_name => 'POL_EMPLOYEE',
        schema_name => 'APP',
        table_name  => 'EMPLOYEE',
        drop_column => FALSE
    );

    SA_POLICY_ADMIN.APPLY_TABLE_POLICY(
        policy_name   => 'POL_EMPLOYEE',
        schema_name   => 'APP',
        table_name    => 'EMPLOYEE',
        table_options => 'READ_CONTROL',
        predicate     => NULL
    );
END;
/
BEGIN
    -- 1. Thay đổi default_options của policy
    SA_SYSDBA.ALTER_POLICY(
        policy_name     => 'POL_EMPLOYEE',
        default_options => 'NO_CONTROL'
    );

    -- 2. Remove table policy hiện tại (không drop cột OLS_LABEL)
    SA_POLICY_ADMIN.REMOVE_TABLE_POLICY(
        policy_name => 'POL_EMPLOYEE',
        schema_name => 'APP',
        table_name  => 'EMPLOYEE',
        drop_column => true
    );

    -- 3. Áp dụng lại table policy với NO_CONTROL
    SA_POLICY_ADMIN.APPLY_TABLE_POLICY(
        policy_name   => 'POL_EMPLOYEE',
        schema_name   => 'APP',
        table_name    => 'EMPLOYEE',
        table_options => 'NO_CONTROL',
        predicate     => NULL
    );
END;
/



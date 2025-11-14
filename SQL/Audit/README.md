## Audit Folder Overview

This folder captures database auditing (FGA) scenarios for the Mobile Service System.  
It is derived from the legacy `SQL/Database_OtherProject/Audit*.sql` sample and aligned with the current VPD-based role model in `SQL/VPD`.

### Goals
- Log **ALL** `INSERT` and `UPDATE` operations against critical operational tables regardless of role.
- Tables audited: `EMPLOYEE`, `CUSTOMER`, `STOCK_IN`, `STOCK_IN_ITEM`, `STOCK_OUT`, `STOCK_OUT_ITEM`, `PART`, `PART_REQUEST`, `PART_REQUEST_ITEM`, `INVOICE`, `INVOICE_ITEM`, `ORDERS`, `CUSTOMER_APPOINTMENT`.
- Reuse the existing application context (`APP_CTX.ROLE_NAME`, `APP_CTX.EMP_ID`, `APP_CTX.CUSTOMER_PHONE`) so audits capture full context for sessions coming from the apps.
- Centralize alert handling through a lightweight package (`AUDIT_ALERT_PKG`) that writes into `AUDIT_ALERT_LOG` and can later be extended to email/SMS integrations.

### Structure
- `init/01_audit_prereq.sql` – verify/enable `AUDIT_TRAIL` database parameter.
- `init/02_audit_alert_pkg.sql` – create log table, sequence and the reusable `AUDIT_ALERT_PKG`.
- `init/03_drop_all_fga_policies.sql` – drop all FGA INSERT/UPDATE policies at once.
- `Employee/01_employee_dml_audit.sql` – FGA INSERT/UPDATE policies for `EMPLOYEE`.
- `Customer/01_customer_dml_audit.sql` – FGA INSERT/UPDATE policies for `CUSTOMER`.
- `Stock/01_stock_dml_audit.sql` – FGA INSERT/UPDATE policies for `STOCK_IN`, `STOCK_OUT`, `STOCK_IN_ITEM`, `STOCK_OUT_ITEM`.
- `Part/01_part_dml_audit.sql` – FGA INSERT/UPDATE policies for `PART`.
- `PartRequest/01_partrequest_dml_audit.sql` – FGA INSERT/UPDATE policies for `PART_REQUEST` and `PART_REQUEST_ITEM`.
- `Invoice/01_invoice_dml_audit.sql` – FGA INSERT/UPDATE policies for `INVOICE` and `INVOICE_ITEM`.
- `Order/01_order_dml_audit.sql` – FGA INSERT/UPDATE policies for `ORDERS`.
- `Appointment/01_appointment_dml_audit.sql` – FGA INSERT/UPDATE policies for `CUSTOMER_APPOINTMENT`.

Each sub-folder also contains:
- `*_AUDIT_POLICY.md` – describes the intent, allowed roles, and quick test queries.
- `02_drop_*_fga_policies.sql` – drop scripts for that module's policies.

### Execution order
1. Run `SQL/Audit/init/01_audit_prereq.sql` as a DBA to ensure `AUDIT_TRAIL` is at least `DB, EXTENDED`, then bounce the database if you had to change it.
2. Connect as the owning schema (e.g. `APP`) and run `SQL/Audit/init/02_audit_alert_pkg.sql`.
3. Run the module-specific INSERT/UPDATE audit scripts needed for your deployment.

> **Note**: These FGA policies log **ALL** `INSERT` and `UPDATE` operations regardless of role. This provides a complete audit trail of all data modifications for compliance and security purposes.



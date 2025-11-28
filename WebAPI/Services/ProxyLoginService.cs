using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Data;
using System.Linq;
using WebAPI.Helpers;

namespace WebAPI.Services
{
    public class ProxyLoginService
    {
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly IConfiguration _configuration;

        public ProxyLoginService(
            OracleConnectionManager connManager,
            JwtHelper jwtHelper,
            IConfiguration configuration)
        {
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _configuration = configuration;
        }

        public ProxyLoginResult LoginCustomer(string username, string? platform = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required for customer proxy login.");

            var resolvedPlatform = ResolvePlatform(platform);
            var proxyPassword = ResolveProxyPassword();
            var sessionId = Guid.NewGuid().ToString();

            var conn = _connManager.CreateConnection(username, proxyPassword, resolvedPlatform, sessionId, proxy: true);

            using (var setRoleCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_role(:p_role); END;", conn))
            {
                setRoleCmd.Parameters.Add("p_role", OracleDbType.Varchar2).Value = "ROLE_KHACHHANG";
                setRoleCmd.ExecuteNonQuery();
            }

            using (var setCusCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_customer(:p_phone); END;", conn))
            {
                setCusCmd.Parameters.Add("p_phone", OracleDbType.Varchar2).Value = username;
                setCusCmd.ExecuteNonQuery();
            }

            var roles = "ROLE_KHACHHANG";
            var token = _jwtHelper.GenerateToken(username, roles, sessionId);

            return new ProxyLoginResult(username, roles, token, sessionId);
        }

        public ProxyLoginResult LoginEmployee(string username, string? platform = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required for employee proxy login.");

            var resolvedPlatform = ResolvePlatform(platform);
            var proxyPassword = ResolveProxyPassword();
            var sessionId = Guid.NewGuid().ToString();
            var conn = _connManager.CreateConnection(username, proxyPassword, resolvedPlatform, sessionId, proxy: true);

            string roles = GetEmployeeRoles(conn, username);
            ApplyEmployeeContext(conn, username, roles);

            var token = _jwtHelper.GenerateToken(username, roles, sessionId);

            return new ProxyLoginResult(username, roles, token, sessionId);
        }

        private static void ApplyEmployeeContext(OracleConnection conn, string username, string roles)
        {
            string? primaryRole = DeterminePrimaryRole(roles);
            if (!string.IsNullOrEmpty(primaryRole))
            {
                using (var setRoleCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_role(:p_role); END;", conn))
                {
                    setRoleCmd.Parameters.Add("p_role", OracleDbType.Varchar2).Value = primaryRole;
                    setRoleCmd.ExecuteNonQuery();
                }

                using (var setUsernameCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_username(:p_username); END;", conn))
                {
                    setUsernameCmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                    setUsernameCmd.ExecuteNonQuery();
                }

                using (var getIdCmd = new OracleCommand("APP.GET_EMPLOYEE_ID_BY_USERNAME", conn))
                {
                    getIdCmd.CommandType = CommandType.StoredProcedure;
                    getIdCmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                    var outEmp = new OracleParameter("p_emp_id", OracleDbType.Decimal)
                    { Direction = ParameterDirection.Output };
                    getIdCmd.Parameters.Add(outEmp);
                    getIdCmd.ExecuteNonQuery();

                    var empId = ((OracleDecimal)outEmp.Value).Value;

                    using var setEmpCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_emp(:p_emp_id); END;", conn);
                    setEmpCmd.Parameters.Add("p_emp_id", OracleDbType.Decimal).Value = empId;
                    setEmpCmd.ExecuteNonQuery();
                }
            }
        }

        private static string? DeterminePrimaryRole(string roles)
        {
            if (string.IsNullOrWhiteSpace(roles)) return null;

            var roleList = roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                .Select(r => r.ToUpperInvariant())
                                .ToList();

            if (roleList.Contains("ROLE_ADMIN")) return "ROLE_ADMIN";
            if (roleList.Contains("ROLE_TIEPTAN")) return "ROLE_TIEPTAN";
            if (roleList.Contains("ROLE_KITHUATVIEN")) return "ROLE_KITHUATVIEN";
            if (roleList.Contains("ROLE_THUKHO")) return "ROLE_THUKHO";

            return null;
        }

        private static string GetEmployeeRoles(OracleConnection conn, string username)
        {
            using var cmdRoles = new OracleCommand("APP.GET_EMPLOYEE_ROLES_BY_USERNAME", conn);
            cmdRoles.CommandType = CommandType.StoredProcedure;

            var returnParam = new OracleParameter("returnVal", OracleDbType.Varchar2, 200)
            {
                Direction = ParameterDirection.ReturnValue
            };
            cmdRoles.Parameters.Add(returnParam);
            cmdRoles.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
            cmdRoles.ExecuteNonQuery();

            return returnParam.Value?.ToString() ?? string.Empty;
        }

        private string ResolvePlatform(string? platform)
        {
            if (!string.IsNullOrWhiteSpace(platform)) return platform!;
            return _configuration["QrLogin:DefaultPlatform"] ?? "WEB";
        }

        private string ResolveProxyPassword()
        {
            return _configuration["QrLogin:ProxyPassword"]
                ?? _configuration.GetConnectionString("DefaultPassword")
                ?? throw new InvalidOperationException("Missing QrLogin proxy password configuration.");
        }
    }

    public record ProxyLoginResult(string Username, string Roles, string Token, string SessionId);
}


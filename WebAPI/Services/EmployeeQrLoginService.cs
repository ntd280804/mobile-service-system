using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using WebAPI.Helpers;
using WebAPI.Models.Auth;

namespace WebAPI.Services
{
    public class EmployeeQrLoginService
    {
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly IConfiguration _configuration;

        public EmployeeQrLoginService(
            OracleConnectionManager connManager,
            JwtHelper jwtHelper,
            IConfiguration configuration)
        {
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _configuration = configuration;
        }

        public EmployeeLoginResult LoginViaProxy(string username, string? platform = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required for employee QR login.");

            var resolvedPlatform = string.IsNullOrWhiteSpace(platform)
                ? (_configuration["QrLogin:DefaultPlatform"] ?? "WEB")
                : platform!;

            var proxyPassword = _configuration["QrLogin:ProxyPassword"]
                ?? _configuration.GetConnectionString("DefaultPassword")
                ?? throw new InvalidOperationException("Missing QrLogin proxy password configuration.");

            var sessionId = Guid.NewGuid().ToString();
            var conn = _connManager.CreateConnection(username, proxyPassword, resolvedPlatform, sessionId, proxy: true);

            // Lấy danh sách role của nhân viên
            string roles1;
            using (var cmdRoles = new OracleCommand("APP.GET_EMPLOYEE_ROLES_BY_USERNAME", conn))
            {
                cmdRoles.CommandType = CommandType.StoredProcedure;

                var returnParam = new OracleParameter("returnVal", OracleDbType.Varchar2, 200)
                {
                    Direction = ParameterDirection.ReturnValue
                };
                cmdRoles.Parameters.Add(returnParam);

                cmdRoles.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                cmdRoles.ExecuteNonQuery();

                roles1 = returnParam.Value?.ToString() ?? string.Empty;
            }

            // Xác định primaryRole để set VPD context
            string? primaryRole = null;
            if (!string.IsNullOrWhiteSpace(roles1))
            {
                var roleList = roles1.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                     .Select(r => r.ToUpperInvariant())
                                     .ToList();
                if (roleList.Contains("ROLE_ADMIN")) primaryRole = "ROLE_ADMIN";
                else if (roleList.Contains("ROLE_TIEPTAN")) primaryRole = "ROLE_TIEPTAN";
                else if (roleList.Contains("ROLE_KITHUATVIEN")) primaryRole = "ROLE_KITHUATVIEN";
                else if (roleList.Contains("ROLE_THUKHO")) primaryRole = "ROLE_THUKHO";
            }

            if (!string.IsNullOrEmpty(primaryRole))
            {
                using (var setRoleCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_role(:p_role); END;", conn))
                {
                    setRoleCmd.Parameters.Add("p_role", OracleDbType.Varchar2).Value = primaryRole;
                    setRoleCmd.ExecuteNonQuery();
                }

                decimal empId;
                using (var setEmpCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_username(:p_username); END;", conn))
                {
                    setEmpCmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                    setEmpCmd.ExecuteNonQuery();
                }
                using (var getIdCmd = new OracleCommand("APP.GET_EMPLOYEE_ID_BY_USERNAME", conn))
                {
                    getIdCmd.CommandType = CommandType.StoredProcedure;
                    getIdCmd.Parameters.Add("p_username", OracleDbType.Varchar2).Value = username;
                    var outEmp = new OracleParameter("p_emp_id", OracleDbType.Decimal)
                    { Direction = ParameterDirection.Output };
                    getIdCmd.Parameters.Add(outEmp);
                    getIdCmd.ExecuteNonQuery();
                    empId = ((OracleDecimal)outEmp.Value).Value;
                }
                using (var setEmpCmd = new OracleCommand("BEGIN APP.APP_CTX_PKG.set_emp(:p_emp_id); END;", conn))
                {
                    setEmpCmd.Parameters.Add("p_emp_id", OracleDbType.Decimal).Value = empId;
                    setEmpCmd.ExecuteNonQuery();
                }
            }

            var roles = roles1;
            var token = _jwtHelper.GenerateToken(username, roles, sessionId);

            return new EmployeeLoginResult
            {
                Username = username,
                Roles = roles,
                Result = "SUCCESS",
                SessionId = sessionId,
                Token = token
            };
        }
    }
}



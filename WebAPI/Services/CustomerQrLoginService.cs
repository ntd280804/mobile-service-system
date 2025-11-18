using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebAPI.Helpers;
using WebAPI.Models.Auth;

namespace WebAPI.Services
{
    public class CustomerQrLoginService
    {
        private readonly OracleConnectionManager _connManager;
        private readonly JwtHelper _jwtHelper;
        private readonly IConfiguration _configuration;

        public CustomerQrLoginService(
            OracleConnectionManager connManager,
            JwtHelper jwtHelper,
            IConfiguration configuration)
        {
            _connManager = connManager;
            _jwtHelper = jwtHelper;
            _configuration = configuration;
        }

        public CustomerLoginResult LoginViaProxy(string username, string? platform = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required for QR login.");

            var resolvedPlatform = string.IsNullOrWhiteSpace(platform)
                ? (_configuration["QrLogin:DefaultPlatform"] ?? "WEB")
                : platform!;

            var proxyPassword = _configuration["QrLogin:ProxyPassword"]
                ?? _configuration.GetConnectionString("DefaultPassword")
                ?? throw new InvalidOperationException("Missing QrLogin proxy password configuration.");

            var sessionId = Guid.NewGuid().ToString();
            var conn = _connManager.CreateConnection(username, proxyPassword, resolvedPlatform, sessionId, proxy: true);

            // Thiết lập VPD context cho customer
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

            return new CustomerLoginResult
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



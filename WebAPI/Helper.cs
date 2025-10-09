using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;

namespace WebAPI
{
    public class Helper
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public Helper(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public OracleConnection GetTempOracleConnection()
        {
            var session = _httpContextAccessor.HttpContext?.Session;

            string tempUser;
            string tempPass;

            if (session == null)
            {
                // 🔥 Fallback về tài khoản mặc định
                tempUser = "App";
                tempPass = "App";
            }
            else
            {
                tempUser = session.GetString("TempOracleUsername");
                tempPass = session.GetString("TempOraclePassword");

                if (string.IsNullOrEmpty(tempUser) || string.IsNullOrEmpty(tempPass))
                {
                    // 🔥 Cũng fallback về tài khoản mặc định
                    tempUser = "App";
                    tempPass = "App";
                }
            }

            string connStr = $"User Id={tempUser};Password={tempPass};Data Source=192.168.26.138:1521/ORCLPDB1;Pooling=true";
            var conn = new OracleConnection(connStr);
            conn.Open();
            return conn;
        }

    }
}

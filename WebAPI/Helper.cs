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

            if (session == null)
                throw new Exception("No HttpContext session available");

            var tempUser = session.GetString("TempOracleUsername");
            var tempPass = session.GetString("TempOraclePassword");

            if (string.IsNullOrEmpty(tempUser) || string.IsNullOrEmpty(tempPass))
                throw new Exception("No temporary Oracle user in session");

            string connStr = $"User Id={tempUser};Password={tempPass};Data Source=192.168.26.138:1521/ORCLPDB1;Pooling=true";
            var conn = new OracleConnection(connStr);
            conn.Open();
            return conn;
        }
    }
}

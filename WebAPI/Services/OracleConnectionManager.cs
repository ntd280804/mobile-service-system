using Oracle.ManagedDataAccess.Client;
using System.Collections.Concurrent;
using System.Data;
using Microsoft.AspNetCore.SignalR;
using WebAPI.Hubs;

namespace WebAPI.Services
{
    public class OracleConnectionManager
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IConfiguration _configuration;
        public OracleConnectionManager(IHubContext<NotificationHub> hubContext, IConfiguration configuration)
        {
            _hubContext = hubContext;
            _configuration = configuration;
        }
        private record OracleConnInfo(OracleConnection Conn, string OracleSid);
        // Key: (username, platform, sessionId)
        private readonly ConcurrentDictionary<(string username, string platform, string sessionId)
            , OracleConnInfo> _connections = new();
        public OracleConnection? GetConnectionIfExists(string username, string platform, string sessionId)
        {
            var key = (username, platform, sessionId);
            if (!_connections.TryGetValue(key, out var info))
                return null;
            try
            {
                if (info.Conn.State != ConnectionState.Open)
                    throw new Exception("Connection closed");

                using var cmd = info.Conn.CreateCommand();
                cmd.CommandText = "SELECT 1 FROM DUAL";
                cmd.ExecuteScalar();

                return info.Conn;
            }
            catch
            {
                RemoveConnection(username, platform, sessionId).Wait();
                return null;
            }
        }
        public OracleConnection CreateConnection(string username, string password, string platform
            , string sessionId, bool proxy = false)
        {
            RemoveAllConnections(username, platform).Wait();
            var oracleUsername = proxy
                ? BuildProxyUsername(username)
                : username;
            var connectionStringTemplate = _configuration.GetConnectionString("OracleDb");
            var connString = connectionStringTemplate
                .Replace("{username}", oracleUsername)
                .Replace("{password}", password);
            var conn = new OracleConnection(connString);
            OracleConnection.ClearPool(conn);
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "BEGIN DBMS_APPLICATION_INFO.SET_MODULE(:module_name, :client_info); END;";
                cmd.Parameters.Add(new OracleParameter("module_name", $"WebAPI-{platform}"));
                cmd.Parameters.Add(new OracleParameter("client_info", sessionId));
                cmd.ExecuteNonQuery();
            }
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "BEGIN DBMS_SESSION.SET_IDENTIFIER(:sid); END;";
                cmd.Parameters.Add(new OracleParameter("sid", sessionId));
                cmd.ExecuteNonQuery();
            }
            string oracleSid;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT SYS_CONTEXT('USERENV', 'SID') FROM DUAL";
                oracleSid = cmd.ExecuteScalar()?.ToString() ?? "";
            }
            var key = (username, platform, sessionId);
            _connections[key] = new OracleConnInfo(conn, oracleSid);
            return conn;
        }
        private string BuildProxyUsername(string targetUsername)
        {
            var proxyUser = _configuration["QrLogin:ProxyUser"]
                ?? _configuration.GetConnectionString("DefaultUsername")
                ?? "APP";
            return $"{proxyUser}[{targetUsername}]";
        }
        public async Task RemoveConnection(string username, string platform, string sessionId)
        {
            var key = (username, platform, sessionId);
            if (_connections.TryRemove(key, out var info))
            {
                var conn = info.Conn;
                try
                {
                    if (conn.State == ConnectionState.Open)
                        conn.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RemoveConnection] Error closing {key}: {ex.Message}");
                }
                finally
                {
                    try { OracleConnection.ClearPool(conn); } catch { }
                    conn.Dispose();
                    Console.WriteLine($"[RemoveConnection] Closed and disposed {key} (SID={info.OracleSid})");
                }

                // Send SignalR logout message to group (sessionId)
                await _hubContext.Clients.Group(sessionId).SendAsync("ForceLogout", "Your session has been logged out.");
            }
        }
        public OracleConnection CreateDefaultConnection()
        {
            var username = _configuration.GetConnectionString("DefaultUsername");
            var password = _configuration.GetConnectionString("DefaultPassword");
            var template = _configuration.GetConnectionString("OracleDb");
            var connString = template.Replace("{username}", username)
                                     .Replace("{password}", password);

            var conn = new OracleConnection(connString);
            conn.Open();
            return conn;
        }
        public async Task RemoveAllConnections(string username, string platform)
        {
            var keys = _connections.Keys
                .Where(k => k.username == username && k.platform == platform)
                .ToList();

            foreach (var key in keys)
                await RemoveConnection(key.username, key.platform, key.sessionId);
        }
        public void CleanupDeadConnections()
        {
            foreach (var key in _connections.Keys.ToList())
            {
                var conn = GetConnectionIfExists(key.username, key.platform, key.sessionId);
                if (conn == null)
                    Console.WriteLine($"[CleanupDeadConnections] Removed dead session {key}");
            }
        }

    }
}

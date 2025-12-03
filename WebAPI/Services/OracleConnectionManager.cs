using Oracle.ManagedDataAccess.Client;
using System.Collections.Concurrent;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using WebAPI.Hubs;
using Microsoft.Extensions.Logging;
using WebAPI.Helpers;

namespace WebAPI.Services
{
    public class OracleConnectionManager
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OracleConnectionManager> _logger;
        
        public OracleConnectionManager(
            IHubContext<NotificationHub> hubContext, 
            IConfiguration configuration,
            ILogger<OracleConnectionManager> logger)
        {
            _hubContext = hubContext;
            _configuration = configuration;
            _logger = logger;
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
                {
                    _logger.LogWarning(
                        "Connection state is {State} for user {Username}, platform {Platform}, session {SessionId}",
                        info.Conn.State, username, platform, sessionId);
                    RemoveConnection(username, platform, sessionId).Wait();
                    return null;
                }

                // Kiểm tra connection còn sống bằng cách chạy query đơn giản
                using var cmd = info.Conn.CreateCommand();
                cmd.CommandText = "SELECT 1 FROM DUAL";
                cmd.CommandTimeout = 5; // Timeout ngắn để phát hiện nhanh
                var result = cmd.ExecuteScalar();
                
                if (result == null || result.ToString() != "1")
                {
                    _logger.LogWarning(
                        "Invalid query result for user {Username}, platform {Platform}, session {SessionId}",
                        username, platform, sessionId);
                    RemoveConnection(username, platform, sessionId).Wait();
                    return null;
                }

                return info.Conn;
            }
            catch (OracleException ex)
            {
                // Error 28 = session killed
                // Error 1012 = not logged on
                // Error 1017 = invalid username/password
                // Error 12154 = TNS could not resolve service name
                // Error 12571 = TNS packet writer failure
                if (ex.Number == 28 || ex.Number == 1012 || ex.Number == 1017 || ex.Number == 12154 || ex.Number == 12571)
                {
                    _logger.LogError(ex,
                        "Oracle session killed or connection lost. Error {ErrorNumber} for user {Username}, platform {Platform}, session {SessionId}. Message: {Message}",
                        ex.Number, username, platform, sessionId, ex.Message);
                }
                else
                {
                    _logger.LogWarning(ex,
                        "OracleException in GetConnectionIfExists. Error {ErrorNumber} for user {Username}, platform {Platform}, session {SessionId}. Message: {Message}",
                        ex.Number, username, platform, sessionId, ex.Message);
                }
                
                RemoveConnection(username, platform, sessionId).Wait();
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "General exception in GetConnectionIfExists for user {Username}, platform {Platform}, session {SessionId}. Type: {ExceptionType}, Message: {Message}",
                    username, platform, sessionId, ex.GetType().Name, ex.Message);
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
            // Dùng TryRemove để đảm bảo atomic - chỉ có 1 luồng có thể remove connection
            // Nếu connection đã bị remove rồi, return ngay
            if (!_connections.TryRemove(key, out var info))
            {
                // Connection đã được remove rồi bởi luồng khác, không cần làm gì
                _logger.LogDebug(
                    "Connection already removed for user {Username}, platform {Platform}, session {SessionId}",
                    username, platform, sessionId);
                return;
            }
            // Chỉ kill session nếu thực sự remove được connection từ dictionary
            // Kill Oracle session bằng procedure trước khi dispose connection
            try
            {
                using var defaultConn = CreateDefaultConnection();
                var killResult = OracleHelper.ExecuteNonQueryWithOutputs(
                    defaultConn,
                    "APP.KILL_SESSION_BY_CLIENT_ID",
                    new[]
                    {
                        ("p_client_identifier", OracleDbType.Varchar2, (object)sessionId)
                    },
                    new[]
                    {
                        ("p_result", OracleDbType.Varchar2)
                    });

                var resultMessage = killResult["p_result"]?.ToString() ?? "Unknown";
                _logger.LogInformation(
                    "Killed Oracle session for user {Username}, platform {Platform}, session {SessionId}. Result: {Result}",
                    username, platform, sessionId, resultMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to kill Oracle session for user {Username}, platform {Platform}, session {SessionId}. Error: {Error}",
                    username, platform, sessionId, ex.Message);
                // Continue with disposal even if kill failed
            }
            // Dispose connection
            var conn = info.Conn;
            try
            {
                if (conn.State == ConnectionState.Open)
                    conn.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error closing connection for user {Username}, platform {Platform}, session {SessionId}",
                    username, platform, sessionId);
            }
            finally
            {
                try { OracleConnection.ClearPool(conn); } catch { }
                conn.Dispose();
                _logger.LogInformation(
                    "Closed and disposed connection for user {Username}, platform {Platform}, session {SessionId}, Oracle SID: {OracleSid}",
                    username, platform, sessionId, info.OracleSid);
            }

            // Send SignalR logout message to group (sessionId)
            await _hubContext.Clients.Group(sessionId).SendAsync("ForceLogout", "Your session has been logged out.");
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
                {
                    _logger.LogInformation(
                        "Removed dead session during cleanup for user {Username}, platform {Platform}, session {SessionId}",
                        key.username, key.platform, key.sessionId);
                }
            }
        }

        /// <summary>
        /// Kiểm tra xem sessionId có tồn tại trong manager không (bất kể username/platform)
        /// </summary>
        public bool SessionIdExists(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return false;

            return _connections.Keys.Any(k => k.sessionId == sessionId);
        }

        /// <summary>
        /// Kiểm tra và refresh session nếu cần (keep-alive)
        /// </summary>
        public bool TryRefreshSession(string username, string platform, string sessionId)
        {
            var key = (username, platform, sessionId);
            if (!_connections.TryGetValue(key, out var info))
            {
                _logger.LogDebug(
                    "Session not found for refresh: user {Username}, platform {Platform}, session {SessionId}",
                    username, platform, sessionId);
                return false;
            }

            try
            {
                if (info.Conn.State != ConnectionState.Open)
                {
                    _logger.LogWarning(
                        "Connection state is {State} during refresh for user {Username}, platform {Platform}, session {SessionId}",
                        info.Conn.State, username, platform, sessionId);
                    return false;
                }

                // Chạy query đơn giản để refresh session
                using var cmd = info.Conn.CreateCommand();
                cmd.CommandText = "SELECT SYS_CONTEXT('USERENV', 'SESSIONID') FROM DUAL";
                cmd.CommandTimeout = 3;
                var sessionIdFromDb = cmd.ExecuteScalar()?.ToString();
                
                if (string.IsNullOrEmpty(sessionIdFromDb))
                {
                    _logger.LogWarning(
                        "Invalid session ID from DB during refresh for user {Username}, platform {Platform}, session {SessionId}",
                        username, platform, sessionId);
                    return false;
                }

                _logger.LogDebug(
                    "Session refreshed successfully for user {Username}, platform {Platform}, session {SessionId}",
                    username, platform, sessionId);
                return true;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex,
                    "OracleException during session refresh. Error {ErrorNumber} for user {Username}, platform {Platform}, session {SessionId}. Message: {Message}",
                    ex.Number, username, platform, sessionId, ex.Message);
                
                if (ex.Number == 28 || ex.Number == 1012)
                {
                    // Session killed, remove it
                    _logger.LogWarning(
                        "Session killed (Error {ErrorNumber}), removing connection for user {Username}, platform {Platform}, session {SessionId}",
                        ex.Number, username, platform, sessionId);
                    RemoveConnection(username, platform, sessionId).Wait();
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Exception during session refresh for user {Username}, platform {Platform}, session {SessionId}. Type: {ExceptionType}, Message: {Message}",
                    username, platform, sessionId, ex.GetType().Name, ex.Message);
                return false;
            }
        }

    }
}

using Microsoft.AspNetCore.SignalR;
using WebAPI.Services;

namespace WebAPI.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly OracleConnectionManager _connectionManager;
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(OracleConnectionManager connectionManager, ILogger<NotificationHub> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        // Gửi message force logout tới user
        public async Task ForceLogout(string message)
        {
            await Clients.Caller.SendAsync("ForceLogout", message);
        }

        public override async Task OnConnectedAsync()
        {
            var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"].ToString();
            
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("Connection attempt without sessionId from {ConnectionId}", Context.ConnectionId);
                throw new HubException("SessionId is required");
            }

            // Kiểm tra sessionId có tồn tại trong OracleConnectionManager không
            if (!_connectionManager.SessionIdExists(sessionId))
            {
                _logger.LogWarning("Connection attempt with invalid sessionId: {SessionId} from {ConnectionId}", 
                    sessionId, Context.ConnectionId);
                throw new HubException("Invalid sessionId. Please login again.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
            _logger.LogInformation("Client connected with valid sessionId: {SessionId}, ConnectionId: {ConnectionId}", 
                sessionId, Context.ConnectionId);
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"].ToString();
            if (!string.IsNullOrEmpty(sessionId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}

using Microsoft.AspNetCore.SignalR;

namespace WebAPI.Hubs
{
    public class NotificationHub : Hub
    {
        // Gửi message force logout tới user
        public async Task ForceLogout(string message)
        {
            await Clients.Caller.SendAsync("ForceLogout", message);
        }

        public override async Task OnConnectedAsync()
        {
            var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"];
            if (!string.IsNullOrEmpty(sessionId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"];
            if (!string.IsNullOrEmpty(sessionId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}

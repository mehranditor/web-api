using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using WebApplication1.Hubs;

namespace WebApplication1.Features.Notifications
{
    public class UserCreatedNotificationHandler : INotificationHandler<UserCreatedNotification>
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<UserCreatedNotificationHandler> _logger;

        public UserCreatedNotificationHandler(IHubContext<NotificationHub> hubContext, ILogger<UserCreatedNotificationHandler> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Sending SignalR notification: New user created: {Username}", notification.Username);
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"New user created: {notification.Username}", cancellationToken);
        }
    }
}
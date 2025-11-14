using MediatR;
using Microsoft.AspNetCore.SignalR;
using WebApplication1.Hubs;

namespace WebApplication1.Features.Notifications
{
    public class UserDeletedNotificationHandler : INotificationHandler<UserDeletedNotification>
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public UserDeletedNotificationHandler(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Handle(UserDeletedNotification notification, CancellationToken cancellationToken)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"User deleted: {notification.UserId}");
        }
    }
}
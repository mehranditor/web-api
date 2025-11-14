using MediatR;
using Microsoft.AspNetCore.SignalR;
using WebApplication1.Hubs;

namespace WebApplication1.Features.Notifications
{
    public class UserUpdatedNotificationHandler : INotificationHandler<UserUpdatedNotification>
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public UserUpdatedNotificationHandler(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Handle(UserUpdatedNotification notification, CancellationToken cancellationToken)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", $"User updated: {notification.UserId}");
        }
    }
}
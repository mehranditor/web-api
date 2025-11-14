using MediatR;

namespace WebApplication1.Features.Notifications
{
    public class UserDeletedNotification : INotification
    {
        public string UserId { get; set; } = null!;

        public UserDeletedNotification(string userId)
        {
            UserId = userId;
        }
    }
}
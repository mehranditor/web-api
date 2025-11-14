using MediatR;

namespace WebApplication1.Features.Notifications
{
    public class UserUpdatedNotification : INotification
    {
        public string UserId { get; set; } = null!;

        public UserUpdatedNotification(string userId)
        {
            UserId = userId;
        }
    }
}
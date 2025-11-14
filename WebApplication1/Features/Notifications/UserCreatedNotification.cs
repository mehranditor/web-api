using MediatR;

namespace WebApplication1.Features.Notifications
{
    public class UserCreatedNotification : INotification
    {
        public string Username { get; set; } = null!;

        public UserCreatedNotification(string username)
        {
            Username = username;
        }
    }
}
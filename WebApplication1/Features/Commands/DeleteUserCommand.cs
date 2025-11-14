using MediatR;

namespace WebApplication1.Features.Commands
{
    public class DeleteUserCommand : IRequest<bool>
    {
        public string Id { get; set; } = null!;

        public DeleteUserCommand(string id)
        {
            Id = id;
        }
    }
}
using MediatR;
using WebApplication1.Models.DTOs;

namespace WebApplication1.Features.Commands
{
    public class UpdateUserCommand : IRequest<bool>
    {
        public string Id { get; set; } = null!;
        public UserDto UserDto { get; set; } = null!;

        public UpdateUserCommand(string id, UserDto userDto)
        {
            Id = id;
            UserDto = userDto;
        }
    }
}
using MediatR;
using Microsoft.AspNetCore.Identity;
using WebApplication1.Models.DTOs;

namespace WebApplication1.Features.Commands
{
    public class CreateUserCommand : IRequest<IdentityUser>
    {
        public CreateUserDto CreateUserDto { get; set; }

        public CreateUserCommand(CreateUserDto createUserDto)
        {
            CreateUserDto = createUserDto;
        }
    }
}
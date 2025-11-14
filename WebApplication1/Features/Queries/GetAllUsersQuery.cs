using MediatR;
using WebApplication1.Models.DTOs;

namespace WebApplication1.Features.Queries
{
    public class GetAllUsersQuery : IRequest<List<UserDto>>
    {
    }
}
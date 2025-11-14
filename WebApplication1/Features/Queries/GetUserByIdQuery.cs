using MediatR;
using WebApplication1.Models.DTOs;

namespace WebApplication1.Features.Queries
{
    public class GetUserByIdQuery : IRequest<UserDto?>
    {
        public string Id { get; set; }

        public GetUserByIdQuery(string id)
        {
            Id = id;
        }
    }
}
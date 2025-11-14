using MediatR;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.DTOs;
using WebApplication1.Services;

namespace WebApplication1.Features.Queries
{
    public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, List<UserDto>>
    {
        private readonly UserService _userService;
        private readonly ILogger<GetAllUsersQueryHandler> _logger;

        public GetAllUsersQueryHandler(UserService userService, ILogger<GetAllUsersQueryHandler> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public async Task<List<UserDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling GetAllUsersQuery");
            return await _userService.GetAllUsersAsync();
        }
    }
}
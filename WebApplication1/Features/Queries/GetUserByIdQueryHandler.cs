using MediatR;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.DTOs;
using WebApplication1.Services;

namespace WebApplication1.Features.Queries
{
    public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
    {
        private readonly UserService _userService;
        private readonly ILogger<GetUserByIdQueryHandler> _logger;

        public GetUserByIdQueryHandler(UserService userService, ILogger<GetUserByIdQueryHandler> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling GetUserByIdQuery for ID: {Id}", request.Id);
            return await _userService.GetUserByIdAsync(request.Id);
        }
    }
}
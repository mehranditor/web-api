using MediatR;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.DTOs;
using WebApplication1.Services;

namespace WebApplication1.Features.Queries
{
    public class GetPagedUsersQueryHandler : IRequestHandler<GetPagedUsersQuery, PagedResult<UserDto>>
    {
        private readonly UserService _userService;
        private readonly ILogger<GetPagedUsersQueryHandler> _logger;

        public GetPagedUsersQueryHandler(UserService userService, ILogger<GetPagedUsersQueryHandler> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public async Task<PagedResult<UserDto>> Handle(GetPagedUsersQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling GetPagedUsersQuery: Search={Search}, Page={PageNumber}", request.Search, request.PageNumber);
            return await _userService.GetUsersAsync(request.Search, request.SortBy, request.SortOrder, request.PageNumber, request.PageSize);
        }
    }
}
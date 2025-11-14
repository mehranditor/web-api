using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Filters;
using WebApplication1.Features.Commands; // For CreateUserCommand, UpdateUserCommand, etc.
using WebApplication1.Features.Queries; // For GetAllUsersQuery, GetUserByIdQuery, etc.
using WebApplication1.Models.DTOs;
using WebApplication1.Swagger;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Requires authentication
    public class UserController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<UserController> _logger;
        private readonly IDistributedCache _cache;

        public UserController(IMediator mediator, ILogger<UserController> logger, IDistributedCache cache)
        {
            _mediator = mediator;
            _logger = logger;
            _cache = cache;
        }

        [HttpGet("test-redis")]
        [AllowAnonymous]
        public async Task<IActionResult> TestRedis()
        {
            try
            {
                var key = "TestKey";
                await _cache.SetStringAsync(key, "TestValue", new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });
                var value = await _cache.GetStringAsync(key);
                _logger.LogInformation("Redis test: Set key {Key} with value {Value}", key, value);
                return Ok(new { message = $"Redis test successful: {value}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis test failed");
                return StatusCode(500, new { error = "Redis connection failed", details = ex.Message });
            }
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<UserDto>))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [SwaggerResponseExample(StatusCodes.Status200OK, typeof(UsersListExample))]
        public async Task<IActionResult> GetAllUsers()
        {
            _logger.LogInformation("Fetching all users");
            var users = await _mediator.Send(new GetAllUsersQuery());
            return Ok(users);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [SwaggerResponseExample(StatusCodes.Status200OK, typeof(UserDtoExample))]
        [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(ProblemDetailsExample))]
        public async Task<IActionResult> GetUser(string id)
        {
            _logger.LogInformation("Fetching user with ID: {Id}", id);
            var user = await _mediator.Send(new GetUserByIdQuery(id));
            return user == null ? NotFound() : Ok(user);
        }

        [HttpPost]
        [AllowAnonymous] // Allow registration without token
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(IdentityUser))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [SwaggerResponseExample(StatusCodes.Status201Created, typeof(UserDtoExample))]
        [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(ProblemDetailsExample))]
        public async Task<IActionResult> CreateUser(CreateUserDto dto)
        {
            _logger.LogInformation("Creating user with username: {UserName}", dto.UserName);
            var user = await _mediator.Send(new CreateUserCommand(dto));
            return user == null ? BadRequest() : CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }

        [HttpGet("paged")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<UserDto>))]
        public async Task<IActionResult> GetPagedUsers(
            [FromQuery] string? search,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = new GetPagedUsersQuery(search, sortBy, sortOrder, pageNumber, pageSize);
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")] // Restrict to Admin
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(ProblemDetailsExample))]
        public async Task<IActionResult> UpdateUser(string id, UserDto dto)
        {
            _logger.LogInformation("Updating user with ID: {Id}", id);
            var command = new UpdateUserCommand(id, dto);
            var success = await _mediator.Send(command);
            return success ? NoContent() : NotFound();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] // Restrict to Admin
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [SwaggerResponseExample(StatusCodes.Status404NotFound, typeof(ProblemDetailsExample))]
        public async Task<IActionResult> DeleteUser(string id)
        {
            _logger.LogInformation("Deleting user with ID: {Id}", id);
            var command = new DeleteUserCommand(id);
            var success = await _mediator.Send(command);
            return success ? NoContent() : NotFound();
        }
    }
}
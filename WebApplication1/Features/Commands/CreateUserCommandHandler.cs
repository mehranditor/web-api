using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using WebApplication1.Features.Notifications;  // Import for UserCreatedNotification
using WebApplication1.Models.DTOs;
using WebApplication1.Repositories;

namespace WebApplication1.Features.Commands
{
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, IdentityUser>
    {
        private readonly UserRepository _repository;
        private readonly ILogger<CreateUserCommandHandler> _logger;
        private readonly IMediator _mediator;  // NEW: Inject IMediator for publishing

        public CreateUserCommandHandler(
            UserRepository repository,
            ILogger<CreateUserCommandHandler> logger,
            IMediator mediator)  // NEW: Add to constructor
        {
            _repository = repository;
            _logger = logger;
            _mediator = mediator;
        }

        public async Task<IdentityUser> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling CreateUserCommand for username: {UserName}", request.CreateUserDto.UserName);
            var user = await _repository.CreateUserAsync(request.CreateUserDto);  // Core logic: Create user

            // NEW: Publish the notification AFTER successful creation
            _logger.LogInformation("Publishing UserCreatedNotification for {UserName}", user.UserName);
            await _mediator.Publish(new UserCreatedNotification(user.UserName!), cancellationToken);

            return user;  // Return the result
        }
    }
}
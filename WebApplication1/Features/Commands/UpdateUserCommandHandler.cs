using MediatR;
using Microsoft.Extensions.Logging;
using WebApplication1.Features.Notifications;  // For UserUpdatedNotification
using WebApplication1.Models.DTOs;
using WebApplication1.Repositories;

namespace WebApplication1.Features.Commands
{
    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, bool>
    {
        private readonly UserRepository _repository;
        private readonly ILogger<UpdateUserCommandHandler> _logger;
        private readonly IMediator _mediator;  // NEW: For publishing

        public UpdateUserCommandHandler(
            UserRepository repository,
            ILogger<UpdateUserCommandHandler> logger,
            IMediator mediator)  // NEW: Add to constructor
        {
            _repository = repository;
            _logger = logger;
            _mediator = mediator;
        }

        public async Task<bool> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling UpdateUserCommand for ID: {Id}", request.Id);
            var success = await _repository.UpdateUserAsync(request.Id, request.UserDto);

            // NEW: Publish event AFTER success
            if (success)
            {
                await _mediator.Publish(new UserUpdatedNotification(request.Id), cancellationToken);
            }

            return success;
        }
    }
}
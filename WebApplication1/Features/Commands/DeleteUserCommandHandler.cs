using MediatR;
using Microsoft.Extensions.Logging;
using WebApplication1.Features.Notifications;  // For UserDeletedNotification
using WebApplication1.Repositories;

namespace WebApplication1.Features.Commands
{
    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
    {
        private readonly UserRepository _repository;
        private readonly ILogger<DeleteUserCommandHandler> _logger;
        private readonly IMediator _mediator;  // NEW: For publishing

        public DeleteUserCommandHandler(
            UserRepository repository,
            ILogger<DeleteUserCommandHandler> logger,
            IMediator mediator)  // NEW: Add to constructor
        {
            _repository = repository;
            _logger = logger;
            _mediator = mediator;
        }

        public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling DeleteUserCommand for ID: {Id}", request.Id);
            var success = await _repository.DeleteUserAsync(request.Id);

            // NEW: Publish event AFTER success
            if (success)
            {
                await _mediator.Publish(new UserDeletedNotification(request.Id), cancellationToken);
            }

            return success;
        }
    }
}
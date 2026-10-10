using MediatR;
using Microsoft.AspNetCore.Identity;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.IdentityService.Application.UseCases.Users.Commands;

public record DeactivateUserCommand(Guid Id) : IRequest;

public class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand>
{
    private readonly UserManager<User> _userManager;

    public DeactivateUserCommandHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? Guard.Against.NotFound<User>(null, "User", request.Id);

        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to deactivate user: {errors}");
        }
    }
}

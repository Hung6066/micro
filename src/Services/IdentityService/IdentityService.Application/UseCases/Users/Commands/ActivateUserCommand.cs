using MediatR;
using Microsoft.AspNetCore.Identity;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Users.Commands;

public record ActivateUserCommand(Guid Id) : IRequest;

public class ActivateUserCommandHandler : IRequestHandler<ActivateUserCommand>
{
    private readonly UserManager<User> _userManager;

    public ActivateUserCommandHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task Handle(ActivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        user.IsActive = true;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to activate user: {errors}");
        }
    }
}

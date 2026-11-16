using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shipwise.AspNetCore.Controllers;
using Shipwise.AspNetCore.Security;
using Shipwise.Identity.Infrastructure.Persistence.DbContexts;
using Shipwise.SharedKernel.Abstractions.Message;
using Shipwise.SharedKernel.Abstractions.Shared;
using Shipwise.SharedKernel.Cqrs;
using Shipwise.SharedKernel.Exceptions;
using Shipwise.SharedKernel.Security;
using ValidationException = Shipwise.SharedKernel.Exceptions.ValidationException;

namespace Shipwise.Identity.Tests.Host;

/// <summary>
/// Test-only endpoints. They exist so the skeleton's request pipeline, authentication and
/// transaction handling can be proven before the module has any business endpoint.
/// </summary>
#pragma warning disable SA1402 // The probe types belong together and never ship.
public sealed class ProbeController(ISender sender) : ApiController(sender)
{
    [HttpGet("ok")]
    [PermissionAuthorize("Probe", ActionType.View)]
    public async Task<IActionResult> GetOk(CancellationToken cancellationToken)
        => Ok((await Sender.SendAsync(new GetProbeQuery(false), cancellationToken)).Value);

    [HttpGet("missing")]
    [PermissionAuthorize("Probe", ActionType.View)]
    public async Task<IActionResult> GetMissing(CancellationToken cancellationToken)
        => Ok((await Sender.SendAsync(new GetProbeQuery(true), cancellationToken)).Value);

    [HttpPost]
    [PermissionAuthorize("Probe", ActionType.Create)]
    public async Task<IActionResult> Create(CreateProbeRequest request, CancellationToken cancellationToken)
    {
        Result<string> result = await Sender.SendAsync(new CreateProbeCommand(request.Name), cancellationToken);
        return result.IsFailure ? HandleFailure(result) : Ok(result.Value);
    }
}

public sealed record CreateProbeRequest(string Name);

public sealed record GetProbeQuery(bool Missing) : IQuery<string>;

public sealed class GetProbeQueryHandler : IQueryHandler<GetProbeQuery, string>
{
    public Task<Result<string>> HandleAsync(GetProbeQuery request, CancellationToken cancellationToken)
        => request.Missing
            ? throw new NotFoundException("Probe", 42)
            : Task.FromResult(Result.Success("hello"));
}

/// <summary>
/// Inserts a row into the probe table. The names "throw" and "fail" make the handler throw
/// or return a failed result after the insert, to prove the transaction is rolled back.
/// </summary>
public sealed record CreateProbeCommand(string Name) : ICommand<string>;

public sealed class CreateProbeCommandValidator : IValidator<CreateProbeCommand>
{
    public Task ValidateAsync(CreateProbeCommand request, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(request.Name)
            ? throw new ValidationException([new ValidationError("name", "Name is required.")])
            : Task.CompletedTask;
}

public sealed class CreateProbeCommandHandler(IdentityDbContext dbContext) : ICommandHandler<CreateProbeCommand, string>
{
    public async Task<Result<string>> HandleAsync(CreateProbeCommand request, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlAsync(
            $"INSERT INTO public.\"SkeletonProbe\" (\"Name\") VALUES ({request.Name})",
            cancellationToken);

        return request.Name switch
        {
            "throw" => throw new ConflictException("Probe.Conflict", "The probe was asked to throw."),
            "fail" => Result.Failure<string>(new Error("Probe.Failed", "The probe was asked to fail.")),
            _ => Result.Success(request.Name),
        };
    }
}
#pragma warning restore SA1402

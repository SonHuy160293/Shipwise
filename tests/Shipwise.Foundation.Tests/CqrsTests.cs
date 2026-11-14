using Microsoft.Extensions.DependencyInjection;
using Shipwise.SharedKernel.Abstractions.Message;
using Shipwise.SharedKernel.Abstractions.Persistence;
using Shipwise.SharedKernel.Abstractions.Shared;
using Shipwise.SharedKernel.Behaviors;
using Shipwise.SharedKernel.Cqrs;
using Shipwise.SharedKernel.DependencyInjection;
using Xunit;

namespace Shipwise.Foundation.Tests;

public sealed class CqrsTests
{
    [Fact]
    public async Task PipelineOrdersValidationBeforeTransactionAndRollsBackHandlerFailure()
    {
        var unit = new RecordingUnitOfWork();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IUnitOfWork>(unit);
        services.AddSelfMadeCqrs(typeof(CqrsTests).Assembly).AddApplicationPipelineBehaviors();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<CqrsValidationException>(() => sender.SendAsync(new SampleCommand(false, false)));
        Assert.Equal(0, unit.Transactions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(new SampleCommand(true, true)));
        Assert.True(unit.RolledBack);
        Assert.False(unit.Committed);
        var result = await sender.SendAsync(new SampleCommand(true, false));
        Assert.True(result.IsSuccess);
        Assert.True(unit.Committed);
        int count = unit.Transactions;
        await sender.SendAsync(new SampleQuery());
        Assert.Equal(count, unit.Transactions);
    }

    public sealed record SampleCommand(bool Valid, bool Fail) : ICommand;

    public sealed record SampleQuery : IQuery<int>;

    public sealed class SampleValidator : IValidator<SampleCommand>
    {
        public Task ValidateAsync(SampleCommand request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return request.Valid ? Task.CompletedTask : throw new CqrsValidationException(["Invalid sample."]);
        }
    }

    public sealed class SampleHandler : ICommandHandler<SampleCommand>
    {
        public Task<Result> HandleAsync(SampleCommand request, CancellationToken cancellationToken)
            => request.Fail ? throw new InvalidOperationException("Sample failure.") : Task.FromResult(Result.Success());
    }

    public sealed class QueryHandler : IQueryHandler<SampleQuery, int>
    {
        public Task<Result<int>> HandleAsync(SampleQuery request, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success(42));
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int Transactions { get; private set; }
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public async Task<TResponse> ExecuteInTransactionAsync<TResponse>(Func<CancellationToken, Task<TResponse>> action, CancellationToken cancellationToken = default)
        {
            Transactions++;
            try
            {
                var response = await action(cancellationToken);
                Committed = true;
                return response;
            }
            catch
            {
                RolledBack = true;
                throw;
            }
        }
    }
}

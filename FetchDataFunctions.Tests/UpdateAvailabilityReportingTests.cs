using System.Data.Common;
using FetchDataFunctions.Models;
using Microsoft.DurableTask;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FetchDataFunctions.Tests;

[TestFixture]
public class UpdateAvailabilityReportingTests
{
    [Test]
    public async Task Orchestrator_ReportingFailure_DoesNotInvalidateCompletedHutRefresh()
    {
        var context = new ReportingOrchestration(FailedReporting(new TimeoutException()));
        var functions = CreateFunctions(null!);

        await functions.UpdateAvailabilityOrchestrator(context);

        Assert.That(context.CompletedHutIds, Is.EqualTo(new[] { 29 }));
        Assert.That(context.ReportingAttempted, Is.True);
    }

    [TestCaseSource(nameof(ReportingCancellations))]
    public async Task Orchestrator_ReportingCancellation_Propagates(Exception failure)
    {
        var context = new ReportingOrchestration(failure);
        var functions = CreateFunctions(null!);

        var actual = await Assert.CatchAsync<Exception>(() => functions.UpdateAvailabilityOrchestrator(context));

        Assert.That(actual, Is.SameAs(failure));
        Assert.That(context.CompletedHutIds, Is.EqualTo(new[] { 29 }));
    }

    [Test]
    public async Task Orchestrator_HutRefreshFailure_IsNotTreatedAsOptionalReportingFailure()
    {
        var failure = new TaskFailedException(nameof(UpdateAvailabilityFunctions.UpdateHutAvailability), 0,
            TaskFailureDetails.FromException(new InvalidOperationException()));
        var context = new ReportingOrchestration(null, failure);
        var functions = CreateFunctions(null!);

        var actual = await Assert.CatchAsync<TaskFailedException>(() =>
            functions.UpdateAvailabilityOrchestrator(context));

        Assert.That(actual, Is.SameAs(failure));
        Assert.That(context.ReportingAttempted, Is.False);
    }

    [TestCase(null)]
    [TestCase(17)]
    public async Task Reporting_AllowsLogThrottledSnapshot_WithoutChangingOtherCommandTimeouts(int? configuredTimeout)
    {
        var commands = new ReportingCommands();
        var factory = CreateFactory(commands, configuredTimeout);
        var functions = CreateFunctions(factory);

        await functions.UpdateAvailabilityReporting(new object(), CancellationToken.None);

        Assert.That(commands.ReportingCompleted, Is.True,
            "The snapshot must have enough time to finish, not have its timeout swallowed.");
        await using var nextContext = await factory.CreateDbContextAsync();
        await nextContext.Database.ExecuteSqlRawAsync("SELECT 1");
        Assert.That(commands.OtherCommandTimeout, Is.EqualTo(configuredTimeout ?? 30),
            "The reporting budget must not leak into a reused pooled context.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Reporting_PropagatesFailureOrCancellation_AndReturnsContextToPool(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        Exception failure = cancel
            ? new OperationCanceledException(cancellation.Token)
            : new InvalidOperationException("Reporting write failed");
        var commands = new ReportingCommands(failure, cancellation);
        var factory = CreateFactory(commands, 17);
        var functions = CreateFunctions(factory);

        var actual = await Assert.CatchAsync<Exception>(() =>
            functions.UpdateAvailabilityReporting(new object(), cancellation.Token));

        Assert.That(actual, Is.SameAs(failure));
        await using var nextContext = await factory.CreateDbContextAsync();
        await nextContext.Database.ExecuteSqlRawAsync("SELECT 1");
        Assert.That(commands.OtherCommandTimeout, Is.EqualTo(17));
    }

    private static UpdateAvailabilityFunctions CreateFunctions(IDbContextFactory<AlpinehutsDbContext> factory) =>
        new(null!, factory, null!, TimeProvider.System, NullLogger<UpdateAvailabilityFunctions>.Instance);

    private static TaskFailedException FailedReporting(Exception failure) =>
        new(nameof(UpdateAvailabilityFunctions.UpdateAvailabilityReporting), 1,
            TaskFailureDetails.FromException(failure));

    private static IEnumerable<Exception> ReportingCancellations()
    {
        yield return new OperationCanceledException();
        yield return FailedReporting(new OperationCanceledException());
        yield return FailedReporting(new TaskCanceledException());
        yield return FailedReporting(new InvalidOperationException("Activity invocation failed",
            new OperationCanceledException()));
    }

    private sealed class ReportingOrchestration(Exception? reportingFailure, Exception? hutFailure = null)
        : TaskOrchestrationContext
    {
        public List<int> CompletedHutIds { get; } = [];
        public bool ReportingAttempted { get; private set; }
        public override TaskName Name => nameof(UpdateAvailabilityFunctions.UpdateAvailabilityOrchestrator);
        public override string InstanceId => "reporting-boundary-test";
        public override ParentOrchestrationInstance? Parent => null;
        public override DateTime CurrentUtcDateTime => new(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc);
        public override bool IsReplaying => false;
        protected override ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;
        public override T GetInput<T>() => (T)(object)new List<int> { 29 };

        public override Task<T> CallActivityAsync<T>(TaskName name, object? input = null, TaskOptions? options = null)
        {
            if (name.Name == nameof(UpdateAvailabilityFunctions.UpdateHutAvailability))
            {
                if (hutFailure != null)
                {
                    return Task.FromException<T>(hutFailure);
                }
                CompletedHutIds.Add((int)input!);
                return Task.FromResult((T)(object)new UpdateAvailabilityResult());
            }
            if (name.Name == nameof(UpdateAvailabilityFunctions.UpdateAvailabilityReporting))
            {
                ReportingAttempted = true;
                return reportingFailure != null
                    ? Task.FromException<T>(reportingFailure)
                    : Task.FromResult(default(T)!);
            }
            throw new NotSupportedException();
        }

        public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public override void SendEvent(string instanceId, string eventName, object payload) =>
            throw new NotSupportedException();
        public override void SetCustomStatus(object? customStatus) => throw new NotSupportedException();
        public override Task<T> CallSubOrchestratorAsync<T>(
            TaskName name, object? input = null, TaskOptions? options = null) => throw new NotSupportedException();
        public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) =>
            throw new NotSupportedException();
        public override Guid NewGuid() => throw new NotSupportedException();
    }

    private static PooledDbContextFactory<AlpinehutsDbContext> CreateFactory(
        ReportingCommands commands, int? configuredTimeout)
    {
        var options = new DbContextOptionsBuilder<AlpinehutsDbContext>()
            .UseSqlServer("Server=reporting-tests.invalid;Database=Offline;Integrated Security=true",
                sql =>
                {
                    if (configuredTimeout.HasValue)
                    {
                        sql.CommandTimeout(configuredTimeout.Value);
                    }
                })
            .AddInterceptors(new OfflineConnection(), commands)
            .Options;
        return new PooledDbContextFactory<AlpinehutsDbContext>(options, poolSize: 1);
    }

    private sealed class OfflineConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private sealed class ReportingCommands(
        Exception? failure = null, CancellationTokenSource? cancellation = null) : DbCommandInterceptor
    {
        public bool ReportingCompleted { get; private set; }
        public int? OtherCommandTimeout { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText == "dbo.UpdateAvailabilityReporting")
            {
                if (failure != null)
                {
                    if (failure is OperationCanceledException)
                    {
                        cancellation!.Cancel();
                    }
                    throw failure;
                }

                // Model a log-throttled snapshot that exceeds SQL Client's default budget without waiting.
                if (command.CommandTimeout < 60)
                {
                    throw new TimeoutException("The snapshot exceeds the command budget.");
                }
                ReportingCompleted = true;
            }
            else
            {
                OtherCommandTimeout = command.CommandTimeout;
            }

            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
        }
    }
}

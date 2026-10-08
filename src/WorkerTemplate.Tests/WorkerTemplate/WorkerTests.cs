using System.Diagnostics;
using FakeItEasy;
using FakeItEasy.Core;
using Microsoft.Extensions.Logging;
using WorkerTemplate;

namespace WorkerTemplate.Tests.WorkerTemplate;

[TestClass]
public class WorkerTests
{
    [TestMethod]
    public async Task StartAsync_LogsWorkerRunning_AndStopAsyncCompletes()
    {
        var logger = A.Fake<ILogger<Worker>>();
        A.CallTo(() => logger.IsEnabled(LogLevel.Information)).Returns(true);

        var firstHeartbeat = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondHeartbeat = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var heartbeatCount = 0;

        A.CallTo(logger)
            .Where(call => IsHeartbeatLog(call))
            .Invokes(call =>
            {
                if (Interlocked.Increment(ref heartbeatCount) == 1)
                {
                    firstHeartbeat.TrySetResult(call.Arguments[2]?.ToString() ?? string.Empty);
                }
                else
                {
                    secondHeartbeat.TrySetResult();
                }
            });

        using var worker = new Worker(logger);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await worker.StartAsync(timeout.Token);
            var message = await firstHeartbeat.Task.WaitAsync(timeout.Token);
            await secondHeartbeat.Task.WaitAsync(timeout.Token);
            stopwatch.Stop();

            Assert.IsTrue(
                message.StartsWith("Worker running at:", StringComparison.Ordinal),
                $"Unexpected log message: {message}");
            Assert.IsTrue(
                stopwatch.Elapsed >= TimeSpan.FromMilliseconds(750) &&
                stopwatch.Elapsed <= TimeSpan.FromSeconds(3),
                $"Expected the second log after approximately one second, but it took {stopwatch.Elapsed}.");
        }
        finally
        {
            using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await worker.StopAsync(shutdownTimeout.Token);
        }

        A.CallTo(() => logger.IsEnabled(LogLevel.Information))
            .MustHaveHappenedTwiceOrMore();
        A.CallTo(logger)
            .Where(call => IsHeartbeatLog(call))
            .MustHaveHappenedTwiceOrMore();
    }

    private static bool IsHeartbeatLog(IFakeObjectCall call)
    {
        return call.Method.Name == nameof(ILogger.Log)
            && call.Arguments[0] is LogLevel.Information
            && call.Arguments[1] is EventId { Id: 0 }
            && call.Arguments[3] is null
            && call.Arguments[2] is IEnumerable<KeyValuePair<string, object?>> state
            && state.Any(property =>
                property.Key == "{OriginalFormat}"
                && property.Value is "Worker running at: {time}")
            && state.Any(property =>
                property.Key == "time"
                && property.Value is DateTimeOffset);
    }
}

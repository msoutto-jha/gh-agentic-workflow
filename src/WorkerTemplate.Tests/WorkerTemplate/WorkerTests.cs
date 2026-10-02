using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WorkerTemplate;

namespace WorkerTemplate.Tests.WorkerTemplate;

[TestClass]
public class WorkerTests
{
    [TestMethod]
    public async Task StartAsync_LogsWorkerRunning_AndStopAsyncCompletes()
    {
        var logger = new RecordingLogger<Worker>();
        var worker = new Worker(logger);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stopwatch = Stopwatch.StartNew();

        await worker.StartAsync(timeout.Token);
        var message = await logger.LoggedMessage.WaitAsync(timeout.Token);
        await logger.SecondLoggedMessage.WaitAsync(timeout.Token);
        stopwatch.Stop();

        Assert.IsTrue(
            message.StartsWith("Worker running at:", StringComparison.Ordinal),
            $"Unexpected log message: {message}");
        Assert.IsTrue(
            stopwatch.Elapsed >= TimeSpan.FromMilliseconds(750) &&
            stopwatch.Elapsed <= TimeSpan.FromSeconds(3),
            $"Expected the second log after approximately one second, but it took {stopwatch.Elapsed}.");

        await worker.StopAsync(timeout.Token);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly TaskCompletionSource<string> loggedMessage =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<string> secondLoggedMessage =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int loggedMessageCount;

        public Task<string> LoggedMessage => loggedMessage.Task;

        public Task<string> SecondLoggedMessage => secondLoggedMessage.Task;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
            {
                var message = formatter(state, exception);
                if (Interlocked.Increment(ref loggedMessageCount) == 1)
                {
                    loggedMessage.TrySetResult(message);
                }
                else
                {
                    secondLoggedMessage.TrySetResult(message);
                }
            }
        }
    }
}

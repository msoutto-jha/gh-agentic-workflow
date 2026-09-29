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

        await worker.StartAsync(timeout.Token);
        var message = await logger.LoggedMessage.WaitAsync(timeout.Token);

        Assert.IsTrue(
            message.StartsWith("Worker running at:", StringComparison.Ordinal),
            $"Unexpected log message: {message}");

        await worker.StopAsync(timeout.Token);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly TaskCompletionSource<string> loggedMessage =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> LoggedMessage => loggedMessage.Task;

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
                loggedMessage.TrySetResult(formatter(state, exception));
            }
        }
    }
}

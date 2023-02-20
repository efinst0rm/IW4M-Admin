using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SharedLibraryCore.Events;

public static class EventExtensions
{
    public static async Task InvokeAsync<TEventType>(this Func<TEventType, CancellationToken, Task> function,
        TEventType eventArgType, CancellationToken token)
    {
        if (function is null)
        {
            return;
        }

        await Parallel.ForEachAsync(function.GetInvocationList().Cast<Func<TEventType, CancellationToken, Task>>(),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 5,
                CancellationToken = token
            }, async (handler, innerToken) =>
            {
                try
                {
                    using var timeoutToken = new CancellationTokenSource(Utilities.DefaultCommandTimeout);
                    using var tokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(innerToken, timeoutToken.Token);
                    await handler(eventArgType, tokenSource.Token).WithWaitCancellation(tokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    Utilities.DefaultLogger.LogWarning("Event timed out {Type}", eventArgType);
                }
                catch (Exception ex)
                {
                    Utilities.DefaultLogger.LogError(ex, "Could not complete invoke for {EventType}",
                        eventArgType.GetType().Name);
                }
            });
    }
}

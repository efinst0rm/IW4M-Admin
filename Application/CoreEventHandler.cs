using System.Collections.Concurrent;
using SharedLibraryCore;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Interfaces.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application
{
    public class CoreEventHandler : ICoreEventHandler
    {
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _onProcessingEvents = new(10, 10);
        private readonly ConcurrentQueue<(IManager, CoreEvent)> _runningEventTasks = new();
        private CancellationToken _cancellationToken;

        private static readonly GameEvent.EventType[] OverrideEvents = {
            GameEvent.EventType.Connect,
            GameEvent.EventType.Disconnect,
            GameEvent.EventType.Quit,
            GameEvent.EventType.Stop
        };

        public CoreEventHandler(ILogger<CoreEventHandler> logger)
        {
            _logger = logger;
        }

        public void QueueEvent(IManager manager, CoreEvent coreEvent)
        {
            _runningEventTasks.Enqueue((manager, coreEvent));
        }

        public async Task StartProcessing(CancellationToken token)
        {
            _cancellationToken = token;

            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _onProcessingEvents.WaitAsync(_cancellationToken);

                    if (!_runningEventTasks.TryDequeue(out var coreEvent))
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    _ = Task.Factory.StartNew(async () => await GetEventTask(coreEvent.Item1, coreEvent.Item2),
                        _cancellationToken);
                }
                finally
                {
                    _onProcessingEvents.Release(1);
                }
            }
        }

        private Task GetEventTask(IManager manager, CoreEvent coreEvent)
        {
            return coreEvent switch
            {
                GameEvent gameEvent => BuildLegacyEventTask(manager, coreEvent, gameEvent, Task.Run(() => { })),
                GameServerEvent gameServerEvent => IGameServerEventSubscriptions.InvokeEventAsync(gameServerEvent,
                    CancellationToken.None),
                ManagementEvent managementEvent => IManagementEventSubscriptions.InvokeEventAsync(managementEvent,
                    CancellationToken.None),
                _ => Task.CompletedTask
            };
        }

        private Task BuildLegacyEventTask(IManager manager, CoreEvent coreEvent, GameEvent gameEvent, Task invokeTask)
        {
            if (manager.IsRunning || OverrideEvents.Contains(gameEvent.Type))
            {
                EventApi.OnGameEvent(gameEvent);

                invokeTask = manager.ExecuteEvent(gameEvent).ContinueWith(_ =>
                    IGameEventSubscriptions.InvokeEventAsync(coreEvent, CancellationToken.None));
            }
            else
            {
                _logger.LogDebug("Skipping event as we're shutting down {EventId}", gameEvent.Id);
            }

            return invokeTask;
        }
    }
}

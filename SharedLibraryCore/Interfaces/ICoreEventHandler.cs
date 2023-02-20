﻿using System.Threading;
using System.Threading.Tasks;
using SharedLibraryCore.Events;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     handles games events (from log, manual events, etc)
    /// </summary>
    public interface ICoreEventHandler
    {
        /// <summary>
        ///     Add a core event event to the queue to be processed
        /// </summary>
        /// <param name="manager"><see cref="IManager"/></param>
        /// <param name="coreEvent"><see cref="CoreEvent"/></param>
        void QueueEvent(IManager manager, CoreEvent coreEvent);

        Task StartProcessing(CancellationToken token);
    }
}

// Copyright 2014 Serilog Contributors
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Serilog.Sinks.PeriodicBatching;
using LogEvent = Serilog.Sinks.SignalR.Data.LogEvent;

namespace Serilog.Sinks.SignalR
{
    /// <summary>
    /// Writes log events as messages to a SignalR hub.
    /// </summary>
    public class SignalRSink : IBatchedLogEventSink
    {
        readonly IFormatProvider _formatProvider;
        readonly IHubContext _context;
        readonly string[] _groupNames;
        readonly string[] _userIds;
        readonly string[] _excludedConnectionIds;

        /// <summary>
        /// A reasonable default for the number of events posted in
        /// each batch.
        /// </summary>
        public const int DefaultBatchPostingLimit = 5;

        /// <summary>
        /// A reasonable default time to wait between checking for event batches.
        /// </summary>
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Construct a sink posting to the specified database.
        /// </summary>
        /// <param name="context">The hub context.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="groupNames">Name of the Signalr group you are broadcasting the log event to. Default is All connections.</param>
        /// <param name="userIds">ID's of the Signalr Users you are broadcasting the log event to. Default is All Users.</param>
        /// <param name="excludedConnectionIds">Signalr connection ID's to exclude from broadcast.</param>
        public SignalRSink(IHubContext context, int batchPostingLimit, TimeSpan period, IFormatProvider formatProvider, string[] groupNames = null, string[] userIds = null, string[] excludedConnectionIds = null)
        {
            _formatProvider = formatProvider;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _groupNames = groupNames;
            _userIds = userIds;
            _excludedConnectionIds = excludedConnectionIds ?? Array.Empty<string>();
        }

        /// <summary>
        /// Emit a batch of log events, running asynchronously.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        public Task EmitBatchAsync(IEnumerable<Events.LogEvent> events)
        {
            // This sink doesn't use batching to send events, instead only using
            // PeriodicBatchingSink to manage the worker thread; requires some consideration.

            foreach (var logEvent in events)
            {
                dynamic target;
                // target the specified clients while opting out the excluded connections
                if (_groupNames != null && _groupNames != Array.Empty<string>())
                    target = _context.Clients.Groups(_groupNames, _excludedConnectionIds);
                else if (_userIds != null && _userIds != Array.Empty<string>())
                    target = _context.Clients.Users(_userIds);
                else
                    target = _context.Clients.AllExcept(_excludedConnectionIds);

                // send the broadcast to the targeted connections
                target.sendLogEvent(new LogEvent(logEvent, logEvent.RenderMessage(_formatProvider)));
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Dispose of resources held by the sink.
        /// </summary>
        /// <returns></returns>
        public Task OnEmptyBatchAsync()
        {
            return Task.FromResult(0);
        }
    }
}

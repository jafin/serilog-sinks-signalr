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
using Microsoft.AspNet.SignalR.Client;
using Serilog.Sinks.PeriodicBatching;
using LogEvent = Serilog.Sinks.SignalR.Data.LogEvent;

namespace Serilog.Sinks.SignalR
{
    /// <summary>
    /// Writes log events as messages to a SignalR hub.
    /// </summary>
    public class SignalRClientSink : IBatchedLogEventSink
    {
        readonly IFormatProvider _formatProvider;
        readonly HubConnection _connection;
        readonly IHubProxy _hubProxy;
        readonly string[] _groupNames;
        readonly string[] _userIds;

        /// <summary>
        /// Construct a sink posting to the specified database.
        /// </summary>
        /// <param name="url">The url of the hub. http://localhost:8080.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        /// <param name="hub">The name of the Signalr hub class. Default is LogHub</param>
        /// <param name="groupNames">Names of the Signalr groups you are broadcasting the log event to. Default is All Groups.</param>
        /// <param name="userIds">ID's of the Signalr Users you are broadcasting the log event to. Default is All Users.</param>
        public SignalRClientSink(string url, IFormatProvider formatProvider,
            string hub = "LogHub", string[] groupNames = null, string[] userIds = null)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));

            _formatProvider = formatProvider;
            _groupNames = groupNames ?? Array.Empty<string>();
            _userIds = userIds ?? Array.Empty<string>();

            _connection = new HubConnection(url);
            _hubProxy = _connection.CreateHubProxy(hub);
            // does not block, but will take some time to initialize
            _connection.Start();
        }

        /// <summary>
        /// Emit the provided log event to the sink.
        /// </summary>
        /// <param name="events"></param>
        public async Task EmitBatchAsync(IEnumerable<Events.LogEvent> events)
        {
            // This sink doesn't use batching to send events, instead only using
            // PeriodicBatchingSink to manage the worker thread; requires some consideration.

            foreach (var logEvent in events)
            {
                // send the log message to the hub
                switch (_connection.State)
                {
                    case ConnectionState.Connected:
                        await _hubProxy.Invoke("receiveLogEvent", _groupNames, _userIds,
                            new LogEvent(logEvent, logEvent.RenderMessage(_formatProvider)));
                        break;
                    case ConnectionState.Disconnected:
                        // attempt to restart the connection
                        await _connection.Start();
                        break;
                }
            }
        }

        /// <summary>
        /// Dispose the sink.
        /// </summary>
        /// <returns></returns>
        public Task OnEmptyBatchAsync()
        {
            return Task.FromResult(0);
        }
    }
}
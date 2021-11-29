/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using Apache.Qpid.Proton.Buffer;
using Apache.Qpid.Proton.Client.Concurrent;

namespace Apache.Qpid.Proton.Client.Transport
{
   /// <summary>
   /// Base transport interface which defines the API of a wire
   /// level IO transport used by the client.
   /// </summary>
   public interface ITransport
   {
      /// <summary>
      /// Returns the event loop that this transport is registed against,
      /// the event loop should never have its lifetime linked to a transport
      /// as the client connection will use a single event loop for the
      /// duration of its lifetime.
      /// </summary>
      IEventLoop EventLoop { get; }

      /// <summary>
      /// Initiates an orderly close of the transport.
      /// </summary>
      void Close();

      /// <summary>
      /// Initiates the IO level connect that will trigger IO events
      /// in the transport event loop based on the outcome.
      /// </summary>
      void Connect();

      /// <summary>
      /// Queues the given buffer for write using this transport and
      /// registers a completion action that will be triggered when
      /// the write is actually performed.
      /// </summary>
      /// <param name="buffer">The buffer to write</param>
      /// <param name="writeCompleteAction">optional action to be performed</param>
      void Write(IProtonBuffer buffer, Action writeCompleteAction);

      /// <summary>
      /// Configures the read handler used to process incoming bytes that
      /// are read by this transport. The handler is always invoked within
      /// the registered event loop.
      /// </summary>
      /// <param name="readHandler">Handler that is invoked on read</param>
      /// <returns>This transport instance.</returns>
      ITransport TransportReadHandler(Action<IProtonBuffer> readHandler);

   }
}
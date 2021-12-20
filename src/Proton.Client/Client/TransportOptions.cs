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

namespace Apache.Qpid.Proton.Client
{
   /// <summary>
   /// Options that control the I/O level transport configuration.
   /// </summary>
   public class TransportOptions
   {
      public static readonly int DEFAULT_SEND_BUFFER_SIZE = 64 * 1024;
      public static readonly int DEFAULT_RECEIVE_BUFFER_SIZE = DEFAULT_SEND_BUFFER_SIZE;
      public static readonly int DEFAULT_TRAFFIC_CLASS = 0;
      public static readonly bool DEFAULT_TCP_NO_DELAY = true;
      public static readonly bool DEFAULT_TCP_KEEP_ALIVE = false;
      public static readonly int DEFAULT_SO_LINGER = Int32.MinValue;
      public static readonly int DEFAULT_SO_TIMEOUT = -1;
      public static readonly int DEFAULT_CONNECT_TIMEOUT = 60000;
      public static readonly int DEFAULT_TCP_PORT = 5672;
      public static readonly int DEFAULT_LOCAL_PORT = 0;
      public static readonly bool DEFAULT_USE_WEBSOCKETS = false;
      public static readonly int DEFAULT_WEBSOCKET_MAX_FRAME_SIZE = 65535;

      /// <summary>
      /// Creates a default transport options instance.
      /// </summary>
      public TransportOptions() : base()
      {
      }

      /// <summary>
      /// Create a transport options instance that copies the configuration from the given instance.
      /// </summary>
      /// <param name="other">The target options instance to copy</param>
      public TransportOptions(TransportOptions other) : this()
      {
         other.CopyInto(this);
      }

      /// <summary>
      /// Clone this options instance, changes to the cloned options are not reflected
      /// in this options instance.
      /// </summary>
      /// <returns>A deep copy of this options instance.</returns>
      public object Clone()
      {
         return MemberwiseClone();
      }

      internal TransportOptions CopyInto(TransportOptions other)
      {
         other.DefaultTcpPort = DefaultTcpPort;

         return this;
      }

      /// <summary>
      /// Configures the default TCP port that all client connections should use if
      /// none is provided in the connect call.
      /// </summary>
      public int DefaultTcpPort { get; set; } = DEFAULT_TCP_PORT;

      /// <summary>
      /// Assigned local address that the client should bind to when creating a
      /// connection to the remote. The user is responsible for ensuring this
      /// local address is free and can be bound to otherwise an error will be
      /// thrown on connect.
      /// </summary>
      public string LocalAddress { get; set; }

      /// <summary>
      /// Assigned local port that the connection should bind to when attempting to
      /// connect to the remote.  The user is responsible for ensuring this local
      /// port is free otherwise an error will be thrown on connect.
      /// </summary>
      public int LocalPort { get; set; } = -1;

   }
}
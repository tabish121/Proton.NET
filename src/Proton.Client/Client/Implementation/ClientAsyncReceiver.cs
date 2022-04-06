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
using System.Threading.Tasks;
using Apache.Qpid.Proton.Logging;

namespace Apache.Qpid.Proton.Client.Implementation
{
   /// <summary>
   /// Client asynchronous receiver implementation which provides a wrapper around the
   /// proton receiver link and processes incoming deliveries with options for queueing
   /// with a credit window.
   /// </summary>
   public class ClientAsyncReceiver : ClientLinkType<IAsyncReceiver, Engine.IReceiver>, IAsyncReceiver
   {
      private static readonly IProtonLogger LOG = ProtonLoggerFactory.GetLogger<ClientAsyncReceiver>();

      private readonly AsyncReceiverOptions options;
      private readonly string receiverId;

      private TaskCompletionSource<IAsyncReceiver> drainingFuture;

      internal ClientAsyncReceiver(ClientSession session, AsyncReceiverOptions options, String receiverId, Engine.IReceiver receiver)
       : base(session, receiver)
      {
         this.options = options;
         this.receiverId = receiverId;
         this.protonLink.LinkedResource = this;

         if (options.CreditWindow > 0)
         {
            protonLink.AddCredit(options.CreditWindow);
         }
      }

      public event EventHandler<AsyncReceiveEventArgs> DeliveryReceived;

      public IReceiver AddCredit(uint credit)
      {
         throw new NotImplementedException();
      }

      public Task<IAsyncReceiver> AddCreditAsync(uint credit)
      {
         throw new NotImplementedException();
      }

      public IReceiver Drain()
      {
         throw new NotImplementedException();
      }

      public Task<IAsyncReceiver> DrainAsync()
      {
         throw new NotImplementedException();
      }

      internal ClientAsyncReceiver Open()
      {
         throw new NotImplementedException();
      }
   }
}
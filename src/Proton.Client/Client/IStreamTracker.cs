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

namespace Apache.Qpid.Proton.Client
{
   /// <summary>
   /// Special streaming sender related tracker that is linked to a stream
   /// sender message and provides the tracker functions for those types of
   /// messages.
   /// </summary>
   public interface IStreamTracker
   {
      /// <summary>
      /// Returns the parent sender instance that sent the delivery that is now being tacked.
      /// </summary>
      IStreamSender Sender { get; }

      /// <summary>
      /// Indicates if the sent delivery has already been locally settled.
      /// </summary>
      bool Settled { get; }

      /// <summary>
      /// Retrieve the currently applied delivery state for the sent delivery.
      /// </summary>
      IDeliveryState State { get; }

      /// <summary>
      /// Indicates if the sent delivery has already been remotely settled.
      /// </summary>
      bool RemoteSettled { get; }

      /// <summary>
      /// Retrieve the currently applied delivery state by the remote for the sent delivery.
      /// </summary>
      IDeliveryState RemoteState { get; }

      /// <summary>
      /// Settles the sent delivery if not performing auto-settlement on the sender.
      /// </summary>
      /// <returns>This tracker instance</returns>
      IStreamTracker Settle();

      /// <summary>
      /// Settles the sent delivery if not performing auto-settlement on the sender
      /// and returns a Task that will be completed once any IO operations required
      /// by the settlement have compelted.
      /// </summary>
      /// <returns>This tracker instance</returns>
      Task<IStreamTracker> SettleAsync();

      /// <summary>
      /// Apply the delivery state and optionally settle the sent delivery with the remote
      /// </summary>
      /// <param name="state">The delivery state to apply to the sent delivery</param>
      /// <param name="settle">Optionally settle the delivery that was sent</param>
      /// <returns>This tracker instance</returns>
      IStreamTracker Disposition(IDeliveryState state, bool settle);

      /// <summary>
      /// Apply the delivery state and optionally settle the sent delivery with the remote.
      /// The method returns a Task that will be completed once any required IO operations
      /// in order to apply the dispostion have been completed.
      /// </summary>
      /// <param name="state">The delivery state to apply to the sent delivery</param>
      /// <param name="settle">Optionally settle the delivery that was sent</param>
      /// <returns>This tracker instance</returns>
      Task<IStreamTracker> DispositionAsync(IDeliveryState state, bool settle);

      /// <summary>
      /// Gets a task that will be completed once the remote has settled the sent
      /// delivery, or will indicate an error if the connection fails before the
      /// remote can settle. If the sender sent the tracked delivery settled the
      /// task returned will already be completed.
      /// </summary>
      Task<IStreamTracker> SettlementTask { get; }

      /// <summary>
      /// Waits for the remote to settle the sent delivery unless the delivery was already
      /// settled by the remote or the delivery was sent already settled.
      /// </summary>
      /// <returns>This tracker instance</returns>
      IStreamTracker AwaitSettlement();

      /// <summary>
      /// Waits for the remote to settle the sent delivery unless the delivery was already
      /// settled by the remote or the delivery was sent already settled.
      /// </summary>
      /// <param name="timeout">The duration to wait for the remote to settle the delivery</param>
      /// <returns>This tracker instance</returns>
      IStreamTracker AwaitSettlement(TimeSpan timeout);

      /// <summary>
      /// Waits for the remote to accept and settle the sent delivery unless the delivery
      /// was already settled by the remote or the delivery was sent already settled.
      /// </summary>
      /// <remarks>
      /// If the remote send back a delivery state other than accepted then this method
      /// will throw an ClientDeliveryStateException to indicate the expected outcome
      /// was not achieved.
      /// </remarks>
      /// <returns>This tracker instance</returns>
      IStreamTracker AwaitAccepted();

      /// <summary>
      /// Waits for the remote to accept and settle the sent delivery unless the delivery
      /// was already settled by the remote or the delivery was sent already settled.
      /// </summary>
      /// <remarks>
      /// If the remote send back a delivery state other than accepted then this method
      /// will throw an ClientDeliveryStateException to indicate the expected outcome
      /// was not achieved.
      /// </remarks>
      /// <param name="timeout">The duration to wait for the remote to accept the delivery</param>
      /// <returns>This tracker instance</returns>
      IStreamTracker AwaitAccepted(TimeSpan timeout);

   }
}
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
   public class AsyncReceiveEventArgs : EventArgs
   {
      /// <summary>
      /// Returns the delivery instance that manages the delivery that was
      /// received from the remote AMQP peer.
      /// </summary>
      public IDelivery Delivery { get; internal set; }

      /// <summary>
      /// Shortcut to the asynchronous receiver that fired this delivery event.
      /// </summary>
      public IAsyncReceiver Receiver { get; internal set; }

   }

   /// <summary>
   /// A single AMQP asynchronous receiver interface that provides a means of
   /// attaching an event handler which will be notified when a delivery from
   /// a remote AMQP peer is received.
   /// </summary>
   /// <remarks>
   /// This is a proposed API and has no implementation at this time.
   /// </remarks>
   public interface IAsyncReceiver : ILink<IAsyncReceiver>
   {
      /// <summary>
      /// The delivery event which fires for each new incoming delivery. Dispatch of incoming
      /// deliveries is done synchronously and if auto accept of deliveries is enabled the delivery
      /// is accepted once the event handler has completed. If an error is thrown from the event
      /// handler the delivery is settled and the delivery failed dispostion configured in the
      /// asynchronous receiver options is applied (default is modified with delivery failed and
      /// undeliverable here).
      /// </summary>
      public event EventHandler<AsyncReceiveEventArgs> DeliveryReceived;

      /// <summary>
      /// Adds credit to the Receiver link for use when there receiver has not been configured with
      /// with a credit window.  When credit window is configured credit replenishment is automatic
      /// and calling this method will result in an exception indicating that the operation is invalid.
      ///
      /// If the Receiver is draining and this method is called an exception will be thrown to
      /// indicate that credit cannot be replenished until the remote has drained the existing link
      /// credit.
      /// </summary>
      /// <param name="credit">The amount of new credit to add to the existing credit if any</param>
      /// <returns>This receiver instance.</returns>
      IReceiver AddCredit(uint credit);

      /// <summary>
      /// Asynchronously Adds credit to the Receiver link for use when there receiver has not been
      /// configured with with a credit window.  When credit window is configured credit replenishment
      /// is automatic and calling this method will result in an exception indicating that the operation
      /// is invalid.
      ///
      /// If the Receiver is draining and this method is called an exception will be thrown to
      /// indicate that credit cannot be replenished until the remote has drained the existing link
      /// credit.
      /// </summary>
      /// <param name="credit">The amount of new credit to add to the existing credit if any</param>
      /// <returns>This receiver instance.</returns>
      Task<IAsyncReceiver> AddCreditAsync(uint credit);

      /// <summary>
      /// Requests the remote to drain previously granted credit for this receiver link.
      /// The remote will either send all available deliveries up to the currently granted
      /// link credit or will report it has none to send an link credit will be set to zero.
      /// This method will block until the remote answers the drain request or the configured
      /// drain timeout expires.
      /// </summary>
      /// <returns>This receiver instance once the remote reports drain completed</returns>
      IReceiver Drain();

      /// <summary>
      /// Requests the remote to drain previously granted credit for this receiver link.
      /// The remote will either send all available deliveries up to the currently granted
      /// link credit or will report it has none to send an link credit will be set to zero.
      /// The caller can wait on the returned task which will be signalled either after the
      /// remote reports drained or once the configured drain timeout is reached.
      /// </summary>
      /// <returns>A Task that will be completed when the remote reports drained.</returns>
      Task<IAsyncReceiver> DrainAsync();

   }
}
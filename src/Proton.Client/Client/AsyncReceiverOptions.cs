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
   public class AsyncReceiverOptions : ReceiverOptions, ICloneable
   {
      /// <summary>
      /// The default disposition that will be applied to any failed asynchronous delivery events.
      /// </summary>
      public static readonly IDeliveryState DefaultFailedDeliveryDisposition = IDeliveryState.Modified(true, true);

      /// <summary>
      /// Creates a default stream receiver options instance.
      /// </summary>
      public AsyncReceiverOptions() : base()
      {
      }

      /// <summary>
      /// Create a new stream receiver options instance whose settings are copied from the instance provided.
      /// </summary>
      /// <param name="other">The stream receiver options instance to copy</param>
      public AsyncReceiverOptions(AsyncReceiverOptions other) : this()
      {
         other.CopyInto(this);
      }

      /// <summary>
      /// Clone this options instance, changes to the cloned options are not reflected
      /// in this options instance.
      /// </summary>
      /// <returns>A deep copy of this options instance.</returns>
      public override object Clone()
      {
         return CopyInto(new AsyncReceiverOptions());
      }

      internal AsyncReceiverOptions CopyInto(AsyncReceiverOptions other)
      {
         other.FailedDeliveryDisposition = FailedDeliveryDisposition;

         return base.CopyInto(other) as AsyncReceiverOptions;
      }

      /// <summary>
      /// Configures a delivery state which will be applied to any delivery that triggers
      /// an exception from the set asynchronous delivery event handler.
      /// </summary>
      public IDeliveryState FailedDeliveryDisposition { get; set; }

   }
}
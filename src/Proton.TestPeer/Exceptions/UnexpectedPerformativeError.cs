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

namespace Apache.Qpid.Proton.Test.Driver.Exceptions
{
   /// <summary>
   /// Exception type that is thrown when an unexpected AMQP performative is received
   /// by the test driver during a scripted test run.
   /// </summary>
   public sealed class UnexpectedPerformativeError : AssertionError
   {
      public UnexpectedPerformativeError() : base()
      {
      }

      public UnexpectedPerformativeError(string message) : base(message)
      {
      }

      public UnexpectedPerformativeError(string message, Exception cause) : base(message, cause)
      {
      }
   }
}
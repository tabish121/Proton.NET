/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed With
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance With
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

using Apache.Qpid.Proton.Test.Driver.Codec.Primitives;
using Apache.Qpid.Proton.Test.Driver.Codec.Security;

namespace Apache.Qpid.Proton.Test.Driver.Actions
{
   /// <summary>
   /// Action type used to inject the SASL Performative into a test script to
   /// drive the SASL authentication lifecycle.
   /// </summary>
   public class SaslInitInjectAction : AbstractSaslPerformativeInjectAction<SaslInit>
   {
      private readonly SaslInit saslInit = new SaslInit();

      public SaslInitInjectAction(AMQPTestDriver driver) : base(driver)
      {
      }

      public override SaslInit Performative => saslInit;

      public SaslInitInjectAction WithMechanism(string mechanism)
      {
         saslInit.Mechanism = new Symbol(mechanism);
         return this;
      }

      public SaslInitInjectAction WithMechanism(Symbol mechanism)
      {
         saslInit.Mechanism = mechanism;
         return this;
      }

      public SaslInitInjectAction WithMechanism(byte[] response)
      {
         saslInit.InitialResponse = response;
         return this;
      }

      public SaslInitInjectAction WithMechanism(Binary response)
      {
         saslInit.InitialResponse = response.Array;
         return this;
      }

      public SaslInitInjectAction WithHostname(string hostname)
      {
         saslInit.Hostname = hostname;
         return this;
      }
   }
}
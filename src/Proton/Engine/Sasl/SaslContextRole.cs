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

namespace Apache.Qpid.Proton.Engine.Sasl
{
   /// <summary>
   /// Represents the Role of a SASL Context instance.
   /// </summary>
   public enum SaslContextRole
   {
      /// <summary>
      /// The SASL context is acting as a client
      /// </summary>
      Client,

      /// <summary>
      /// The SASL context is acting as a server
      /// </summary>
      Server
   }

   public static class SaslContextRoleExtension
   {
      public static bool IsServer(this SaslContextRole role)
      {
         return role == SaslContextRole.Server;
      }

      public static bool IsClient(this SaslContextRole role)
      {
         return role == SaslContextRole.Client;
      }
   }
}
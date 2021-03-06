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

namespace Apache.Qpid.Proton.Test.Driver
{
   /// <summary>
   /// Base interface for all scriptable test types.
   /// </summary>
   public interface IScriptedElement
   {
      /// <summary>
      /// Defines the type of scripted element that this entry implements.
      /// </summary>
      ScriptEntryType ScriptedType => throw new NotImplementedException("Must be implemented in base classes or interfaces");

   }
}
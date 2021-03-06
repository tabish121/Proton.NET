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

using System.IO;
using NUnit.Framework;
using Apache.Qpid.Proton.Buffer;
using Apache.Qpid.Proton.Codec.Utilities;

namespace Apache.Qpid.Proton.Codec
{
   [TestFixture]
   public class RegisteredTypeCodecTest : CodecTestSupport
   {
      [Test]
      public void TestEncodeDecodeRegisteredType()
      {
         DoTestEncodeDecodeRegisteredType(false);
      }

      [Test]
      public void TestEncodeDecodeRegisteredTypeFromStream()
      {
         DoTestEncodeDecodeRegisteredType(true);
      }

      private void DoTestEncodeDecodeRegisteredType(bool fromStream)
      {
         IProtonBuffer buffer = ProtonByteBufferAllocator.Instance.Allocate();
         Stream stream = new ProtonBufferInputStream(buffer);

         // Register the codec pair.
         encoder.RegisterDescribedTypeEncoder(new NoLocalTypeEncoder());
         decoder.RegisterDescribedTypeDecoder(new NoLocalTypeDecoder());
         streamDecoder.RegisterDescribedTypeDecoder(new NoLocalTypeDecoder());

         encoder.WriteObject(buffer, encoderState, NoLocalType.Instance);

         object result;
         if (fromStream)
         {
            result = streamDecoder.ReadObject(stream, streamDecoderState);
         }
         else
         {
            result = decoder.ReadObject(buffer, decoderState);
         }

         Assert.IsTrue(result is NoLocalType);
         NoLocalType resultTye = (NoLocalType)result;
         Assert.AreEqual(NoLocalType.Instance.Descriptor, resultTye.Descriptor);
      }
   }
}
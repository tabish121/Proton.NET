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
using System.IO;
using Apache.Qpid.Proton.Buffer;
using Apache.Qpid.Proton.Codec.Decoders.Primitives;
using Apache.Qpid.Proton.Types;
using Apache.Qpid.Proton.Types.Security;

namespace Apache.Qpid.Proton.Codec.Decoders.Security
{
   public sealed class SaslMechanismsTypeDecoder : AbstractDescribedTypeDecoder
   {
      private static readonly int RequiredListEntries = 1;

      public override Symbol DescriptorSymbol => SaslMechanisms.DescriptorSymbol;

      public override ulong DescriptorCode => SaslMechanisms.DescriptorCode;

      public override Type DecodesType => typeof(SaslMechanisms);

      public override object ReadValue(IProtonBuffer buffer, IDecoderState state)
      {
         ITypeDecoder decoder = state.Decoder.ReadNextTypeDecoder(buffer, state);

         return ReadType(buffer, state, CheckIsExpectedTypeAndCast<IListTypeDecoder>(decoder));
      }

      public override Array ReadArrayElements(IProtonBuffer buffer, IDecoderState state, int count)
      {
         ITypeDecoder decoder = state.Decoder.ReadNextTypeDecoder(buffer, state);

         CheckIsExpectedType<IListTypeDecoder>(decoder);

         SaslMechanisms[] result = new SaslMechanisms[count];
         for (int i = 0; i < count; ++i)
         {
            result[i] = ReadType(buffer, state, CheckIsExpectedTypeAndCast<IListTypeDecoder>(decoder));
         }

         return result;
      }

      public override void SkipValue(IProtonBuffer buffer, IDecoderState state)
      {
         ITypeDecoder decoder = state.Decoder.ReadNextTypeDecoder(buffer, state);

         CheckIsExpectedType<IListTypeDecoder>(decoder);

         decoder.SkipValue(buffer, state);
      }

      private static SaslMechanisms ReadType(IProtonBuffer buffer, IDecoderState state, IListTypeDecoder listDecoder)
      {
         SaslMechanisms result = new();

         _ = listDecoder.ReadSize(buffer, state);
         int count = listDecoder.ReadCount(buffer, state);

         if (count != RequiredListEntries)
         {
            throw new DecodeException("SASL Mechanisms must contain at least one entry: " + count);
         }
         else
         {
            result.Mechanisms = state.Decoder.ReadMultiple<Symbol>(buffer, state);
         }

         return result;
      }

      public override object ReadValue(Stream stream, IStreamDecoderState state)
      {
         IStreamTypeDecoder decoder = state.Decoder.ReadNextTypeDecoder(stream, state);

         return ReadType(stream, state, CheckIsExpectedTypeAndCast<IListTypeDecoder>(decoder));
      }

      public override Array ReadArrayElements(Stream stream, IStreamDecoderState state, int count)
      {
         IStreamTypeDecoder decoder = state.Decoder.ReadNextTypeDecoder(stream, state);

         CheckIsExpectedType<IListTypeDecoder>(decoder);

         SaslMechanisms[] result = new SaslMechanisms[count];
         for (int i = 0; i < count; ++i)
         {
            result[i] = ReadType(stream, state, CheckIsExpectedTypeAndCast<IListTypeDecoder>(decoder));
         }

         return result;
      }

      public override void SkipValue(Stream stream, IStreamDecoderState state)
      {
         IStreamTypeDecoder decoder = state.Decoder.ReadNextTypeDecoder(stream, state);

         CheckIsExpectedType<IListTypeDecoder>(decoder);

         decoder.SkipValue(stream, state);
      }

      private static SaslMechanisms ReadType(Stream stream, IStreamDecoderState state, IListTypeDecoder listDecoder)
      {
         SaslMechanisms result = new();

         _ = listDecoder.ReadSize(stream, state);
         int count = listDecoder.ReadCount(stream, state);

         if (count != RequiredListEntries)
         {
            throw new DecodeException("SASL Mechanisms must contain at least one entry: " + count);
         }
         else
         {
            result.Mechanisms = state.Decoder.ReadMultiple<Symbol>(stream, state);
         }

         return result;
      }
   }
}
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
using System.Text;
using System.Collections.Generic;

namespace Apache.Qpid.Proton.Test.Driver.Codec.Primitives
{
   public sealed class Symbol : IEquatable<Symbol>, IComparable, IComparable<Symbol>
   {
      private static readonly Symbol EMPTY_SYMBOL = new Symbol();

      // Lazy allocated based on calls to stringify the given Symbol
      private string symbolString;

      private readonly byte[] underlying;
      private readonly int hashCode;

      private Symbol()
      {
         underlying = new byte[0];
         symbolString = "";
         hashCode = 32;
      }

      private Symbol(byte[] buffer)
      {
         underlying = buffer;
         hashCode = buffer.GetHashCode();
      }

      /// <summary>
      /// Returns the number of ASCII characters that comprise this Symbol
      /// </summary>
      public int Length
      {
         get { return underlying.Length; }
      }

      /// <summary>
      /// Writes a copy of the Symbol bytes to the given buffer.
      /// </summary>
      /// <param name="buffer">The buffer to write the Symbol bytes to</param>
      public void WriteTo(Span<byte> buffer)
      {
         underlying.CopyTo(buffer);
      }

      public override string ToString()
      {
         if (symbolString == null && underlying.Length > 0)
         {
            Decoder decoder = Encoding.ASCII.GetDecoder();
            int charCount = decoder.GetCharCount(underlying, 0, underlying.Length);
            char[] output = new char[charCount];
            int outputChars = decoder.GetChars(underlying, 0, underlying.Length, output, 0);

            symbolString = new string(output, 0, outputChars);
         }

         return symbolString ?? "";
      }

      public override int GetHashCode()
      {
         return hashCode;
      }

      public override bool Equals(object symbol)
      {
         if (symbol == null || symbol.GetType() != GetType())
         {
            return false;
         }

         return Equals(symbol as Symbol);
      }

      public bool Equals(Symbol symbol)
      {
         if (symbol == null)
         {
            return false;
         }

         return underlying.Equals(symbol.underlying);
      }

      public int CompareTo(Symbol other)
      {
         return Comparer<byte[]>.Default.Compare(underlying, other.underlying);
      }

      public int CompareTo(object other)
      {
         return CompareTo(other as Symbol);
      }
   }
}
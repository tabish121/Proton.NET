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
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Apache.Qpid.Proton.Utilities;

namespace Apache.Qpid.Proton.Buffer
{
   /// <summary>
   /// A composite buffer contains zero, one or more proton buffer instances
   /// chained together to behave as if it were one single contiguous buffer
   /// which can be read or written to.
   /// </summary>
   public sealed class ProtonCompositeBuffer : IProtonCompositeBuffer
   {
      /// <summary>
      /// Limit capcity to a value that might still allow for a non-composite
      /// buffer copy of this buffer to be made if requested.
      /// </summary>
      private const long MAX_CAPACITY = long.MaxValue;

      private static readonly IProtonBuffer[] EMPTY_BUFFER_ARRAY = Array.Empty<IProtonBuffer>();

      private readonly SplitBufferAccessor splitBufferAccessor;
      private readonly IProtonBufferAllocator allocator;
      private readonly long maxCapacity;

      private IProtonBuffer[] buffers;
      private long[] indexTracker;

      private long readOffset;
      private long writeOffset;
      private long capacity;

      /// <summary>
      /// Before any read or write the composite must determine an index into the
      /// chosen buffer to be read or written to where that operation should start.
      /// </summary>
      private long nextComputedAccessIndex;

      internal ProtonCompositeBuffer(long maxCapacity = MAX_CAPACITY) : this(ProtonByteBufferAllocator.Instance, EMPTY_BUFFER_ARRAY, maxCapacity)
      {
      }

      internal ProtonCompositeBuffer(IProtonBufferAllocator allocator) : this(allocator, EMPTY_BUFFER_ARRAY, MAX_CAPACITY)
      {
      }

      internal ProtonCompositeBuffer(IProtonBufferAllocator allocator, IEnumerable<IProtonBuffer> buffers, long maxCapacity = MAX_CAPACITY)
      {
         this.maxCapacity = maxCapacity;
         this.allocator = allocator ??
            throw new ArgumentNullException(nameof(allocator), "Cannot create a composite buffer with a null allocator");
         this.buffers = FilterIncomingBuffers(buffers);
         this.splitBufferAccessor = new SplitBufferAccessor(this);

         ComputeOffsetsAndIndexes();
      }

      #region Basic Buffer information and state APIs

      public long Capacity => capacity;

      public bool IsReadable => readOffset < writeOffset;

      public long ReadableBytes => writeOffset - readOffset;

      public bool IsWritable => writeOffset < capacity;

      public long WritableBytes => capacity - writeOffset;

      public long ReadOffset
      {
         get => readOffset;
         set
         {
            CheckReadBounds(value, 0);
            long remaining = value;
            foreach (IProtonBuffer buffer in buffers)
            {
               buffer.ReadOffset = Math.Min(buffer.Capacity, remaining);
               remaining = Math.Max(0, remaining - buffer.Capacity);
            }
            this.readOffset = value;
         }
      }

      public long WriteOffset
      {
         get => writeOffset;
         set
         {
            CheckWriteBounds(value, 0);
            long remaining = value;
            foreach (IProtonBuffer buffer in buffers)
            {
               buffer.WriteOffset = Math.Min(buffer.Capacity, remaining);
               remaining = Math.Max(0, remaining - buffer.Capacity);
            }
            this.writeOffset = value;
         }
      }

      public uint ComponentCount
      {
         get
         {
            uint count = 0;
            foreach (IProtonBuffer buffer in buffers)
            {
               count += buffer.ComponentCount;
            }

            return count;
         }
      }

      public uint ReadableComponentCount
      {
         get
         {
            uint count = 0;
            foreach (IProtonBuffer buffer in buffers)
            {
               count += buffer.ReadableComponentCount;
            }

            return count;
         }
      }

      public uint WritableComponentCount
      {
         get
         {
            uint count = 0;
            foreach (IProtonBuffer buffer in buffers)
            {
               count += buffer.WritableComponentCount;
            }

            return count;
         }
      }

      #endregion

      public IProtonBuffer Fill(byte value)
      {
         foreach (IProtonBuffer buffer in buffers)
         {
            buffer.Fill(value);
         }

         return this;
      }

      public IProtonBuffer Reset()
      {
         ReadOffset = 0;
         WriteOffset = 0;

         return this;
      }

      public IProtonBuffer SkipBytes(long amount)
      {
         ReadOffset += amount;
         return this;
      }

      public IProtonCompositeBuffer Append(IProtonBuffer buffer)
      {
         Statics.RequireNonNull(buffer, "Appended buffer cannot be null");

         if (buffer.Capacity == 0)
         {
            return this; // Filter buffers that offer nothing for this composite.
         }

         long newCapacity = capacity + buffer.Capacity;
         CheckIsValidBufferSize(newCapacity);
         IProtonBuffer[] restorable = this.buffers; // Save in case of errors
         try
         {
            if (IProtonCompositeBuffer.IsComposite(buffer))
            {
               IProtonCompositeBuffer composite = buffer as IProtonCompositeBuffer;
               IEnumerable<IProtonBuffer> decomposed = composite.DecomposeBuffer();
               int decomposedLength = decomposed.Count();

               List<IProtonBuffer> newComposed = new(buffers.Length + decomposedLength);
               newComposed.AddRange(buffers);
               newComposed.AddRange(decomposed);

               // Will throw if duplicates detected otherwise returns.
               IEnumerable<IProtonBuffer> filtered = FilterIncomingBuffers(newComposed);

               int extendedAt = buffers.Length;
               buffers = Statics.CopyOf(buffers, extendedAt + decomposedLength);
               Array.ConstrainedCopy(decomposed.ToArray(), 0, buffers, extendedAt, decomposedLength);
               ComputeOffsetsAndIndexes();
            }
            else
            {
               foreach (IProtonBuffer candidate in buffers)
               {
                  if (ReferenceEquals(candidate, buffer))
                  {
                     throw new ArgumentException("Cannot add a duplicate buffer to a composite buffer");
                  }
               }
               AppendValidatedBuffer(buffer);
            }
         }
         catch (Exception)
         {
            // Put buffer into safe state following failed add.
            this.buffers = restorable;
            throw;
         }

         return this;
      }

      public IProtonBuffer Compact()
      {
         if (readOffset == 0)
         {
            return this;
         }

         int unused = 0;
         int position = 0;
         long oldReadOffset = readOffset;

         if (IsReadable)
         {
            ForEachReadableComponent(unused, (index, component) =>
            {
               for (int i = 0; i < component.ReadableArrayLength; ++i)
               {
                  SetUnsignedByte(position++, component.ReadableArray[component.ReadableArrayOffset + i]);
               }

               return true;
            });
         }

         ReadOffset = 0;
         WriteOffset = writeOffset - oldReadOffset;

         return this;
      }

      public IProtonBuffer Reclaim()
      {
         if (readOffset != 0)
         {
            int readIsInBuffer = (int)SearchIndexTracker(readOffset);
            if (readIsInBuffer > 0)
            {
               buffers = Statics.CopyOfRange(buffers, readIsInBuffer, buffers.Length);
            }
            else if (readOffset == Capacity)
            {
               buffers = EMPTY_BUFFER_ARRAY;
            }

            ComputeOffsetsAndIndexes();
         }

         return this;
      }

      public IProtonBuffer WriteSplit(long offset)
      {
         return Split(WriteOffset + offset);
      }

      public IProtonBuffer ReadSplit(long offset)
      {
         return Split(ReadOffset + offset);
      }

      public IProtonBuffer Split()
      {
         return Split(WriteOffset);
      }

      public IProtonBuffer Split(long offset)
      {
         if (offset > int.MaxValue)
         {
            throw new ArgumentOutOfRangeException(nameof(offset), "Proton byte buffer cannot exceed Int32.MaxValue bytes in capacity");
         }

         if (offset < 0 || offset > Capacity)
         {
            throw new ArgumentOutOfRangeException(nameof(offset), "The buffer split offset must be within the current buffer capacity");
         }

         long splitAtBuffer = SearchIndexTracker(offset);

         // This value will either be the start of the buffer or some offset into it
         // which means either we can easily split here or possible need to split this
         // buffer to compose the two halves we will create.
         long bufferSplitPoint = offset - indexTracker[splitAtBuffer];

         // Either we can copy everything up to the split at point or we need to copy
         // the split at buffer into both array and then split it to complete the operation.
         IProtonBuffer[] result = Statics.CopyOf(buffers, (int)(bufferSplitPoint == 0 ? splitAtBuffer : 1 + splitAtBuffer));
         buffers = Statics.CopyOfRange(buffers,
            (int)(bufferSplitPoint == buffers[splitAtBuffer].Capacity ? 1 + splitAtBuffer : splitAtBuffer), buffers.Length);

         if (bufferSplitPoint > 0 && result.Length > 0 && bufferSplitPoint < result[result.Length - 1].Capacity)
         {
            result[result.Length - 1] = buffers[0].Split(bufferSplitPoint);
         }

         ComputeOffsetsAndIndexes();

         return new ProtonCompositeBuffer(allocator, result);
      }

      public IEnumerable<IProtonBuffer> DecomposeBuffer()
      {
         IProtonBuffer[] result = buffers;

         // Clear state such that this buffer appears empty.
         this.buffers = EMPTY_BUFFER_ARRAY;
         this.readOffset = 0;
         this.writeOffset = 0;
         this.nextComputedAccessIndex = -1;

         return result;
      }

      public IProtonBuffer Copy()
      {
         return Copy(ReadOffset, ReadableBytes);
      }

      public IProtonBuffer Copy(long index, long length)
      {
         ProtonBufferSupport.CheckLength(length);
         CheckGetBounds(index, length);

         IProtonBuffer choice = (IProtonBuffer)ChooseBuffer(index, 0);
         IProtonBuffer[] copies;

         if (length > 0)
         {
            copies = new IProtonBuffer[buffers.Length];
            long offset = nextComputedAccessIndex;
            long remaining = length;
            long i;
            long j = 0;
            for (i = SearchIndexTracker(index); remaining > 0; i++)
            {
               IProtonBuffer source = buffers[i];
               long available = source.Capacity - offset;
               copies[j++] = source.Copy(offset, Math.Min(remaining, available));
               remaining -= available;
               offset = 0;
            }

            // Compact the copies array now to what we actually used
            IProtonBuffer[] result = new IProtonBuffer[j];
            Array.Copy(copies, result, j);
            copies = result;
         }
         else
         {
            // Specialize for length == 0, since we must copy from at least one constituent buffer.
            copies = new IProtonBuffer[] { choice.Copy(nextComputedAccessIndex, 0) };
         }

         return new ProtonCompositeBuffer(allocator, copies);
      }

      public IProtonBuffer CopyInto(long srcPos, byte[] dest, long destPos, long length)
      {
         if (length < 0)
         {
            throw new ArgumentOutOfRangeException("Length cannot be negative: " + length + '.');
         }
         if (srcPos < 0)
         {
            throw IndexOutOfBounds(srcPos, false);
         }
         if (srcPos + length > capacity)
         {
            throw IndexOutOfBounds(srcPos + length, false);
         }

         while (length > 0)
         {
            // Given a required length of zero choose buffer always gives a direct
            // access buffer form the array of buffers.
            IProtonBuffer buf = (IProtonBuffer)ChooseBuffer(srcPos, 0);

            // Delegate to each subregion for a copy into the array, moving the array
            // offset by the copy amount with each iteration.
            long toCopy = Math.Min(buf.Capacity - nextComputedAccessIndex, length);
            buf.CopyInto(nextComputedAccessIndex, dest, destPos, toCopy);
            srcPos += toCopy;
            destPos += toCopy;
            length -= toCopy;
         }

         return this;
      }

      public IProtonBuffer CopyInto(long srcPos, IProtonBuffer dest, long destPos, long length)
      {
         if (length < 0)
         {
            throw new ArgumentOutOfRangeException("Length cannot be negative: " + length + '.');
         }
         if (srcPos < 0)
         {
            throw IndexOutOfBounds(srcPos, false);
         }
         if (Statics.AddExact(srcPos, length) > capacity)
         {
            throw IndexOutOfBounds(srcPos + length, false);
         }

         // TODO There could be an issue here with the copy if the source and dest buffers overlap

         while (length > 0)
         {
            // Given a required length of zero choose buffer always gives a direct
            // access buffer form the array of buffers.
            IProtonBuffer buf = (IProtonBuffer)ChooseBuffer(srcPos, 0);

            // Delegate to each subregion for a copy into the array, moving the array
            // offset by the copy amount with each iteration.
            long toCopy = Math.Min(buf.Capacity - nextComputedAccessIndex, length);
            buf.CopyInto(nextComputedAccessIndex, dest, destPos, toCopy);
            srcPos += toCopy;
            destPos += toCopy;
            length -= toCopy;
         }

         return this;
      }

      public IProtonBuffer EnsureWritable(long amount)
      {
         return DoEnsureWritable(amount);
      }

      private IProtonBuffer DoEnsureWritable(long amount, bool allowCompaction = true, long minimumGrowth = 1)
      {
         if (amount < 0)
         {
            throw new ArgumentOutOfRangeException(nameof(amount), "Growth amount must be greater than zero");
         }

         if (WritableBytes >= amount)
         {
            return this;
         }

         if (minimumGrowth < 0)
         {
            throw new ArgumentOutOfRangeException(nameof(minimumGrowth), "The minimum growth cannot be negative: " + minimumGrowth + '.');
         }

         if (allowCompaction && amount <= readOffset)
         {
            int compactableBuffers = 0;
            foreach (IProtonBuffer candidate in buffers)
            {
               if (candidate.Capacity != candidate.ReadOffset)
               {
                  break;
               }
               compactableBuffers++;
            }

            if (compactableBuffers > 0)
            {
               IProtonBuffer[] compactable;
               if (compactableBuffers < buffers.Length)
               {
                  compactable = new IProtonBuffer[compactableBuffers];
                  Array.ConstrainedCopy(buffers, 0, compactable, 0, compactable.Length);
                  Array.ConstrainedCopy(buffers, compactable.Length, buffers, 0, buffers.Length - compactable.Length);
                  Array.ConstrainedCopy(compactable, 0, buffers, buffers.Length - compactable.Length, compactable.Length);
               }
               else
               {
                  compactable = buffers;
               }

               foreach (IProtonBuffer target in compactable)
               {
                  target.Reset();
               }

               ComputeOffsetsAndIndexes();
               if (WritableBytes >= amount)
               {
                  return this;
               }
            }
            else if (buffers.Length == 1)
            {
               // If we only have a single component buffer, then we can safely compact that in-place.
               buffers[0].Compact();
               ComputeOffsetsAndIndexes();
               if (WritableBytes >= amount)
               {
                  return this;
               }
            }
         }

         long growth = Math.Max(amount - WritableBytes, minimumGrowth);

         if (growth < 0)
         {
            throw new ArgumentOutOfRangeException("Buffer size must not be negative, but was " + growth + '.');
         }

         if ((ulong)(growth + Capacity) > (ulong)maxCapacity)
         {
            throw new ArgumentOutOfRangeException(
                    "Buffer size cannot be made greater than " + maxCapacity +
                    ", but was requested to grow to" + (ulong)(growth + Capacity) + '.');
         }

         IProtonBuffer extension = allocator.Allocate(growth);
         AppendValidatedBuffer(extension);
         return this;
      }

      public int CompareTo(object obj)
      {
         return CompareTo((IProtonBuffer)obj);
      }

      public int CompareTo(IProtonBuffer other)
      {
         return ProtonBufferSupport.Compare(this, other);
      }

      public bool Equals(IProtonBuffer other)
      {
         return ProtonBufferSupport.Equals(this, other);
      }

      public override int GetHashCode()
      {
         return ProtonBufferSupport.GetHashCode(this);
      }

      public int ForEachReadableComponent(in int index, in Func<int, IReadableComponent, bool> processor)
      {
         CheckReadBounds(ReadOffset, Math.Max(1, ReadableBytes));
         int visited = 0;
         foreach (IProtonBuffer buffer in buffers)
         {
            if (buffer.ReadableBytes > 0)
            {
               int count = buffer.ForEachReadableComponent(visited + index, processor);
               if (count > 0)
               {
                  visited += count;
               }
               else
               {
                  visited = -visited + count;
                  break;
               }
            }
         }
         return visited;
      }

      public int ForEachWritableComponent(in int index, in Func<int, IWritableComponent, bool> processor)
      {
         CheckWriteBounds(WriteOffset, Math.Max(1, WritableBytes));
         int visited = 0;
         foreach (IProtonBuffer buffer in buffers)
         {
            if (buffer.WritableBytes > 0)
            {
               int count = buffer.ForEachWritableComponent(visited + index, processor);
               if (count > 0)
               {
                  visited += count;
               }
               else
               {
                  visited = -visited + count;
                  break;
               }
            }
         }

         return visited;
      }

      public bool GetBoolean(long index)
      {
         return PrepareForGet(index, sizeof(byte)).GetBoolean(nextComputedAccessIndex);
      }

      public sbyte GetByte(long index)
      {
         return PrepareForGet(index, sizeof(byte)).GetByte(nextComputedAccessIndex);
      }

      public char GetChar(long index)
      {
         return PrepareForGet(index, sizeof(char)).GetChar(nextComputedAccessIndex);
      }

      public double GetDouble(long index)
      {
         return PrepareForGet(index, sizeof(double)).GetDouble(nextComputedAccessIndex);
      }

      public float GetFloat(long index)
      {
         return PrepareForGet(index, sizeof(float)).GetFloat(nextComputedAccessIndex);
      }

      public short GetShort(long index)
      {
         return PrepareForGet(index, sizeof(short)).GetShort(nextComputedAccessIndex);
      }

      public int GetInt(long index)
      {
         return PrepareForGet(index, sizeof(int)).GetInt(nextComputedAccessIndex);
      }

      public long GetLong(long index)
      {
         return PrepareForGet(index, sizeof(long)).GetLong(nextComputedAccessIndex);
      }

      public byte GetUnsignedByte(long index)
      {
         return PrepareForGet(index, sizeof(byte)).GetUnsignedByte(nextComputedAccessIndex);
      }

      public ushort GetUnsignedShort(long index)
      {
         return PrepareForGet(index, sizeof(ushort)).GetUnsignedShort(nextComputedAccessIndex);
      }

      public uint GetUnsignedInt(long index)
      {
         return PrepareForGet(index, sizeof(int)).GetUnsignedInt(nextComputedAccessIndex);
      }

      public ulong GetUnsignedLong(long index)
      {
         return PrepareForGet(index, sizeof(ulong)).GetUnsignedLong(nextComputedAccessIndex);
      }

      public bool ReadBoolean()
      {
         return PrepareForRead(sizeof(byte)).ReadBoolean();
      }

      public sbyte ReadByte()
      {
         return PrepareForRead(sizeof(byte)).ReadByte();
      }

      public char ReadChar()
      {
         return PrepareForRead(sizeof(char)).ReadChar();
      }

      public double ReadDouble()
      {
         return PrepareForRead(sizeof(double)).ReadDouble();
      }

      public float ReadFloat()
      {
         return PrepareForRead(sizeof(float)).ReadFloat();
      }

      public short ReadShort()
      {
         return PrepareForRead(sizeof(short)).ReadShort();
      }

      public int ReadInt()
      {
         return PrepareForRead(sizeof(int)).ReadInt();
      }

      public long ReadLong()
      {
         return PrepareForRead(sizeof(long)).ReadLong();
      }

      public byte ReadUnsignedByte()
      {
         return PrepareForRead(sizeof(byte)).ReadUnsignedByte();
      }

      public ushort ReadUnsignedShort()
      {
         return PrepareForRead(sizeof(ushort)).ReadUnsignedShort();
      }

      public uint ReadUnsignedInt()
      {
         return PrepareForRead(sizeof(uint)).ReadUnsignedInt();
      }

      public ulong ReadUnsignedLong()
      {
         return PrepareForRead(sizeof(ulong)).ReadUnsignedLong();
      }

      public IProtonBuffer SetBoolean(long index, bool value)
      {
         PrepareForWrite(index, sizeof(byte)).SetBoolean(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer SetByte(long index, sbyte value)
      {
         PrepareForWrite(index, sizeof(byte)).SetByte(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer SetChar(long index, char value)
      {
         PrepareForWrite(index, sizeof(char)).SetChar(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer SetDouble(long index, double value)
      {
         PrepareForWrite(index, sizeof(double)).SetDouble(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer SetFloat(long index, float value)
      {
         PrepareForWrite(index, sizeof(float)).SetFloat(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer SetShort(long index, short value)
      {
         PrepareForWrite(index, sizeof(short)).SetShort(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer SetInt(long index, int value)
      {
         PrepareForWrite(index, sizeof(int)).SetInt(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer SetLong(long index, long value)
      {
         PrepareForWrite(index, sizeof(long)).SetLong(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer SetUnsignedByte(long index, byte value)
      {
         PrepareForWrite(index, sizeof(byte)).SetUnsignedByte(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer SetUnsignedShort(long index, ushort value)
      {
         PrepareForWrite(index, sizeof(short)).SetUnsignedShort(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer SetUnsignedInt(long index, uint value)
      {
         PrepareForWrite(index, sizeof(uint)).SetUnsignedInt(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer SetUnsignedLong(long index, ulong value)
      {
         PrepareForWrite(index, sizeof(ulong)).SetUnsignedLong(nextComputedAccessIndex, value);
         return this;
      }

      public IProtonBuffer WriteBoolean(bool value)
      {
         PrepareForWrite(sizeof(byte)).WriteBoolean(value);
         return this;
      }

      public IProtonBuffer WriteByte(sbyte value)
      {
         PrepareForWrite(sizeof(byte)).WriteByte(value);
         return this;
      }

      public IProtonBuffer WriteDouble(double value)
      {
         PrepareForWrite(sizeof(double)).WriteDouble(value);
         return this;
      }

      public IProtonBuffer WriteFloat(float value)
      {
         PrepareForWrite(sizeof(float)).WriteFloat(value);
         return this;
      }

      public IProtonBuffer WriteShort(short value)
      {
         PrepareForWrite(sizeof(short)).WriteShort(value);
         return this;
      }

      public IProtonBuffer WriteInt(int value)
      {
         PrepareForWrite(sizeof(int)).WriteInt(value);
         return this;
      }

      public IProtonBuffer WriteLong(long value)
      {
         PrepareForWrite(sizeof(long)).WriteLong(value);
         return this;
      }

      public IProtonBuffer WriteUnsignedByte(byte value)
      {
         PrepareForWrite(sizeof(byte)).WriteUnsignedByte(value);
         return this;
      }

      public IProtonBuffer WriteUnsignedShort(ushort value)
      {
         PrepareForWrite(sizeof(ushort)).WriteUnsignedShort(value);
         return this;
      }

      public IProtonBuffer WriteUnsignedInt(uint value)
      {
         PrepareForWrite(sizeof(uint)).WriteUnsignedInt(value);
         return this;
      }

      public IProtonBuffer WriteUnsignedLong(ulong value)
      {
         PrepareForWrite(sizeof(ulong)).WriteUnsignedLong(value);
         return this;
      }

      public IProtonBuffer WriteBytes(byte[] source)
      {
         if (source == null)
         {
            throw new ArgumentNullException(nameof(source), "Input source array cannot be null");
         }

         return WriteBytes(source, 0, source.Length);
      }

      public IProtonBuffer WriteBytes(byte[] source, long offset, long length)
      {
         if (source == null)
         {
            throw new ArgumentNullException(nameof(source), "Cannot write a null array into this buffer");
         }

         CheckWriteIntoArgs(source.LongLength, offset, length, writeOffset, Capacity);
         // Inefficient but workable solution that should be optimized later
         long woff = WriteOffset;
         WriteOffset = woff + length;
         for (long i = 0; i < length; i++)
         {
            SetUnsignedByte(woff + i, source[offset + i]);
         }

         return this;
      }

      public IProtonBuffer WriteBytes(IProtonBuffer source)
      {
         long size = source.ReadableBytes;
         long woff = WriteOffset;
         source.CopyInto(source.ReadOffset, this, woff, size);
         source.ReadOffset += size;
         WriteOffset = woff + size;

         return this;
      }

      public string ToString(Encoding encoding)
      {
         // Inefficient but workable solution which should be optimized in the future.
         byte[] data = new byte[ReadableBytes];
         CopyInto(ReadOffset, data, 0, ReadableBytes);

         Decoder decoder = encoding.GetDecoder();
         int charCount = decoder.GetCharCount(data, 0, data.Length);
         char[] output = new char[charCount];

         int outputChars = decoder.GetChars(data, 0, data.Length, output, 0);

         return new string(output, 0, outputChars);
      }

      public override string ToString()
      {
         return "Buffer[readOffset:" + readOffset + ", writeOffset:" + writeOffset + ", cap:" + capacity + ']';
      }

      #region Internal composite buffer APIs

      private static IProtonBuffer[] FilterIncomingBuffers(IEnumerable<IProtonBuffer> buffers)
      {
         // First we need to filter all empty buffers from the input as they don't add anything to
         // the body of a composite buffer and then we need to flatten any composite buffers into
         // our internal view to avoid composites of composite which could allow an overflow on
         // the compsoed size which we should be proactively trying to prevent.
         IProtonBuffer[] composite = buffers.Where(IsNotAnEmptyBuffer)
                                            .SelectMany(FlattenedCompositeBuffers)
                                            .ToArray();

         IProtonBuffer[] distinct = composite.Distinct(IdentityComparator.Instance).ToArray();

         if (composite.Length != distinct.Length)
         {
            throw new ArgumentException("Cannot construct a composite buffer from equal buffer instances");
         }

         return composite;
      }

      private static bool IsNotAnEmptyBuffer(IProtonBuffer buffer) => buffer.Capacity > 0;

      private static IEnumerable<IProtonBuffer> FlattenedCompositeBuffers(IProtonBuffer buffer)
      {
         if (IProtonCompositeBuffer.IsComposite(buffer))
         {
            return ((IProtonCompositeBuffer)buffer).DecomposeBuffer();
         }
         else
         {
            return new IProtonBuffer[] { buffer };
         }
      }

      private void ComputeOffsetsAndIndexes()
      {
         long writeOffset = 0;
         long readOffset = 0;

         // Compute read offset and write offsets into the provided buffers and check
         // invariants such as no read or write gaps etc.
         if (buffers.Length > 0)
         {
            bool unwrittenBufferEnforced = false;
            bool unreadBufferEnforced = false;

            foreach (IProtonBuffer buffer in buffers)
            {
               if (!unwrittenBufferEnforced)
               {
                  writeOffset += buffer.WriteOffset;
                  if (buffer.WritableBytes > 0)
                  {
                     unwrittenBufferEnforced = true;
                  }
               }
               else if (buffer.WriteOffset != 0)
               {
                  throw new ArgumentOutOfRangeException(
                     "Cannot compose the given buffers due to gap in writeable portion of span");
               }
            }
            foreach (IProtonBuffer buffer in buffers)
            {
               if (!unreadBufferEnforced)
               {
                  readOffset += buffer.ReadOffset;
                  if (buffer.ReadableBytes > 0 || buffer.WritableBytes > 0)
                  {
                     unreadBufferEnforced = true;
                  }
               }
               else if (buffer.ReadOffset != 0)
               {
                  throw new ArgumentOutOfRangeException(
                     "Cannot compose the given buffers due to gap in readable portion of span");
               }
            }

            if (readOffset > writeOffset)
            {
               throw new ArgumentOutOfRangeException(
                  "Composed buffers resulted in read offset ahead of write offset");
            }
         }

         this.readOffset = readOffset;
         this.writeOffset = writeOffset;
         this.indexTracker = new long[buffers.Length];

         long capacity = 0;
         for (int i = 0; i < buffers.Length; ++i)
         {
            indexTracker[i] = capacity;
            capacity += buffers[i].Capacity;
         }

         if (capacity > MAX_CAPACITY)
         {
            throw new ArgumentOutOfRangeException(string.Format(
               "Cannot create a buffer with combined capacity greater than {0} " +
               "due to array copy constraints. Input buffers capacity was : {1}", MAX_CAPACITY, capacity));
         }

         this.capacity = capacity;
      }

      private long SearchIndexTracker(long index)
      {
         long i = Array.BinarySearch(indexTracker, index);
         return i < 0 ? -(i + 2) : i;
      }

      private IProtonBufferAccessors ChooseBuffer(long index, long size)
      {
         long i = SearchIndexTracker(index);

         // When the read and write offsets are at the end of the buffer they
         // will be equal to the number of tracked buffers and we return null
         // as no read or write operation can occur in this state.
         if (i == buffers.Length)
         {
            return null;
         }

         long off = index - indexTracker[i];
         IProtonBuffer candidate = buffers[i];

         // Space available in the selected buffer to accommodate the
         // requested read or write so we can return it directly but
         // in the case where the operation will run past the buffer
         // we use our internal accessor which will splice the operation
         // across two or more buffers.
         if (off + size <= candidate.Capacity)
         {
            nextComputedAccessIndex = (int)off;
            return candidate;
         }
         else
         {
            nextComputedAccessIndex = index;
            return splitBufferAccessor;
         }
      }

      private IProtonBufferAccessors ChooseBufferForDirectOperation(long index)
      {
         long bufferIndex = SearchIndexTracker(index);
         return buffers[bufferIndex];
      }

      private void CheckReadBounds(long index, long size)
      {
         if (index < 0 || writeOffset < index + size)
         {
            throw IndexOutOfBounds(index, false);
         }
      }

      private void CheckGetBounds(long index, long size)
      {
         if (index < 0 || capacity < index + size)
         {
            throw IndexOutOfBounds(index, false);
         }
      }

      private void CheckWriteBounds(long index, long size)
      {
         if (index < 0 || capacity < index + size)
         {
            throw IndexOutOfBounds(index, true);
         }
      }

      private static void CheckWriteIntoArgs(long srcCapacity, long srcPos, long length, long destPos, long destLength)
      {
         if (srcPos < 0)
         {
            throw new ArgumentOutOfRangeException("The srcPos cannot be negative: " + srcPos + '.');
         }
         if (length < 0)
         {
            throw new ArgumentOutOfRangeException("The length cannot be negative: " + length + '.');
         }
         if (srcCapacity < srcPos + length)
         {
            throw new ArgumentOutOfRangeException("The srcPos + length is beyond the end of the source buffer: " +
                                                  "srcPos = " + srcPos + ", length = " + length + '.');
         }
         if (destPos < 0)
         {
            throw new ArgumentOutOfRangeException("The destPos cannot be negative: " + destPos + '.');
         }
         if (destLength < destPos + length)
         {
            throw new ArgumentOutOfRangeException("The destPos + length is beyond the end of the destination: " +
                                                  "destPos = " + destPos + ", length = " + length + '.');
         }
      }

      private Exception IndexOutOfBounds(long index, bool write)
      {
         return new IndexOutOfRangeException(
            "Index " + index + " is out of bounds: [read 0 to " + writeOffset + ", write 0 to " + capacity + "].");
      }

      private IProtonBufferAccessors PrepareForRead(int size)
      {
         IProtonBufferAccessors buf = PrepareForRead(readOffset, size);
         readOffset += size;
         return buf;
      }

      private IProtonBufferAccessors PrepareForRead(long index, long size)
      {
         // This method either returns the correct buffer for the given index
         // which has enough remaining readable bytes to fulfill the request
         // or it returns the split buffer accessor and configures the object
         // state such that it can produce a read of the requested bytes across
         // more than one contained buffer.
         CheckReadBounds(index, size);
         return ChooseBuffer(index, size);
      }

      private IProtonBufferAccessors PrepareForWrite(long size)
      {
         var buf = PrepareForWrite(writeOffset, size);
         writeOffset += size;
         return buf;
      }

      private IProtonBufferAccessors PrepareForWrite(long index, long size)
      {
         // This method either returns the correct buffer for the given index
         // which has enough remaining writable bytes to fulfill the request
         // or it returns the split buffer accessor and configures the object
         // state such that it can produce a write of the requested bytes across
         // more than one contained buffer.
         CheckWriteBounds(index, size);
         return ChooseBuffer(index, size);
      }

      private IProtonBufferAccessors PrepareForGet(long index, int size)
      {
         CheckGetBounds(index, size);
         return ChooseBuffer(index, size);
      }

      private static void CheckIsValidBufferSize(long size)
      {
         if (size < 0)
         {
            throw new ArgumentOutOfRangeException("Buffer capacity must not be negative, but was " + size + '.');
         }
         // We use max array size because on-heap buffers will be backed by byte-arrays.
         if (size > MAX_CAPACITY)
         {
            throw new ArgumentOutOfRangeException(
               "Buffer capacity cannot be greater than " + MAX_CAPACITY + ", but was " + size + '.');
         }
      }

      private void AppendValidatedBuffer(IProtonBuffer extension)
      {
         buffers = Statics.CopyOf(buffers, buffers.Length + 1);
         buffers[buffers.Length - 1] = extension;
         ComputeOffsetsAndIndexes();
      }

      #endregion

      #region Direct access API used by the split buffer accessor

      private byte DirectRead()
      {
         return ChooseBufferForDirectOperation(nextComputedAccessIndex++).ReadUnsignedByte();
      }

      private void DirectWrite(byte value)
      {
         ChooseBufferForDirectOperation(nextComputedAccessIndex++).WriteUnsignedByte(value);
      }

      private void DirectSet(long index, byte value)
      {
         ChooseBuffer(index, 1).SetUnsignedByte(nextComputedAccessIndex, value);
      }

      private byte DirectGet(long index)
      {
         return ChooseBuffer(index, 1).GetUnsignedByte(nextComputedAccessIndex);
      }

      #endregion

      #region Internal buffer accessor for use when read / write occurs accross two or more buffers

      /// <summary>
      /// When a read or write will cross the boundary of two or more buffers, the split
      /// buffer accessor perform single byte operations to span that gap.
      /// </summary>
      private class SplitBufferAccessor : IProtonBufferAccessors
      {
         private readonly ProtonCompositeBuffer buffer;

         public SplitBufferAccessor(ProtonCompositeBuffer buffer)
         {
            this.buffer = buffer;
         }

         #region Single byte methods which should never be used

         public bool GetBoolean(long index)
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         public sbyte GetByte(long index)
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         public byte GetUnsignedByte(long index)
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         public bool ReadBoolean()
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         public sbyte ReadByte()
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         public byte ReadUnsignedByte()
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         public IProtonBuffer SetBoolean(long index, bool value)
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         public IProtonBuffer SetByte(long index, sbyte value)
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         public IProtonBuffer SetUnsignedByte(long index, byte value)
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         public IProtonBuffer WriteBoolean(bool value)
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         public IProtonBuffer WriteByte(sbyte value)
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         public IProtonBuffer WriteUnsignedByte(byte value)
         {
            throw new NotImplementedException("The split buffer accessor does not supply byte level access");
         }

         #endregion

         public char GetChar(long index)
         {
            return (char)GetUnsignedShort(index);
         }

         public short GetShort(long index)
         {
            return (short)GetUnsignedShort(index);
         }

         public int GetInt(long index)
         {
            return (int)GetUnsignedInt(index);
         }

         public long GetLong(long index)
         {
            return (long)GetUnsignedLong(index);
         }

         public double GetDouble(long index)
         {
            return BitConverter.Int64BitsToDouble(GetLong(index));
         }

         public float GetFloat(long index)
         {
            return BitConverter.Int32BitsToSingle(GetInt(index));
         }

         public ushort GetUnsignedShort(long index)
         {
            return (ushort)(Get(index) << 8 |
                            Get(index + 1));
         }

         public uint GetUnsignedInt(long index)
         {
            return (uint)(Get(index) << 24 |
                          Get(index + 1) << 16 |
                          Get(index + 2) << 8 |
                          Get(index + 3));
         }

         public ulong GetUnsignedLong(long index)
         {
            return (ulong)Get(index) << 56 |
                   (ulong)Get(index + 1) << 48 |
                   (ulong)Get(index + 2) << 40 |
                   (ulong)Get(index + 3) << 32 |
                   (ulong)Get(index + 4) << 24 |
                   (ulong)Get(index + 5) << 16 |
                   (ulong)Get(index + 6) << 8 |
                   Get(index + 7);
         }

         public char ReadChar()
         {
            return (char)ReadUnsignedShort();
         }

         public double ReadDouble()
         {
            return BitConverter.Int64BitsToDouble(ReadLong());
         }

         public float ReadFloat()
         {
            return BitConverter.Int32BitsToSingle(ReadInt());
         }

         public short ReadShort()
         {
            return (short)ReadUnsignedShort();
         }

         public int ReadInt()
         {
            return (int)ReadUnsignedInt();
         }

         public long ReadLong()
         {
            return (long)ReadUnsignedLong();
         }

         public ushort ReadUnsignedShort()
         {
            return (ushort)(Read() << 8 | Read());
         }

         public uint ReadUnsignedInt()
         {
            return (uint)(Read() << 24 |
                          Read() << 16 |
                          Read() << 8 |
                          Read());
         }

         public ulong ReadUnsignedLong()
         {
            return (ulong)Read() << 56 |
                   (ulong)Read() << 48 |
                   (ulong)Read() << 40 |
                   (ulong)Read() << 32 |
                   (ulong)Read() << 24 |
                   (ulong)Read() << 16 |
                   (ulong)Read() << 8 |
                   Read();
         }

         public IProtonBuffer SetChar(long index, char value)
         {
            return SetUnsignedShort(index, value);
         }

         public IProtonBuffer SetDouble(long index, double value)
         {
            return SetUnsignedLong(index, (ulong)BitConverter.DoubleToInt64Bits(value));
         }

         public IProtonBuffer SetFloat(long index, float value)
         {
            return SetUnsignedInt(index, (uint)BitConverter.SingleToInt32Bits(value));
         }

         public IProtonBuffer SetShort(long index, short value)
         {
            return SetUnsignedShort(index, (ushort)value);
         }

         public IProtonBuffer SetInt(long index, int value)
         {
            return SetUnsignedInt(index, (uint)value);
         }

         public IProtonBuffer SetLong(long index, long value)
         {
            return SetUnsignedLong(index, (ulong)value);
         }

         public IProtonBuffer SetUnsignedShort(long index, ushort value)
         {
            Set(index, (byte)(value >> 8));
            Set(index + 1, (byte)(value >> 0));

            return buffer;
         }

         public IProtonBuffer SetUnsignedInt(long index, uint value)
         {
            Set(index, (byte)(value >> 24));
            Set(index + 1, (byte)(value >> 16));
            Set(index + 2, (byte)(value >> 8));
            Set(index + 3, (byte)(value >> 0));

            return buffer;
         }

         public IProtonBuffer SetUnsignedLong(long index, ulong value)
         {
            Set(index, (byte)(value >> 56));
            Set(index + 1, (byte)(value >> 48));
            Set(index + 2, (byte)(value >> 40));
            Set(index + 3, (byte)(value >> 32));
            Set(index + 4, (byte)(value >> 24));
            Set(index + 5, (byte)(value >> 16));
            Set(index + 6, (byte)(value >> 8));
            Set(index + 7, (byte)(value >> 0));

            return buffer;
         }

         public IProtonBuffer WriteDouble(double value)
         {
            return WriteUnsignedLong((ulong)BitConverter.DoubleToInt64Bits(value));
         }

         public IProtonBuffer WriteFloat(float value)
         {
            return WriteUnsignedInt((uint)BitConverter.SingleToInt32Bits(value));
         }

         public IProtonBuffer WriteShort(short value)
         {
            return WriteUnsignedShort((ushort)value);
         }

         public IProtonBuffer WriteInt(int value)
         {
            return WriteUnsignedInt((uint)value);
         }

         public IProtonBuffer WriteLong(long value)
         {
            return WriteUnsignedLong((ulong)value);
         }

         public IProtonBuffer WriteUnsignedShort(ushort value)
         {
            Write((byte)(value >> 8));
            Write((byte)(value >> 0));

            return buffer;
         }

         public IProtonBuffer WriteUnsignedInt(uint value)
         {
            Write((byte)(value >> 24));
            Write((byte)(value >> 16));
            Write((byte)(value >> 8));
            Write((byte)(value >> 0));

            return buffer;
         }

         public IProtonBuffer WriteUnsignedLong(ulong value)
         {
            Write((byte)(value >> 56));
            Write((byte)(value >> 48));
            Write((byte)(value >> 40));
            Write((byte)(value >> 32));
            Write((byte)(value >> 24));
            Write((byte)(value >> 16));
            Write((byte)(value >> 8));
            Write((byte)(value >> 0));

            return buffer;
         }

         private void Write(byte value)
         {
            buffer.DirectWrite(value);
         }

         private byte Read()
         {
            return buffer.DirectRead();
         }

         private void Set(long index, byte value)
         {
            buffer.DirectSet(index, value);
         }

         private byte Get(long index)
         {
            return buffer.DirectGet(index);
         }
      }

      #endregion

      #region Simple Equality Comparator that only uses reference equality for buffers

      private sealed class IdentityComparator : IEqualityComparer<IProtonBuffer>
      {
         public static readonly IdentityComparator Instance = new();

         public bool Equals(IProtonBuffer x, IProtonBuffer y)
         {
            return ReferenceEquals(x, y);
         }

         public int GetHashCode([DisallowNull] IProtonBuffer obj)
         {
            return obj.GetHashCode();
         }
      }

      #endregion
   }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Penguin.Analysis
{
    public struct LongByte : IEnumerable<int>
    {
        public long Value;

        private int count;

        public int Count
        {
            get
            {
                if (count < 0)
                {
                    count = CountBits(true);
                }

                return count;
            }
            private set
            {
                count = value;
            }
        }

        public LongByte(IEnumerable<int> indexes)
        {
            count = -1;
            Value = 0;
            foreach (int index in indexes)
            {
                Count++;
                this.AddBitAt(index);
            }
        }

        public LongByte(IEnumerable<sbyte> indexes)
        {
            count = -1;
            Value = 0;
            foreach (int index in indexes)
            {
                Count++;
                this.AddBitAt(index);
            }
        }

        public LongByte(long v)
        {
            count = -1;
            Value = v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AddBitAt(long value, int Index)
        {
            return value |= (long)1 << Index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountBits(long value, bool state)
        {
            int count = 0;

            if (state)
            {
                while (value > 0)
                {
                    count += (int)(value & 1);
                    value >>= 1;
                }
            }
            else
            {
                count = 64;

                while (value > 0)
                {
                    count -= (int)(value & 1);
                    value >>= 1;
                }
            }

            return count;
        }


        public static IEnumerable<int> GetSetBits(long value, bool state)
        {
            long b = value;
            int i = 0;

            if (state)
            {
                while (b > 0)
                {
                    if ((b & 1) != 0)
                    {
                        yield return i;
                    }

                    b >>= 1;
                    i++;
                }
            }
            else
            {
                while (b > 0)
                {
                    if ((b & 1) == 0)
                    {
                        yield return i;
                    }

                    b >>= 1;
                    i++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasBitAt(long value, int Index) => (value & ((long)1 << Index)) != 1;

        public static implicit operator long(LongByte d) => d.Value;

        public static implicit operator LongByte(long b) => new LongByte(b);

        public static bool operator !=(LongByte lhs, LongByte rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator ==(LongByte lhs, LongByte rhs)
        {
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long RemoveBitAt(long value, int Index)
        {
            return value & ~((long)1 << Index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SetBit(long value, int Index, bool state)
        {
            if (state)
            {
                return AddBitAt(value, Index);
            }
            else
            {
                return RemoveBitAt(value, Index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddBitAt(int Index)
        {
            long v = AddBitAt(Value, Index);

            if (v != Value)
            {
                Count++;
                Value = v;
            }
        }

        public LongByte And(LongByte other)
        {
            return Value & other;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CountBits(bool state) => CountBits(Value, state);

        public override bool Equals(object obj)
        {
            return this.Equals((LongByte)obj);
        }

        public bool Equals(LongByte p)
        {
            // Return true if the fields match.
            // Note that the base class is not invoked because it is
            // System.Object, which defines Equals as reference equality.
            return Value == p.Value;
        }

        public IEnumerator<int> GetEnumerator() => GetSetBits(Value, true).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public override int GetHashCode()
        {
            return unchecked((int)Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasBitAt(int Index) => HasBitAt(Value, Index);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveBitAt(int Index)
        {
            bool v = HasBitAt(Index);
            Value &= ~((long)1 << Index);

            if (v)
            {
                Count--;
            }

            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBit(int Index, bool state)
        {
            long v = SetBit(Value, Index, state);

            if (v != Value)
            {
                Count += state ? 1 : -1;
                Value = v;
            }
        }

        public override string ToString() => $"{Convert.ToString(Value, 2).PadLeft(64, '0')} ({Value.ToString()})";

        public bool TrimLeft(int bits = 1)
        {
            long nKey;
            long mask = -1;
            do
            {
                mask >>= 1;
                nKey = Value & mask;
            } while (nKey == Value && mask > 1);

            if (mask == 0)
            {
                return false;
            }

            Value = nKey;

            Count -= bits;

            return true;
        }

        public bool TrimRight(int bits = 1)
        {
            long nKey;
            long mask = -1;
            do
            {
                mask <<= 1;
                nKey = Value & mask;
            } while (nKey == Value && (nKey & mask) > 1);

            if (mask == 0)
            {
                return false;
            }

            Value = nKey;

            Count -= bits;

            return true;
        }
    }
}
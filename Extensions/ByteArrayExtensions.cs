using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Penguin.Analysis.Extensions
{
    internal static class ByteArrayExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetInt(this byte[] source, int offset)
        {
            int result = 0;
            for (int i = 0; i < 4; i++)
            {
                result <<= 8;
                result |= (source[3 - i + offset] & 0xFF);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetInt(this byte[] source, long offset)
        {
            int result = 0;
            for (int i = 0; i < 4; i++)
            {
                result <<= 8;
                result |= (source[3 - i + offset] & 0xFF);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<int> GetInts(this byte[] source, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return source.GetInt(offset + (i * 4));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetLong(this byte[] source, int offset)
        {
            long result = 0;
            for (int i = 0; i < 8; i++)
            {
                result <<= 8;
                result |= (source[(7 - i) + offset] & 0xFF);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetLong(this byte[] source, long offset)
        {
            long result = 0;
            for (int i = 0; i < 8; i++)
            {
                result <<= 8;
                result |= (source[(7 - i) + offset] & 0xFF);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<long> GetLongs(this byte[] source, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return source.GetLong(offset + (i * 8));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort GetShort(this byte[] source, int offset)
        {
            ushort result = 0;
            for (int i = 0; i < 2; i++)
            {
                result <<= 8;
                result |= (ushort)(source[1 - i + offset] & 0xFF);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort GetShort(this byte[] source, long offset)
        {
            ushort result = 0;
            for (int i = 0; i < 2; i++)
            {
                result <<= 8;
                result |= (ushort)(source[1 - i + offset] & 0xFF);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<ushort> GetShorts(this byte[] source, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return source.GetShort(offset + (i * 2));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<ushort> GetShorts(this byte[] source, long offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return source.GetShort(offset + (i * 2));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Segment(this byte[] source, int offset, int length)
        {
            byte[] chunk = new byte[length];

            for (int i = 0; i < length; i++)
            {
                chunk[i] = source[i + offset];
            }

            return chunk;
        }
    }
}
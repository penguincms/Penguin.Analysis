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

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static long GetLong(this byte[] source, int offset)
        //{
        //    long result = 0;
        //    for (int i = 0; i < 8; i++)
        //    {
        //        result <<= 8;
        //        result |= (source[(7 - i) + offset] & 0xFF);
        //    }
        //    return result;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static long GetLong(this byte[] source, long offset)
        //{
        //    long r2 = (source[7 + offset] << 56) | (source[6 + offset] << 48) |
        //              (source[5 + offset] << 40) | (source[4 + offset] << 32) |
        //              (source[3 + offset] << 24) | (source[2 + offset] << 16) |
        //              (source[1 + offset] << 8) | (source[offset]);

        //    long result = 0;
        //    for (int i = 0; i < 8; i++)
        //    {
        //        result <<= 8;
        //        result |= (source[(7 - i) + offset] & 0xFF);
        //    }
        //    return result;
        //}

        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe long GetLong(this byte[] value, long startIndex = 0)
        {
            fixed (byte* pbyte = &value[startIndex])
            {
                if (startIndex % 8 == 0)
                { // data is aligned
                    return *((long*)pbyte);
                }
                else
                {
                    int i1 = (*pbyte) | (*(pbyte + 1) << 8) | (*(pbyte + 2) << 16) | (*(pbyte + 3) << 24);
                    int i2 = (*(pbyte + 4)) | (*(pbyte + 5) << 8) | (*(pbyte + 6) << 16) | (*(pbyte + 7) << 24);
                    return (uint)i1 | ((long)i2 << 32);
                }
            }
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe long GetInt40(this byte[] value, long startIndex = 0)
        {
            fixed (byte* pbyte = &value[startIndex])
            {

                int i1 = (*pbyte) | (*(pbyte + 1) << 8) | (*(pbyte + 2) << 16) | (*(pbyte + 3) << 24);
                int i2 = (*(pbyte + 4)) | (*(pbyte + 5) << 8);
                return (uint)i1 | ((long)i2 << 32);

            }
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
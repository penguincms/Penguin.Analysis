using System;

namespace Penguin.Analysis
{
    public struct ByteCache : IEquatable<ByteCache>
    {
        private static readonly ushort Boot = (ushort)(((DateTime.Now.Hour * 60) + DateTime.Now.Minute) / 2);

        public ByteCache(byte[] data)
        {
            Data = data;
            LastUse = unchecked((ushort)((((DateTime.Now.Hour * 60) + DateTime.Now.Minute) / 2) - Boot));
        }

        public void SetLast()
        {
            LastUse = unchecked((ushort)((((DateTime.Now.Hour * 60) + DateTime.Now.Minute) / 2) - Boot));
        }

        public byte[] Data { get; set; }
        public ushort LastUse { get; set; }

        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public static bool operator ==(ByteCache left, ByteCache right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ByteCache left, ByteCache right)
        {
            return !(left == right);
        }

        public bool Equals(ByteCache other)
        {
            throw new NotImplementedException();
        }
    }
}
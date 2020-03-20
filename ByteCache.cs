using System;
using System.Collections.Generic;
using System.Text;

namespace Penguin.Analysis
{
    public struct ByteCache
    {
        static ushort Boot = (ushort)((DateTime.Now.Hour * 60 + DateTime.Now.Minute) / 2);
        public ByteCache(byte[] data)
        {
            Data = data;
            LastUse = unchecked((ushort)(((DateTime.Now.Hour * 60 + DateTime.Now.Minute) / 2) - Boot));
        }

        public void SetLast() => LastUse = unchecked((ushort)(((DateTime.Now.Hour * 60 + DateTime.Now.Minute) / 2) - Boot));
        public byte[] Data { get; set; }
        public ushort LastUse { get; set; }
    }
}
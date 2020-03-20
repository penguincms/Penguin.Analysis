using System;
using System.Collections.Generic;
using System.Text;

namespace Penguin.Analysis
{
    public class ByteCache
    {
        public byte[] Data { get; set; }
        public DateTime LastUse { get; set; } = DateTime.Now;
    }
}
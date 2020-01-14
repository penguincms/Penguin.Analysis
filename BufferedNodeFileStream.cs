using System;
using System.IO;

namespace Penguin.Analysis
{
    public class MemoryNodeFileStream : INodeFileStream
    {
        private MemoryStream stream = new MemoryStream();

        public bool Ready { get; set; }

        public long SourceOffset;

        public MemoryNodeFileStream(long sourceOffset)
        {
            this.SourceOffset = sourceOffset;
        }

        public long Offset => this.stream.Position + this.SourceOffset;

        public long Seek(long lastOffset)
        {
            return this.stream.Seek(lastOffset - this.SourceOffset, SeekOrigin.Begin);
        }

        public void Write(byte[] toWrite)
        {
            this.stream.Write(toWrite, 0, toWrite.Length);
        }

        public void Write(long v)
        {
            this.Write(BitConverter.GetBytes(v));
        }

        public byte[] ToArray()
        {
            return this.stream.ToArray();
        }
    }
}
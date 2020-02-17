using System;

namespace Penguin.Analysis
{
    internal interface INodeFileStream : IDisposable
    {
        long Offset { get; }

        long Seek(long lastOffset);

        void Write(byte[] toWrite);

        void Write(long v);
    }
}
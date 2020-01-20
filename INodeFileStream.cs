using System;

namespace Penguin.Analysis
{
    internal interface INodeFileStream : IDisposable
    {
        long Offset { get; }

        void Write(byte[] toWrite);

        void Write(long v);

        long Seek(long lastOffset);
    }
}
namespace Penguin.Analysis
{
    internal interface INodeFileStream
    {
        long Offset { get; }

        void Write(byte[] toWrite);

        void Write(long v);

        long Seek(long lastOffset);
    }
}
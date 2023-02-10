using Penguin.Analysis.Extensions;
using System;
using System.IO;
using System.Threading;

namespace Penguin.Analysis
{
    public partial class LockedNodeFileStream : INodeFileStream, IDisposable
    {
        private static readonly object NodeFileLock = new();
        private static int StreamPointer;
        private readonly bool PoolStreams;
        private FileStream _backingStream;
        private StreamLock[] StreamPool = new StreamLock[System.Environment.ProcessorCount * 2];
        public string FilePath => _backingStream.Name;
        public long Offset => _backingStream.Position;

        public LockedNodeFileStream(FileStream backingStream, bool poolStreams = true)
        {
            if (_backingStream is null)
            {
                if (backingStream is null)
                {
                    throw new ArgumentNullException(nameof(backingStream));
                }

                _backingStream = backingStream;

                if (poolStreams)
                {
                    for (int i = 0; i < StreamPool.Length; i++)
                    {
                        StreamPool[i] = new StreamLock(backingStream);
                    }
                }
            }

            PoolStreams = poolStreams;
        }

        public LockedNodeFileStream(string FilePath) : this(new FileStream(FilePath, FileMode.CreateNew))
        {
        }

        public static void Lock()
        {
            Monitor.Enter(NodeFileLock);
        }

        public byte[] ReadBlock(long offset)
        {
            byte[] bByte;

            FileStream sourceStream;
            StreamLock sl = default;

            if (PoolStreams)
            {
                while (!Monitor.TryEnter((sl = StreamPool[StreamPointer++ % StreamPool.Length]).LockObject))
                {
                }
                if (StreamPointer >= StreamPool.Length)
                {
                    StreamPointer = 0;
                }
                sourceStream = sl.Stream;

                //sourceStream = new FileStream(StreamPool[0].Stream.Name, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            else
            {
                sourceStream = _backingStream;
            }

            _ = sourceStream.Seek(offset, SeekOrigin.Begin);

            int childCount;

            switch (offset)
            {
                case DiskNode.HEADER_BYTES:
                    bByte = new byte[DiskNode.NODE_SIZE + 4];
                    _ = sourceStream.Read(bByte, 0, bByte.Length);
                    childCount = bByte.GetInt(DiskNode.NODE_SIZE);
                    break;

                default:
                    bByte = new byte[DiskNode.NODE_SIZE + 2];
                    _ = sourceStream.Read(bByte, 0, bByte.Length);
                    childCount = bByte.GetShort(DiskNode.NODE_SIZE);
                    break;
            }

            if (childCount == 0)
            {
                if (PoolStreams)
                {
                    Monitor.Exit(sl.LockObject);
                }

                return bByte;
            }

            byte[] cByte = new byte[childCount * DiskNode.NEXT_SIZE];

            _ = sourceStream.Read(cByte, 0, cByte.Length);

            if (PoolStreams)
            {
                Monitor.Exit(sl.LockObject);
            }

            byte[] toReturn = new byte[bByte.Length + cByte.Length];

            bByte.CopyTo(toReturn, 0);

            cByte.CopyTo(toReturn, DiskNode.NODE_SIZE + (offset == DiskNode.HEADER_BYTES ? 4 : 2));

            return toReturn;
        }

        public int ReadInt()
        {
            byte[] intBytes = new byte[4];

            _ = _backingStream.Read(intBytes, 0, intBytes.Length);

            return BitConverter.ToInt32(intBytes, 0);
        }

        public long ReadLong()
        {
            byte[] intBytes = new byte[8];

            _ = _backingStream.Read(intBytes, 0, intBytes.Length);

            return BitConverter.ToInt32(intBytes, 0);
        }

        public sbyte ReadSbyte()
        {
            return unchecked((sbyte)_backingStream.ReadByte());
        }

        public long Seek(long lastOffset)
        {
            return _backingStream.Seek(lastOffset, SeekOrigin.Begin);
        }

        public static void Unlock()
        {
            Monitor.Exit(NodeFileLock);
        }

        public void Write(string v)
        {
            Write(System.Text.Encoding.UTF8.GetBytes(v));
        }

        public void Write(char v)
        {
            _backingStream.WriteByte((byte)v);
        }

        public void Write(long v)
        {
            _backingStream.Write(BitConverter.GetBytes(v), 0, 8);
        }

        public void Write(int v)
        {
            _backingStream.Write(BitConverter.GetBytes(v), 0, 4);
        }

        public void Write(ushort v)
        {
            _backingStream.Write(BitConverter.GetBytes(v), 0, 2);
        }

        public void Write(byte v)
        {
            _backingStream.WriteByte(v);
        }

        public void Write(sbyte v)
        {
            unchecked
            {
                _backingStream.WriteByte((byte)v);
            }
        }

        public void Write(byte[] v)
        {
            if (v is null)
            {
                throw new ArgumentNullException(nameof(v));
            }

            _backingStream.Write(v, 0, v.Length);
        }

        internal void Flush()
        {
            _backingStream.Flush();
        }

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (StreamLock sLock in StreamPool)
                    {
                        try
                        {
                            sLock.Dispose();
                        }
                        catch (Exception)
                        {
                        }
                    }
                    _backingStream.Dispose();

                    StreamPool = new StreamLock[System.Environment.ProcessorCount * 2];

                    _backingStream = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~LockedNodeFileStream()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support
    }
}
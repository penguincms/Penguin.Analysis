using Penguin.Analysis.Extensions;
using System;
using System.IO;
using System.Threading;

namespace Penguin.Analysis
{
    public class LockedNodeFileStream : INodeFileStream, IDisposable
    {
        private static object NodeFileLock = new object();
        private static int StreamPointer = 0;
        private static StreamLock[] StreamPool;
        private FileStream _backingStream;
        private bool PoolStreams;
        public long Offset => this._backingStream.Position;

        private struct StreamLock : IDisposable
        {
            public object LockObject;

            public FileStream Stream;

            public StreamLock(FileStream source)
            {
                this.LockObject = new object();
                this.Stream = new FileStream(source.Name, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            public void Dispose()
            {
                this.Stream?.Dispose();
                this.Stream = null;
            }
        }

        static LockedNodeFileStream()
        {
            StreamPool = new StreamLock[System.Environment.ProcessorCount * 2];
        }

        public LockedNodeFileStream(FileStream backingStream, bool poolStreams = true)
        {
            if (this._backingStream is null)
            {
                this._backingStream = backingStream;

                if (poolStreams)
                {
                    for (int i = 0; i < StreamPool.Length; i++)
                    {
                        StreamPool[i] = new StreamLock(backingStream);
                    }
                }
            }

            this.PoolStreams = poolStreams;
        }

        public LockedNodeFileStream(string FilePath) : this(new FileStream(FilePath, FileMode.CreateNew))
        {
        }

        public void Lock()
        {
            Monitor.Enter(NodeFileLock);
        }

        public byte[] ReadBlock(long offset)
        {
            int p = 0;

            byte[] bByte = new byte[DiskNode.NodeSize];

            FileStream sourceStream;
            StreamLock sl = default;

            if (this.PoolStreams)
            {
                while (!Monitor.TryEnter((sl = StreamPool[StreamPointer++ % StreamPool.Length]).LockObject))
                {
                    if (StreamPointer >= StreamPool.Length)
                    {
                        StreamPointer = 0;
                    }
                }

                sourceStream = sl.Stream;
            }
            else
            {
                sourceStream = this._backingStream;
            }

            sourceStream.Seek(offset, SeekOrigin.Begin);

            sourceStream.Read(bByte, 0, DiskNode.NodeSize);

            int childCount = bByte.GetInt(DiskNode.NodeSize - 4);

            if (childCount == 0)
            {
                if (this.PoolStreams)
                {
                    Monitor.Exit(sl.LockObject);
                }

                return bByte;
            }

            byte[] cByte = new byte[childCount * DiskNode.NextSize];

            sourceStream.Read(cByte, 0, cByte.Length);

            if (this.PoolStreams)
            {
                Monitor.Exit(sl.LockObject);
            }

            byte[] toReturn = new byte[bByte.Length + cByte.Length];

            bByte.CopyTo(toReturn, 0);

            cByte.CopyTo(toReturn, DiskNode.NodeSize);

            return toReturn;
        }

        public int ReadInt()
        {
            byte[] intBytes = new byte[4];

            this._backingStream.Read(intBytes, 0, intBytes.Length);

            return BitConverter.ToInt32(intBytes, 0);
        }

        public long ReadLong()
        {
            byte[] intBytes = new byte[8];

            this._backingStream.Read(intBytes, 0, intBytes.Length);

            return BitConverter.ToInt32(intBytes, 0);
        }

        public sbyte ReadSbyte()
        {
            return unchecked((sbyte)this._backingStream.ReadByte());
        }

        public long Seek(long offset)
        {
            return this._backingStream.Seek(offset, SeekOrigin.Begin);
        }

        public void Unlock()
        {
            Monitor.Exit(NodeFileLock);
        }

        public void Write(string v)
        {
            this.Write(System.Text.Encoding.UTF8.GetBytes(v));
        }

        public void Write(char v)
        {
            this._backingStream.WriteByte((byte)v);
        }

        public void Write(long v)
        {
            this._backingStream.Write(BitConverter.GetBytes(v), 0, 8);
        }

        public void Write(int v)
        {
            this._backingStream.Write(BitConverter.GetBytes(v), 0, 4);
        }

        public void Write(byte v)
        {
            this._backingStream.WriteByte(v);
        }

        public void Write(sbyte v)
        {
            unchecked
            {
                this._backingStream.WriteByte((byte)v);
            }
        }

        public void Write(byte[] v)
        {
            this._backingStream.Write(v, 0, v.Length);
        }

        internal void Flush()
        {
            this._backingStream.Flush();
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
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
                    this._backingStream.Dispose();

                    StreamPool = new StreamLock[System.Environment.ProcessorCount * 2];

                    this._backingStream = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
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
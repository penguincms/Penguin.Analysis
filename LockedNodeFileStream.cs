using Penguin.Analysis.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Penguin.Analysis
{
    public class LockedNodeFileStream : INodeFileStream, IDisposable
    {

        
        private FileStream _backingStream;

        private static object NodeFileLock = new object();

        private static StreamLock[] StreamPool;
        private static int StreamPointer = 0;

        private struct StreamLock
        {
            public StreamLock(FileStream source)
            {
                LockObject = new object();
                Stream = new FileStream(source.Name, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            public object LockObject;
            public FileStream Stream;
        }

        static LockedNodeFileStream()
        {
            StreamPool = new StreamLock[System.Environment.ProcessorCount * 2];
        }

        public LockedNodeFileStream(FileStream backingStream)
        {
            if (this._backingStream is null) {
                this._backingStream = backingStream;

                for (int i = 0; i < StreamPool.Length; i++)
                {
                    StreamPool[i] = new StreamLock(backingStream);
                }
            }
        }

        public LockedNodeFileStream(string FilePath) : this(new FileStream(FilePath, FileMode.CreateNew))
        {
        }

        public long Seek(long offset)
        {
            return this._backingStream.Seek(offset, SeekOrigin.Begin);
        }

        public void Lock()
        {
            Monitor.Enter(NodeFileLock);
        }

        public void Unlock()
        {
            Monitor.Exit(NodeFileLock);
        }

        public long Offset => this._backingStream.Position;

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

        public void Dispose()
        {
            ((IDisposable)this._backingStream).Dispose();
        }

        public int ReadInt()
        {
            byte[] intBytes = new byte[4];

            _backingStream.Read(intBytes, 0, intBytes.Length);

            return BitConverter.ToInt32(intBytes, 0);
        }

        public sbyte ReadSbyte()
        {
            return unchecked((sbyte)_backingStream.ReadByte());
        }

        public long ReadLong()
        {
            byte[] intBytes = new byte[8];

            _backingStream.Read(intBytes, 0, intBytes.Length);

            return BitConverter.ToInt32(intBytes, 0);
        }
        public byte[] ReadBlock(long offset)
        {

            int p = 0;

            byte[] bByte = new byte[DiskNode.NodeSize];

            StreamLock sl;
            while(!Monitor.TryEnter((sl = StreamPool[StreamPointer++ % StreamPool.Length]).LockObject)){
                if(StreamPointer >= StreamPool.Length)
                {
                    StreamPointer = 0;
                }
            }

            sl.Stream.Seek(offset, SeekOrigin.Begin);

            sl.Stream.Read(bByte, 0, DiskNode.NodeSize);

            int childCount = bByte.GetInt(DiskNode.NodeSize - 4);

            if(childCount == 0)
            {
                Monitor.Exit(sl.LockObject);
                return bByte;
            }

            byte[] cByte = new byte[childCount * DiskNode.NextSize];

            sl.Stream.Read(cByte, 0, cByte.Length);

            Monitor.Exit(sl.LockObject);

            byte[] toReturn = new byte[bByte.Length + cByte.Length];

            bByte.CopyTo(toReturn, 0);

            cByte.CopyTo(toReturn, DiskNode.NodeSize);

            return toReturn;
        }

        internal void Flush()
        {
            this._backingStream.Flush();
        }
    }
}
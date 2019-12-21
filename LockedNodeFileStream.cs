using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Penguin.Analysis
{
    public class LockedNodeFileStream : IDisposable
    {
        private FileStream _backingStream;

        static object NodeFileLock = new object();
        
        public LockedNodeFileStream(FileStream backingStream)
        {
            _backingStream = backingStream;
        }

        public LockedNodeFileStream(string FilePath) : this(new FileStream(FilePath, FileMode.CreateNew))
        {

        }

        public long Seek(long offset)
        {
            return _backingStream.Seek(offset, SeekOrigin.Begin);
        }

        public void Lock()
        {
            Monitor.Enter(NodeFileLock);
        }

        public void Unlock()
        {
            Monitor.Exit(NodeFileLock);
        }

        public long Offset
        {
            get
            {
                return _backingStream.Position;
            }
        }

        public void Write(string v) => Write(System.Text.Encoding.UTF8.GetBytes(v));
        public void Write(char v)
        {
            _backingStream.WriteByte((byte)v);
        }

        public void Write(long v)
        {
            _backingStream.Write(BitConverter.GetBytes(v), 0, 8);
        }

        public void Write(byte[] v)
        {
            _backingStream.Write(v, 0, v.Length);
        }

        public void Dispose()
        {
            ((IDisposable)this._backingStream).Dispose();
        }

        public byte[] ReadBlock(long offset)
        {
            Lock();
            _backingStream.Seek(offset, SeekOrigin.Begin);

            List<byte> toReturn = new List<byte>();

            int p = 0;

            byte[] bByte = new byte[DiskNode.NodeSize];

            _backingStream.Read(bByte, 0, DiskNode.NodeSize);

            toReturn.AddRange(bByte);

            byte[] cByte = new byte[8];

            do
            {
                _backingStream.Read(cByte, 0, 8);

                if(BitConverter.ToInt64(cByte, 0) == 0)
                {
                    break;
                } else
                {
                    toReturn.AddRange(cByte);
                }
            } while (true);
                    

            Unlock();

            return toReturn.ToArray();
        }

        internal void Flush()
        {
            _backingStream.Flush();
        }
    }
}

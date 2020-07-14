using System;
using System.IO;

namespace Penguin.Analysis
{
    public class MemoryNodeFileStream : INodeFileStream
    {
        public long SourceOffset;
        private MemoryStream stream = new MemoryStream();

        public long Offset => this.stream.Position + this.SourceOffset;
        public bool Ready { get; set; }

        public MemoryNodeFileStream(long sourceOffset)
        {
            this.SourceOffset = sourceOffset;
        }

        public long Seek(long lastOffset)
        {
            return this.stream.Seek(lastOffset - this.SourceOffset, SeekOrigin.Begin);
        }

        public byte[] ToArray()
        {
            return this.stream.ToArray();
        }

        public void Write(byte[] toWrite)
        {
            if (toWrite is null)
            {
                throw new ArgumentNullException(nameof(toWrite));
            }

            this.stream.Write(toWrite, 0, toWrite.Length);
        }

        public void Write(long v)
        {
            this.Write(BitConverter.GetBytes(v));
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
                    this.stream.Dispose();
                    this.stream = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~MemoryNodeFileStream()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support
    }
}
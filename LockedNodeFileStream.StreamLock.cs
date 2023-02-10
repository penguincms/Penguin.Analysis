using System;
using System.IO;

namespace Penguin.Analysis
{
    public partial class LockedNodeFileStream
    {
        private struct StreamLock : IDisposable
        {
            public object LockObject;

            public FileStream Stream;

            public StreamLock(FileStream source)
            {
                LockObject = new object();
                Stream = new FileStream(source.Name, FileMode.Open, FileAccess.Read, FileShare.Read, 10000);
            }

            public void Dispose()
            {
                Stream?.Dispose();
                Stream = null;
            }
        }
    }
}
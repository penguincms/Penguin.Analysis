using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Penguin.Analysis
{
    public class OptimizedRootNode : Node
    {
        public static int ExecutingEvaluations = 0;
        private static ConcurrentQueue<Byte[]> ArrayPool = new ConcurrentQueue<byte[]>();
        private static ConcurrentDictionary<long, ByteCache> CachedBytes = new ConcurrentDictionary<long, ByteCache>();

        private static int LastMatchAmount = 0;
        private List<DiskNode>[][] next;

        public static Task FlushTask { get; set; } = Task.CompletedTask;
        public override int ChildCount { get; }

        public override sbyte ChildHeader => -1;

        public override sbyte Header { get; } = -1;

        public override long Key { get; }

        public override IEnumerable<INode> Next
        {
            get
            {
                for (int header = 0; header < next.Length; header++)
                {
                    for (int value = 0; value < next[header].Length; value++)
                    {
                        foreach (INode n in next[header][value])
                        {
                            yield return n;
                        }
                    }
                }
            }
        }

        public override INode ParentNode { get; }

        public override ushort[] Results { get; } = new ushort[4];

        public override ushort Value { get; } = 0;

        private DataSourceSettings Settings { get; set; }

        public OptimizedRootNode(INode source, DataSourceSettings settings)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Settings = settings;

            int MaxHeader = 0;

            IEnumerable<INode> Parents = source.Next.Where(n => !(n is null));
            IEnumerable<INode> Children = Parents.SelectMany(n => n.Next).Where(n => !(n is null));

            foreach (INode n in Parents)
            {
                MaxHeader = Math.Max(MaxHeader, n.ChildHeader);
            }

            int[] MaxValues = new int[MaxHeader + 1];
            next = new List<DiskNode>[MaxValues.Length][];

            foreach (INode n in Parents)
            {
                MaxValues[n.ChildHeader] = Math.Max(MaxValues[n.ChildHeader], n.ChildCount);
            }

            for (int header = 0; header <= MaxHeader; header++)
            {
                next[header] = new List<DiskNode>[MaxValues[header]];

                for (int value = 0; value < MaxValues[header]; value++)
                {
                    next[header][value] = new List<DiskNode>();
                }
            }

            foreach (DiskNode c in Children)
            {
                next[c.Header][c.Value].Add(c);
            }
        }

        public static void Flush()
        {
            CachedBytes.Clear();
            ArrayPool = new ConcurrentQueue<byte[]>();
            ExecutingEvaluations = 0;
            FlushTask = Task.CompletedTask;
            LastMatchAmount = 0;
        }

        public void Evaluate(Evaluation e, bool MultiThread = true) => this.Evaluate(e, 0, MultiThread);

        public override void Evaluate(Evaluation e, long routeKey, bool MultiThread = true)
        {
            ExecutingEvaluations++;

            try
            {
                if (e is null)
                {
                    throw new ArgumentNullException(nameof(e));
                }

                string FilePath = DiskNode._backingStream.FilePath;

                object streamLock = new object();

                using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess))
                {
                    List<DiskNode> matchingNodes = new List<DiskNode>(LastMatchAmount);

                    for (int header = 0; header < next.Length; header++)
                    {
                        int value = e.DataRow[header];

                        if (next[header].Length > value)
                        {
                            matchingNodes.AddRange(next[header][value]);
                        }
                    }

                    LastMatchAmount = Math.Max(LastMatchAmount, matchingNodes.Count);

                    Parallel.ForEach(matchingNodes, (n) =>
                    {
                        if (n.NextOffset != 0)
                        {
                            long bLength = n.NextOffset - n.Offset;

                            if (!ArrayPool.TryDequeue(out byte[] backingData) || backingData.Length < bLength)
                            {
                                backingData = new byte[bLength];
                            }

                            if (!CachedBytes.TryGetValue(n.Offset, out ByteCache cachedBytes))
                            {
                                cachedBytes = new ByteCache()
                                {
                                    Data = new byte[bLength]
                                };

                                lock (streamLock)
                                {
                                    fs.Seek(n.Offset, SeekOrigin.Begin);

                                    fs.Read(cachedBytes.Data, 0, (int)bLength);
                                }

                                CachedBytes.TryAdd(n.Offset, cachedBytes);
                            }

                            cachedBytes.LastUse = DateTime.Now;

                            cachedBytes.Data.CopyTo(backingData, 0);

                            DiskNode nn = new DiskNode(backingData, n.Offset, n.Offset);

                            nn.Evaluate(e, 0, MultiThread);
                        }
                        else
                        {
                            n.Evaluate(e, 0, MultiThread);
                        }
                    });
                }
            }
            finally
            {
                ExecutingEvaluations--;
                FlushMemory();
            }
        }

        public async Task Preload(string FilePath)
        {
            try
            {
                using (LockedNodeFileStream ns = new LockedNodeFileStream(new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess)))
                {
                    using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess))
                    {
                        using (FileStream fsn = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess))
                        {
                            byte[] offsetBytes = new byte[DiskNode.HEADER_BYTES];

                            fs.Read(offsetBytes, 0, offsetBytes.Length);

                            long JsonOffset = offsetBytes.GetLong(0);
                            long SortOffset = offsetBytes.GetLong(8);

                            fs.Seek(SortOffset, SeekOrigin.Begin);

                            ulong freeMem = SystemInterop.Memory.Status.ullAvailPhys;

                            while (freeMem > this.Settings.MinFreeMemory + this.Settings.RangeFreeMemory)
                            {
                                for (int i = 0; i < this.Settings.PreloadChunkSize / 2; i++)
                                {
                                    if (fs.Position >= JsonOffset)
                                    {
                                        return;
                                    }

                                    byte[] thisNodeBytes = new byte[8];

                                    fs.Read(thisNodeBytes, 0, thisNodeBytes.Length);

                                    long offset = thisNodeBytes.GetLong();

                                    if (!CachedBytes.ContainsKey(offset))
                                    {
                                        DiskNode dn = new DiskNode(ns, offset);

                                        if (dn.NextOffset != 0)
                                        {
                                            long bLength = dn.NextOffset - dn.Offset;

                                            ByteCache cachedBytes = new ByteCache()
                                            {
                                                Data = new byte[bLength]
                                            };

                                            fsn.Seek(dn.Offset, SeekOrigin.Begin);

                                            fsn.Read(cachedBytes.Data, 0, (int)bLength);

                                            CachedBytes.TryAdd(dn.Offset, cachedBytes);

                                            freeMem -= (ulong)cachedBytes.Data.Length;
                                        }
                                    }

                                    if (freeMem > this.Settings.MinFreeMemory + this.Settings.RangeFreeMemory)
                                    {
                                        return;
                                    }
                                }

                                freeMem = SystemInterop.Memory.Status.ullAvailPhys;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }

        private void FlushMemory()
        {
            try
            {
                if (ExecutingEvaluations == 0 && (FlushTask is null || FlushTask.IsCompleted))
                {
                    FlushTask = Task.Run(() =>
                    {
                        ulong freeMem = SystemInterop.Memory.Status.ullAvailPhys;

                        if (freeMem < Settings.MinFreeMemory)
                        {
                            List<KeyValuePair<long, ByteCache>> cachedBytes = CachedBytes.OrderBy(v => v.Value.LastUse).ToList();

                            foreach (KeyValuePair<long, ByteCache> kvp in cachedBytes)
                            {
                                if (CachedBytes.TryRemove(kvp.Key, out _))
                                {
                                    freeMem -= (ulong)kvp.Value.Data.Length;
                                }

                                if (freeMem > this.Settings.MinFreeMemory + this.Settings.RangeFreeMemory)
                                {
                                    break;
                                }
                            }

                            cachedBytes.Clear();

                            GC.Collect();

                            Task.Delay(1000).Wait();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    throw;
                }
            }
        }

        #region IDisposable Support

        public override INode NextAt(int index) => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    foreach (INode n in this.Next)
                    {
                        try
                        {
                            n.Dispose();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    this.next = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~OptimizedRootNode()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support
    }
}
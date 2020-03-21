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
        private static ByteCache[] CachedBytes;

        private static int LastMatchAmount = 0;
        private List<INodeBlock>[][] next;

        private HashSet<long> NoCache = new HashSet<long>();
        public static Task FlushTask { get; set; } = Task.CompletedTask;
        public override int ChildCount { get; }

        public override sbyte ChildHeader => -1;

        public override sbyte Header { get; } = -1;

        public override long Key { get; }

        public override IEnumerable<INode> Next => throw new NotImplementedException();

        public override INode ParentNode { get; }

        public override ushort[] Results { get; } = new ushort[4];

        public override ushort Value { get; } = 0;

        private DataSourceSettings Settings { get; set; }

        public OptimizedRootNode(DiskNode source, DataSourceSettings settings)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Settings = settings;

            int MaxHeader = 0;

            IEnumerable<DiskNode> Parents = source.Next.Cast<DiskNode>().Where(n => !(n is null));
            IEnumerable<DiskNode> Children = Parents.SelectMany(n => n.Next.Cast<DiskNode>()).Where(n => !(n is null));

            foreach (DiskNode n in Parents)
            {
                MaxHeader = Math.Max(MaxHeader, n.ChildHeader);
            }

            int[] MaxValues = new int[MaxHeader + 1];
            next = new List<INodeBlock>[MaxValues.Length][];

            foreach (DiskNode n in Parents)
            {
                MaxValues[n.ChildHeader] = Math.Max(MaxValues[n.ChildHeader], n.ChildCount + n.SkipChildren);
            }

            for (int header = 0; header <= MaxHeader; header++)
            {
                next[header] = new List<INodeBlock>[MaxValues[header]];

                for (int value = 0; value < MaxValues[header]; value++)
                {
                    next[header][value] = new List<INodeBlock>();
                }
            }

            int cbCount = 0;
            foreach (DiskNode c in Children)
            {
                if (c.NextOffset == 0)
                {
                    next[c.Header][c.Value].Add(c);
                    NoCache.Add(c.Offset);
                }
                else
                {
                    next[c.Header][c.Value].Add(new NodeBlock() { NextOffset = c.NextOffset, Offset = c.Offset, Index = cbCount++ });
                    c.Clear();
                }
            }

            CachedBytes = new ByteCache[cbCount];

            DiskNode.FlushCache();

            GC.Collect();
        }

        public static void Flush()
        {
            CachedBytes = Array.Empty<ByteCache>();
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
                    List<INodeBlock> matchingNodes = new List<INodeBlock>(LastMatchAmount);

                    for (int header = 0; header < next.Length; header++)
                    {
                        int value = e.DataRow[header];

                        if (next[header].Length > value)
                        {
                            matchingNodes.AddRange(next[header][value]);
                        }
                    }

                    LastMatchAmount = Math.Max(LastMatchAmount, matchingNodes.Count);

                    void Evaluate(INodeBlock n)
                    {
                        if (n is DiskNode dn)
                        {
                            dn.Evaluate(e, 0, MultiThread);
                        }
                        else if (n is NodeBlock nb)
                        {
                            long bLength = n.NextOffset - n.Offset;

                            if (!ArrayPool.TryDequeue(out byte[] backingData) || backingData.Length < bLength)
                            {
                                backingData = new byte[bLength];
                            }

                            ByteCache cachedBytes = CachedBytes[nb.Index];

                            if (cachedBytes.Data is null)
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

                                CachedBytes[nb.Index] = cachedBytes;
                            }

                            cachedBytes.SetLast();

                            cachedBytes.Data.CopyTo(backingData, 0);

                            DiskNode nn = new DiskNode(backingData, n.Offset, n.Offset);

                            nn.Evaluate(e, 0, MultiThread);
                        }
                    }

                    if (MultiThread)
                    {
                        Parallel.ForEach(matchingNodes, Evaluate);
                    }
                    else
                    {
                        foreach (INodeBlock nb in matchingNodes)
                        {
                            Evaluate(nb);
                        }
                    }
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
                Dictionary<long, NodeBlock> nodeblocks = new Dictionary<long, NodeBlock>();

                for (int header = 0; header < next.Length; header++)
                {
                    for (int value = 0; value < next[header].Length; value++)
                    {
                        foreach (INodeBlock inb in next[header][value])
                        {
                            if (inb is NodeBlock nb)
                            {
                                nodeblocks.Add(nb.Offset, nb);
                            }
                        }
                    }
                }
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

                                    byte[] thisNodeBytes = new byte[5];

                                    fs.Read(thisNodeBytes, 0, thisNodeBytes.Length);

                                    long offset = thisNodeBytes.GetInt40();

                                    if (!NoCache.Contains(offset))
                                    {
                                        if (!nodeblocks.TryGetValue(offset, out NodeBlock nb))
                                        {
                                            continue;
                                        }

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

                                            CachedBytes[nb.Index] = cachedBytes;

                                            freeMem -= (ulong)cachedBytes.Data.Length;
                                        }
                                    }

                                    if (freeMem < this.Settings.MinFreeMemory + this.Settings.RangeFreeMemory)
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
            IEnumerable<(int Index, ByteCache byteCache)> CheckCache()
            {
                HashSet<ushort> Times = new HashSet<ushort>();

                for (int i = 0; i < CachedBytes.Length; i++)
                {
                    if (CachedBytes[i].Data != null)
                    {
                        Times.Add(CachedBytes[i].LastUse);
                    }
                }

                foreach (ushort t in Times.OrderByDescending(t => t))
                {
                    for (int i = 0; i < CachedBytes.Length; i++)
                    {
                        if (CachedBytes[i].Data != null && CachedBytes[i].LastUse == t)
                        {
                            yield return (i, CachedBytes[i]);
                        }
                    }
                }
            }
            try
            {
                if (ExecutingEvaluations == 0 && (FlushTask is null || FlushTask.IsCompleted))
                {
                    FlushTask = Task.Run(() =>
                    {
                        ulong freeMem = SystemInterop.Memory.Status.ullAvailPhys;

                        if (freeMem < Settings.MinFreeMemory)
                        {
                            IEnumerable<ByteCache> cachedBytes = CachedBytes.Where(b => b.Data != null).OrderByDescending(b => b.LastUse);

                            foreach ((int Index, ByteCache byteCache) in CheckCache())
                            {
                                freeMem += (ulong)CachedBytes[Index].Data.Length;

                                CachedBytes[Index].Data = null;
                                if (freeMem > this.Settings.MinFreeMemory + this.Settings.RangeFreeMemory)
                                {
                                    break;
                                }
                            }

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
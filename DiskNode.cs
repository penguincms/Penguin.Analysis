using Newtonsoft.Json;
using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using static Penguin.Analysis.DataSourceBuilder;

namespace Penguin.Analysis
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DiskNode : INode
    {
        public const int HeaderBytes = 16;
        public const int NextSize = 12;
        public const int NodeSize = 35;
        internal static LockedNodeFileStream _backingStream;
        internal static ConcurrentDictionary<long, DiskNode> Cache = new ConcurrentDictionary<long, DiskNode>();

        internal static Dictionary<long, DiskNode> MemoryManaged = new Dictionary<long, DiskNode>();

        internal long Offset;
        private static object clearCacheLock = new object();

        private byte? depth;

        private int? key;

        private int[] results;

        public float Accuracy => this.GetAccuracy();

        public int ChildCount => this.BackingData.GetInt(DiskNode.NodeSize - 4);

        public sbyte ChildHeader => unchecked((sbyte)this.BackingData[30]);

        public OffsetValue[] ChildOffsets { get; set; }

        public byte Depth
        {
            get
            {
                if (this.depth is null)
                {
                    this.depth = this.GetDepth();
                }
                return this.depth.Value;
            }
        }

        [JsonProperty("H", Order = 2)]
        public sbyte Header => unchecked((sbyte)this.BackingData[24]);

        public int Key
        {
            get
            {
                if (this.key is null)
                {
                    this.key = this.GetKey();
                }
                return this.key.Value;
            }
        }

        [JsonProperty("L", Order = 4)]
        public bool LastNode => this.BackingData[29] == 1;

        public int Matched => this.GetMatched();

        public IEnumerable<INode> Next
        {
            get
            {
                foreach (OffsetValue childOffset in this.ChildOffsets)
                {
                    yield return LoadNode(_backingStream, childOffset.Offset);
                }
            }
        }

        [JsonProperty("P", Order = 0)]
        public DiskNode ParentNode => LoadNode(_backingStream, this.ParentOffset);

        INode INode.ParentNode => this.ParentNode;

        [JsonProperty("R", Order = 1)]
        public int[] Results
        {
            get
            {
                if (this.results is null)
                {
                    this.results = this.BackingData.GetInts(8, 4).ToArray();
                }

                return this.results;
            }
        }

        [JsonProperty("V", Order = 3)]
        public int Value => this.BackingData.GetInt(25);

        private byte[] BackingData { get; set; }

        private long ParentOffset => this.BackingData.GetLong(0);

        public DiskNode(LockedNodeFileStream fileStream, long offset)
        {
            this.Offset = offset;

            this.BackingData = fileStream.ReadBlock(offset);

            this.ChildOffsets = new OffsetValue[this.ChildCount];

            for (int i = 0; i < this.ChildOffsets.Length; i++)
            {
                int oset = NodeSize + (i * NextSize);

                this.ChildOffsets[i] = new OffsetValue()
                {
                    Offset = this.BackingData.GetLong(oset),
                    Value = this.BackingData.GetInt(oset + 8)
                };
            }

            _backingStream = _backingStream ?? fileStream ?? throw new ArgumentNullException(nameof(fileStream));
        }

        public static int ClearCache(MemoryManagementStyle memoryManagementStyle)
        {
            int CacheSize = Cache.Count;

            if (Monitor.TryEnter(clearCacheLock))
            {
                List<DiskNode> CachedNodes = Cache.Select(c => c.Value).ToList();

                Cache.Clear();

                foreach (DiskNode n in CachedNodes)
                {
                    if (n.Header == -1)
                    {
                        Cache.TryAdd(n.Offset, n);
                    }
                }

                Monitor.Exit(clearCacheLock);
            }

            return CacheSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DiskNode LoadNode(LockedNodeFileStream backingStream, long offset, bool memoryManaged = false)
        {
            if (offset == 0)
            {
                return null;
            }

            if (MemoryManaged.TryGetValue(offset, out DiskNode dn))
            {
                return dn;
            }
            else if (memoryManaged)
            {
                dn = new DiskNode(_backingStream, offset);
                MemoryManaged.Add(offset, dn);
                return dn;
            }

            if (!Cache.TryGetValue(offset, out DiskNode toReturn))
            {
                toReturn = new DiskNode(backingStream, offset);
                Cache.TryAdd(offset, toReturn);
            }

            return toReturn;
        }

        public bool Evaluate(Evaluation e)
        {
            return this.StandardEvaluate(e);
        }

        public void Flush(int depth)
        {
        }

        public DiskNode GetNextByValue(int Value)
        {
            foreach (OffsetValue ov in this.ChildOffsets)
            {
                if (ov.Value == Value)
                {
                    return LoadNode(_backingStream, ov.Offset);
                }
            }

            return null;
        }

        INode INode.GetNextByValue(int Value)
        {
            return this.GetNextByValue(Value);
        }

        public float GetScore(float BaseRate)
        {
            return this.CalculateScore(BaseRate);
        }

        public void Preload(int depth)
        {
            if (this.Header == -1 || depth > 0)
            {
                foreach (DiskNode n in this.Next)
                {
                    n.Preload(depth - 1);
                }
            }
        }

        internal static void DisposeAll()
        {
            try
            {
                MemoryManaged.Clear();
            }
            catch (Exception)
            {
            }

            try
            {
                Cache.Clear();
            }
            catch (Exception)
            {
            }

            try
            {
                _backingStream.Dispose();
            }
            catch (Exception)
            {
            }

            _backingStream = null;
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
                    try
                    {
                        Cache.TryRemove(this.Offset, out DiskNode _);
                    }
                    catch (Exception)
                    {
                    }

                    try
                    {
                        MemoryManaged.Remove(this.Offset);
                    }
                    catch (Exception)
                    {
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DiskNode()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support
    }
}
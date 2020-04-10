using Newtonsoft.Json;
using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Penguin.Analysis
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DiskNode : Node, INodeBlock
    {
        public const int HEADER_BYTES = 16;

        public const int NEXT_SIZE = 5;

        public const int NODE_SIZE = 20;

        internal static LockedNodeFileStream _backingStream;

        internal static ConcurrentDictionary<long, DiskNode> Cache = new ConcurrentDictionary<long, DiskNode>();

        internal static int MaxCacheCount = int.MaxValue;

        internal static Dictionary<long, DiskNode> MemoryManaged = new Dictionary<long, DiskNode>();

        private static readonly object clearCacheLock = new object();

        private static DiskNode RootNode;

        private static ConcurrentDictionary<long, DiskNode> RootNodes = new ConcurrentDictionary<long, DiskNode>();

        private static HashSet<long> SmallCache = new HashSet<long>();

        private bool BackingDataSet = false;

        private long? key;

        private ushort[] results;

        public static bool CacheNodes { get; set; } = true;

        public override int ChildCount
        {
            get
            {
                switch (this.Offset)
                {
                    case HEADER_BYTES:
                        return this.BackingData.GetInt((NODE_SIZE + LookupOffset));

                    default:
                        return this.BackingData.GetShort((NODE_SIZE + LookupOffset));
                }
            }
        }

        public override sbyte ChildHeader => unchecked((sbyte)this.BackingData[12 + LookupOffset]);

        public long[] ChildOffsets { get; set; }

        public override sbyte Header => unchecked((sbyte)this.BackingData[9 + LookupOffset]);

        public override long Key
        {
            get
            {
                if (this.key is null)
                {
                    this.key = GetKey();
                }
                return this.key.Value;
            }
        }

        public override IEnumerable<INode> Next
        {
            get
            {
                foreach (long o in this.ChildOffsets)
                {
                    DiskNode dn = LoadNode(_backingStream, o);

                    yield return dn;
                }
            }
        }

        public long NextOffset => this.BackingData.GetInt40((NODE_SIZE + LookupOffset) - 5);

        public long Offset { get; internal set; }

        public override INode ParentNode => LoadNode(_backingStream, this.ParentOffset);

        public override ushort[] Results
        {
            get
            {
                if (this.results is null)
                {
                    this.results = this.BackingData.GetShorts(5 + LookupOffset, 2).ToArray();
                }

                return this.results;
            }
        }

        public ushort SkipChildren => this.BackingData.GetShort((13 + LookupOffset));

        public override ushort Value => this.BackingData.GetShort(10 + LookupOffset);

        internal static int CurrentCacheCount { get; set; } = 0;

        internal byte[] BackingData { get; set; }

        internal long ParentOffset => this.BackingData.GetInt40(LookupOffset);

        private long BackingDataOffset { get; set; } = 0;

        private long LookupOffset => this.Offset - this.BackingDataOffset;

        public DiskNode(byte[] backingData, long backingDataOffset, long offset)
        {
            BackingData = backingData;
            BackingDataOffset = backingDataOffset;
            Offset = offset;
            BackingDataSet = true;
        }

        public DiskNode(LockedNodeFileStream fileStream, long offset)
        {
            if (fileStream is null)
            {
                throw new ArgumentNullException(nameof(fileStream));
            }

            this.Offset = offset;
            this.BackingDataOffset = offset;

            this.BackingData = fileStream.ReadBlock(offset);

            this.ChildOffsets = new long[this.ChildCount];

            for (int i = 0; i < this.ChildOffsets.Length; i++)
            {
                int oset = NODE_SIZE + (i * NEXT_SIZE) + (offset == 16 ? 4 : 2);

                this.ChildOffsets[i] = this.BackingData.GetInt40(oset);
            }

            _backingStream = _backingStream ?? fileStream ?? throw new ArgumentNullException(nameof(fileStream));
        }

        public override ushort this[MatchResult result]
        {
            get
            {
                return (((int)result > 1) ? (ushort)0 : this.Results[(int)result]);
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public static int ClearCache()
        {
            int CacheSize = Cache.Count;

            if (Monitor.TryEnter(clearCacheLock))
            {
                List<DiskNode> CachedNodes = Cache.Select(c => c.Value).ToList();

                Cache.Clear();

                CurrentCacheCount -= CachedNodes.Count;

                foreach (DiskNode n in CachedNodes)
                {
                    if (n.Header == -1 && Cache.TryAdd(n.Offset, n))
                    {
                        CurrentCacheCount++;
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

            if (!CacheNodes)
            {
                if (RootNode is null)
                {
                    RootNode = new DiskNode(backingStream, DiskNode.HEADER_BYTES);
                    foreach (long l in RootNode.ChildOffsets)
                    {
                        SmallCache.Add(l);
                    }
                }

                if (offset == DiskNode.HEADER_BYTES)
                {
                    return RootNode;
                }

                if (!RootNodes.TryGetValue(offset, out DiskNode thisNode))
                {
                    thisNode = new DiskNode(backingStream, offset);

                    if (SmallCache.Contains(offset) || SmallCache.Contains(thisNode.ParentOffset))
                    {
                        RootNodes.TryAdd(offset, thisNode);
                    }
                }

                return thisNode;
            }

            if (MemoryManaged.TryGetValue(offset, out DiskNode dn))
            {
                return dn;
            }
            else if (memoryManaged && CurrentCacheCount < MaxCacheCount)
            {
                dn = new DiskNode(_backingStream, offset);
                CurrentCacheCount++;
                MemoryManaged.Add(offset, dn);
                return dn;
            }

            if (!Cache.TryGetValue(offset, out DiskNode toReturn))
            {
                toReturn = new DiskNode(backingStream, offset);

                if (CurrentCacheCount < MaxCacheCount && Cache.TryAdd(offset, toReturn))
                {
                    CurrentCacheCount++;
                }
            }

            return toReturn ?? new DiskNode(_backingStream, offset);
        }

        public override INode NextAt(int index)
        {
            index -= SkipChildren;

            if (index < 0 || index > (ChildCount - 1))
            {
                return null;
            }

            if (!BackingDataSet)
            {
                if (this.ChildOffsets[index] == 0)
                {
                    return null;
                }
                return LoadNode(_backingStream, this.ChildOffsets[index]);
            }
            else
            {
                long coffset = this.BackingData.GetInt40(NODE_SIZE + (index * NEXT_SIZE) + (this.Offset == 16 ? 4 : 2) + LookupOffset);

                if (coffset == 0)
                {
                    return null;
                }

                return new DiskNode(BackingData, this.BackingDataOffset, coffset);
            }
        }

        public override void Preload(int depth)
        {
            if (this.Header == -1 || depth > 0)
            {
                foreach (DiskNode n in this.Next)
                {
                    n.Preload(depth - 1);
                }
            }
        }

        public void SetBackingData(byte[] backingData)
        {
            BackingData = backingData;
            BackingDataOffset = Offset;
            BackingDataSet = true;
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

        internal static void FlushCache()
        {
            SmallCache = new HashSet<long>();
            RootNodes = new ConcurrentDictionary<long, DiskNode>();
            Cache = new ConcurrentDictionary<long, DiskNode>();
            MemoryManaged = new Dictionary<long, DiskNode>();
        }

        internal void Clear()
        {
            results = Array.Empty<ushort>();
            BackingData = null;
            ChildOffsets = Array.Empty<long>();
        }

        #region IDisposable Support

        protected override void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing && !this.BackingDataSet)
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
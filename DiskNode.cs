using Newtonsoft.Json;
using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Penguin.Analysis
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DiskNode : Node
    {
        public const int HEADER_BYTES = 16;
        public const int NEXT_SIZE = 10;
        public const int NODE_SIZE = 24;
        internal static LockedNodeFileStream _backingStream;
        internal static ConcurrentDictionary<long, DiskNode> Cache = new ConcurrentDictionary<long, DiskNode>();
        internal static int MaxCacheCount = int.MaxValue;
        internal static Dictionary<long, DiskNode> MemoryManaged = new Dictionary<long, DiskNode>();
        internal long Offset;
        private static readonly object clearCacheLock = new object();
        private static readonly Dictionary<long, DiskNode> RootNodes = new Dictionary<long, DiskNode>();
        private static DiskNode RootNode;
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
                        return this.BackingData.GetInt((NODE_SIZE + BackingDataOffset));

                    default:
                        return this.BackingData.GetShort((NODE_SIZE + BackingDataOffset));
                }
            }
        }

        public override sbyte ChildHeader => unchecked((sbyte)this.BackingData[15 + BackingDataOffset]);
        public OffsetValue[] ChildOffsets { get; set; }
        public override sbyte Header => unchecked((sbyte)this.BackingData[12 + BackingDataOffset]);

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
                foreach (OffsetValue o in this.ChildOffsets)
                {
                    DiskNode dn = LoadNode(_backingStream, o.Offset);

                    yield return dn;
                }
            }
        }

        public override INode ParentNode => LoadNode(_backingStream, this.ParentOffset);

        public override ushort[] Results
        {
            get
            {
                if (this.results is null)
                {
                    this.results = this.BackingData.GetShorts(8 + BackingDataOffset, 2).Concat(new ushort[] { 0, 0 }).ToArray();
                }

                return this.results;
            }
        }

        public override ushort Value => this.BackingData.GetShort(13 + BackingDataOffset);
        internal static int CurrentCacheCount { get; set; } = 0;
        internal long NextOffset => this.BackingData.GetLong((NODE_SIZE + BackingDataOffset) - 8);
        internal long ParentOffset => this.BackingData.GetLong(BackingDataOffset);
        private byte[] BackingData { get; set; }
        private long BackingDataOffset { get; set; } = 0;

        public DiskNode(byte[] backingData, long backingDataOffset, long offset)
        {
            BackingData = backingData;
            BackingDataOffset = backingDataOffset;
            Offset = offset;
            BackingDataSet = true;

            this.ChildOffsets = new OffsetValue[this.ChildCount];

            for (int i = 0; i < this.ChildOffsets.Length; i++)
            {
                int oset = NODE_SIZE + (i * NEXT_SIZE) + (offset == 16 ? 4 : 2);

                this.ChildOffsets[i] = new OffsetValue()
                {
                    Offset = this.BackingData.GetLong(oset + BackingDataOffset),
                    Value = this.BackingData.GetShort(oset + 8 + BackingDataOffset)
                };
            }
        }

        public DiskNode(LockedNodeFileStream fileStream, long offset)
        {
            if (fileStream is null)
            {
                throw new ArgumentNullException(nameof(fileStream));
            }

            this.Offset = offset;

            this.BackingData = fileStream.ReadBlock(offset);

            this.ChildOffsets = new OffsetValue[this.ChildCount];

            for (int i = 0; i < this.ChildOffsets.Length; i++)
            {
                int oset = NODE_SIZE + (i * NEXT_SIZE) + (offset == 16 ? 4 : 2);

                this.ChildOffsets[i] = new OffsetValue()
                {
                    Offset = this.BackingData.GetLong(oset),
                    Value = this.BackingData.GetShort(oset + 8)
                };
            }

            _backingStream = _backingStream ?? fileStream ?? throw new ArgumentNullException(nameof(fileStream));
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
                    foreach (long l in RootNode.ChildOffsets.Select(o => o.Offset))
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
                        RootNodes.Add(offset, thisNode);
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
            if (!BackingDataSet)
            {
                return LoadNode(_backingStream, this.ChildOffsets[index].Offset);
            }
            else
            {
                return new DiskNode(BackingData, this.ChildOffsets[index].Offset - this.Offset, this.ChildOffsets[index].Offset);
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

        #region IDisposable Support

        protected override void Dispose(bool disposing)
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
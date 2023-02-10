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
    public class DiskNode : Node, INodeBlock
    {
        public const int HEADER_BYTES = 16;

        public const int NEXT_SIZE = 5;

        public const int NODE_SIZE = 20;

        internal static LockedNodeFileStream _backingStream;

        internal static ConcurrentDictionary<long, DiskNode> Cache = new();

        internal static int MaxCacheCount = int.MaxValue;

        internal static Dictionary<long, DiskNode> MemoryManaged = new();

        private static readonly object clearCacheLock = new();

        private static DiskNode RootNode;

        private static ConcurrentDictionary<long, DiskNode> RootNodes = new();

        private static HashSet<long> SmallCache = new();

        private bool BackingDataSet;

        private long? key;

        private ushort[] results;

        public static bool CacheNodes { get; set; } = true;

        public override int ChildCount => Offset switch
        {
            HEADER_BYTES => BackingData.GetInt(NODE_SIZE + LookupOffset),
            _ => BackingData.GetShort(NODE_SIZE + LookupOffset),
        };

        public override sbyte ChildHeader => unchecked((sbyte)BackingData[12 + LookupOffset]);

        public long[] ChildOffsets { get; set; }

        public override sbyte Header => unchecked((sbyte)BackingData[9 + LookupOffset]);

        public override long Key
        {
            get
            {
                key ??= GetKey();
                return key.Value;
            }
        }

        public override IEnumerable<INode> Next
        {
            get
            {
                foreach (long o in ChildOffsets)
                {
                    DiskNode dn = LoadNode(_backingStream, o);

                    yield return dn;
                }
            }
        }

        public long NextOffset => BackingData.GetInt40(NODE_SIZE + LookupOffset - 5);

        public long Offset { get; internal set; }

        public override INode ParentNode => LoadNode(_backingStream, ParentOffset);

        public override ushort[] Results
        {
            get
            {
                results ??= BackingData.GetShorts(5 + LookupOffset, 2).ToArray();

                return results;
            }
        }

        public ushort SkipChildren => BackingData.GetShort(13 + LookupOffset);

        public override ushort Value => BackingData.GetShort(10 + LookupOffset);

        internal static int CurrentCacheCount { get; set; }

        internal byte[] BackingData { get; set; }

        internal long ParentOffset => BackingData.GetInt40(LookupOffset);

        private long BackingDataOffset { get; set; }

        private long LookupOffset => Offset - BackingDataOffset;

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

            Offset = offset;
            BackingDataOffset = offset;

            BackingData = fileStream.ReadBlock(offset);

            ChildOffsets = new long[ChildCount];

            for (int i = 0; i < ChildOffsets.Length; i++)
            {
                int oset = NODE_SIZE + (i * NEXT_SIZE) + (offset == 16 ? 4 : 2);

                ChildOffsets[i] = BackingData.GetInt40(oset);
            }

            _backingStream ??= fileStream ?? throw new ArgumentNullException(nameof(fileStream));
        }

        public override ushort this[MatchResult result]
        {
            get => ((int)result > 1) ? (ushort)0 : Results[(int)result];
            set => throw new NotImplementedException();
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
                        _ = SmallCache.Add(l);
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
                        _ = RootNodes.TryAdd(offset, thisNode);
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
                return ChildOffsets[index] == 0 ? null : (INode)LoadNode(_backingStream, ChildOffsets[index]);
            }
            else
            {
                long coffset = BackingData.GetInt40(NODE_SIZE + (index * NEXT_SIZE) + (Offset == 16 ? 4 : 2) + LookupOffset);

                return coffset == 0 ? null : (INode)new DiskNode(BackingData, BackingDataOffset, coffset);
            }
        }

        public override void Preload(int depth)
        {
            if (Header == -1 || depth > 0)
            {
                foreach (DiskNode n in Next)
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
            if (!disposedValue)
            {
                if (disposing && !BackingDataSet)
                {
                    try
                    {
                        _ = Cache.TryRemove(Offset, out _);
                    }
                    catch (Exception)
                    {
                    }

                    try
                    {
                        _ = MemoryManaged.Remove(Offset);
                    }
                    catch (Exception)
                    {
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
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
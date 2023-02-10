using Newtonsoft.Json;
using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis
{
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class MemoryNode : Node
    {
        public bool HasValidChild
        {
            get
            {
                if (next is null)
                {
                    return false;
                }

                foreach (MemoryNode mn in next)
                {
                    if (mn != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        #region Fields

        private long? key;

        public override long Key
        {
            get
            {
                key ??= GetKey();
                return key.Value;
            }
        }

        public List<TypelessDataRow> MatchingRows { get; }

        #endregion Fields

        #region Properties

        internal MemoryNode parentNode;

        private readonly ushort value;
        public override sbyte Header => header;
        public ushort SkipChildren { get; set; }
        public bool LastNode { get; set; }
        public int Matched => this[MatchResult.Route] + this[MatchResult.Both];
        public override IEnumerable<INode> Next => next;
        public override INode ParentNode => parentNode;
        public override ushort Value => value;
        internal sbyte header;
        internal MemoryNode[] next;

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Deserialization only. Dont use this unless you're a deserializer
        /// </summary>
        public MemoryNode()
        { }

        public MemoryNode(sbyte header, ushort value, int children, ushort backingRows)
        {
            this.header = header;

            MatchingRows = new List<TypelessDataRow>(backingRows);

            this[MatchResult.None] = backingRows;

            this.value = value;

            if (children != 0)
            {
                next = new MemoryNode[children];
                LastNode = false;
            }
            else
            {
                LastNode = true;
            }
        }

        public void AddNext(MemoryNode next, int i)
        {
            if (next is null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            this.next[i] = next;
            next.parentNode = this;
        }

        public bool Trim(DataSourceBuilder sourceBuilder)
        {
            if (sourceBuilder is null)
            {
                throw new ArgumentNullException(nameof(sourceBuilder));
            }

            int lastGoodNode = 0;
            ushort firstGoodNode = 0;

            bool trimmed = false;

            if (Header != -1 && !HasValidChild)
            {
                if (Math.Abs(GetScore(sourceBuilder.Result.BaseRate)) < sourceBuilder.Settings.Results.MinumumScore && ParentNode != null)
                {
                    sourceBuilder.Settings.TrimmedNode?.Invoke(this);

                    parentNode.RemoveNode(this);

                    return true;
                }
#if DEBUG
                else
                {
                }
#endif
                LastNode = true;
            }

            if (!HasValidChild)
            {
                if (next != null && ChildCount > 0)
                {
                    next = Array.Empty<MemoryNode>();
                    trimmed = true;
                }
            }
            else
            {
                foreach (MemoryNode mn in next)
                {
                    if (mn != null)
                    {
                        trimmed = trimmed || mn.Trim(sourceBuilder);
                    }
                }

                for (int i = next.Length; i > 0; i--)
                {
                    if (next[i - 1] != null)
                    {
                        lastGoodNode = i;
                        break;
                    }
                }

                for (ushort i = 0; i < next.Length; i++)
                {
                    if (next[i] != null)
                    {
                        firstGoodNode = i;
                        break;
                    }
                }

                if (lastGoodNode != next.Length)
                {
                    trimmed = true;

                    next = next.Take(lastGoodNode).ToArray();
                }

                if (firstGoodNode != 0)
                {
                    trimmed = true;
                    SkipChildren += firstGoodNode;
                    next = next.Skip(firstGoodNode).ToArray();
                }
            }

            return trimmed;
        }

        public long GetLength(byte childListSize)
        {
            long length = DiskNode.NODE_SIZE + childListSize;

            if (next is not null)
            {
                foreach (MemoryNode cnode in next)
                {
                    length += DiskNode.NEXT_SIZE;
                    length += cnode?.GetLength(2) ?? 0;
                }
            }

            return length;
        }

        public void RemoveNode(MemoryNode n)
        {
            for (int i = 0; i < next.Length; i++)
            {
                if (next[i] == n)
                {
                    next[i] = null;
                    break;
                }
            }
        }

        public void TrimNext(DataSourceBuilder sourceBuilder)
        {
            if (sourceBuilder is null)
            {
                throw new ArgumentNullException(nameof(sourceBuilder));
            }

            if (ChildHeader < 0)
            {
                throw new Exception();
            }

            foreach (TypelessDataRow row in MatchingRows)
            {
                MemoryNode matchNext = next[row[ChildHeader]];

                matchNext?.MatchRow(row);
            }

            for (int i = 0; i < next.Length; i++)
            {
                if (next[i].Matched < sourceBuilder.Settings.Results.MinimumHits)
                {
                    sourceBuilder.Settings.TrimmedNode?.Invoke(next[i]);

                    next[i] = null;
                }
            }
        }

        internal SerializationResults Serialize(INodeFileStream lockedNodeFileStream, long ParentOffset = 0)
        {
            SerializationResults results = new(lockedNodeFileStream, this, ParentOffset);
            long thisOffset = results.Single().Offset;
            //parent 0 - 8

            byte childListSize = (byte)(ParentOffset == 0 ? 4 : 2);

            byte[] toWrite = new byte[DiskNode.NODE_SIZE + childListSize];

            ParentOffset.ToInt40Array().CopyTo(toWrite, 0);

            for (int i = 0; i < 2; i++)
            {
                BitConverter.GetBytes(Results[i]).CopyTo(toWrite, 5 + (i * 2));
            }

            unchecked
            {
                toWrite[9] = (byte)Header;
            }

            BitConverter.GetBytes(Value).CopyTo(toWrite, 10);

            toWrite[12] = (byte)ChildHeader;

            BitConverter.GetBytes(SkipChildren).CopyTo(toWrite, 13);

            int nCount = ChildCount;

            byte[] childBytes;

            if (childListSize == 4)
            {
                childBytes = BitConverter.GetBytes(nCount);
            }
            else
            {
                if (nCount > ushort.MaxValue)
                {
                    throw new Exception($"Cant not exceed {ushort.MaxValue} children on non-root node");
                }
                else
                {
                    ushort usCount = (ushort)nCount;
                    childBytes = BitConverter.GetBytes(usCount);
                }
            }

            childBytes.CopyTo(toWrite, DiskNode.NODE_SIZE);

            lockedNodeFileStream.Write(toWrite);

            if (nCount > 0)
            {
                int skip = nCount * DiskNode.NEXT_SIZE;

                long ChildListOffset = lockedNodeFileStream.Offset;

                byte[] skipBytes = new byte[skip];

                lockedNodeFileStream.Write(skipBytes);

                byte[] nextOffsets = new byte[nCount * DiskNode.NEXT_SIZE];

                int i;

                for (i = 0; i < nCount; i++)
                {
                    MemoryNode nextn = next[i];

                    byte[] offsetBytes = nextn != null ? lockedNodeFileStream.Offset.ToInt40Array() : (new byte[] { 0, 0, 0, 0, 0 });
                    offsetBytes.CopyTo(nextOffsets, i * DiskNode.NEXT_SIZE);

                    if (nextn != null)
                    {
                        results.AddRange(nextn.Serialize(lockedNodeFileStream, thisOffset));
                    }
                }

                long lastOffset = lockedNodeFileStream.Offset;

                _ = lockedNodeFileStream.Seek(ChildListOffset - childBytes.Length - 5);

                lockedNodeFileStream.Write(lastOffset.ToInt40Array());

                _ = lockedNodeFileStream.Seek(ChildListOffset);

                lockedNodeFileStream.Write(nextOffsets);

                _ = lockedNodeFileStream.Seek(lastOffset);
            }

            return results;
        }

        //public bool AddNext(DataSourceBuilder sourceBuilder, MemoryNode next, int i, bool trim = true)
        //{
        //    if (sourceBuilder is null)
        //    {
        //        throw new ArgumentNullException(nameof(sourceBuilder));
        //    }

        //    if (next is null)
        //    {
        //        throw new ArgumentNullException(nameof(next));
        //    }

        //    if (trim)
        //    {
        //        foreach (TypelessDataRow row in MatchingRows)
        //        {
        //            next.MatchRow(row);
        //        }

        //        int hits = next.Matched;

        //        if (sourceBuilder.Settings.Results.MatchOnly && next[MatchResult.Both] == 0)
        //        {
        //            hits = 0;
        //        }

        //        if (hits >= sourceBuilder.Settings.Results.MinimumHits)
        //        {
        //            this.next[i] = next;
        //            next.parentNode = this;
        //            return true;
        //        }
        //    }
        //    else
        //    {
        //        this.next[i] = next;
        //        next.parentNode = this;
        //        return true;
        //    }

        //    return false;
        //}

        #endregion Constructors

        #region Methods

        public override int ChildCount => next?.Length ?? 0;

        public override sbyte ChildHeader
        {
            get
            {
                if (ChildCount != 0)
                {
                    foreach (MemoryNode mn in next)
                    {
                        if (mn != null)
                        {
                            return mn.header;
                        }
                    }
                }

                return -1;
            }
        }

        public override string ToString()
        {
            return Header == -1 ? "" : ParentNode is null ? $"{Header}: {Value}" : $"{ParentNode} => {Header}: {Value}";
        }

        #region IDisposable Support

        // This code added to correctly implement the disposable pattern.
        public void MatchRow(TypelessDataRow dataRow)
        {
            if (dataRow is null)
            {
                throw new ArgumentNullException(nameof(dataRow));
            }

            if (Header == -1)
            {
                MatchingRows.Add(dataRow);
                return;
            }

            MatchResult pool = dataRow.MatchesOutput ? MatchResult.Both : MatchResult.Route;

            MatchingRows.Add(dataRow);

            this[MatchResult.None]--;
            this[pool]++;
        }

        public override INode NextAt(int index)
        {
            return next[index];
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    MatchingRows.Clear();
                    foreach (MemoryNode n in next)
                    {
                        try
                        {
                            n.Dispose();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    next = Array.Empty<MemoryNode>();
                    parentNode = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Node()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support

        #endregion Methods
    }
}
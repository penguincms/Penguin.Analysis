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
        #region Fields

        private long? key;

        public override long Key
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

        public IList<TypelessDataRow> MatchingRows { get; set; }

        #endregion Fields

        #region Properties

        public MemoryNode parentNode;

        private ushort value;
        public override sbyte Header => header;

        public bool LastNode { get; set; }
        public int Matched => this[MatchResult.Route] + this[MatchResult.Both];
        public override IEnumerable<INode> Next => next;
        public override INode ParentNode => parentNode;
        public override ushort Value => value;
        internal sbyte header { get; set; }
        internal MemoryNode[] next { get; set; }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Deserialization only. Dont use this unless you're a deserializer
        /// </summary>
        public MemoryNode() { }

        public MemoryNode(sbyte header, ushort value, int children, ushort backingRows)
        {
            this.header = header;

            this.MatchingRows = new List<TypelessDataRow>(backingRows);

            this[MatchResult.None] = backingRows;

            this.value = value;

            if (children != 0)
            {
                this.next = new MemoryNode[children];
                this.LastNode = false;
            }
            else
            {
                this.LastNode = true;
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

        public void CheckValidity()
        {
            if (!LastNode && (next is null || !next.Any(n => n != null)))
            {
                parentNode?.RemoveNode(this);
            }
        }

        public long GetLength(byte childListSize)
        {
            long length = DiskNode.NODE_SIZE + childListSize;

            if (!(next is null))
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
            for (int i = 0; i < this.next.Length; i++)
            {
                if (this.next[i] == n)
                {
                    this.next[i] = null;
                    break;
                }
            }

            this.CheckValidity();
        }

        public void TrimNext(DataSourceBuilder sourceBuilder)
        {
            if (sourceBuilder is null)
            {
                throw new ArgumentNullException(nameof(sourceBuilder));
            }

            if (this.ChildHeader < 0)
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

                    this.next[i] = null;
                }
            }
        }

        internal SerializationResults Serialize(INodeFileStream lockedNodeFileStream, long ParentOffset = 0)
        {
            SerializationResults results = new SerializationResults(lockedNodeFileStream, this, ParentOffset);
            long thisOffset = results.Single().Offset;
            //parent 0 - 8

            byte childListSize = (byte)(ParentOffset == 0 ? 4 : 2);

            byte[] toWrite = new byte[DiskNode.NODE_SIZE + childListSize];

            BitConverter.GetBytes(ParentOffset).CopyTo(toWrite, 0);

            for (int i = 0; i < 2; i++)
            {
                BitConverter.GetBytes(Results[i]).CopyTo(toWrite, 8 + i * 2);
            }

            unchecked
            {
                toWrite[12] = (byte)Header;
            }

            BitConverter.GetBytes(Value).CopyTo(toWrite, 13);

            toWrite[15] = (byte)ChildHeader;

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
                int skip = (nCount * DiskNode.NEXT_SIZE);

                long ChildListOffset = lockedNodeFileStream.Offset;

                byte[] skipBytes = new byte[skip];

                lockedNodeFileStream.Write(skipBytes);

                byte[] nextOffsets = new byte[nCount * DiskNode.NEXT_SIZE];

                int i;

                for (i = 0; i < nCount; i++)
                {
                    MemoryNode nextn = this.next[i];

                    long offset = 0;

                    if (nextn != null)
                    {
                        offset = lockedNodeFileStream.Offset;
                    }

                    BitConverter.GetBytes(offset).CopyTo(nextOffsets, i * DiskNode.NEXT_SIZE);

                    if (nextn != null)
                    {
                        results.AddRange(nextn.Serialize(lockedNodeFileStream, thisOffset));
                    }
                }

                long lastOffset = lockedNodeFileStream.Offset;

                lockedNodeFileStream.Seek(ChildListOffset - childBytes.Length - 8);

                lockedNodeFileStream.Write(lastOffset);

                lockedNodeFileStream.Seek(ChildListOffset);

                lockedNodeFileStream.Write(nextOffsets);

                lockedNodeFileStream.Seek(lastOffset);
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

        public override int ChildCount => this.next?.Length ?? 0;

        public override sbyte ChildHeader => (sbyte)(this.ChildCount > 0 ? this.next.Where(n => n != null).Select(n => n.Header).Distinct().Single() : -1);

        public override string ToString()
        {
            if (this.Header == -1)
            {
                return "";
            }
            else if (this.ParentNode is null)
            {
                return $"{this.Header}: {this.Value}";
            }
            else
            {
                return $"{this.ParentNode} => {this.Header}: {this.Value}";
            }
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

        public override INode NextAt(int index) => next[index];

        protected override void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.MatchingRows.Clear();
                    foreach (MemoryNode n in this.next)
                    {
                        try
                        {
                            n.Dispose();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    this.next = Array.Empty<MemoryNode>();
                    this.parentNode = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
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
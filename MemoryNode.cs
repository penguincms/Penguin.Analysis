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
        internal bool lastNode;
        private int value;
        public override sbyte Header => header;

        public override bool LastNode => lastNode;
        public override IEnumerable<INode> Next => next;
        public override INode ParentNode => parentNode;
        public override int Value => value;
        internal sbyte header { get; set; }
        internal MemoryNode[] next { get; set; }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Deserialization only. Dont use this unless you're a deserializer
        /// </summary>
        public MemoryNode() { }

        public MemoryNode(sbyte header, int value, int children, int backingRows)
        {
            this.header = header;

            this.MatchingRows = new List<TypelessDataRow>(backingRows);

            this[MatchResult.None] = backingRows;

            this.value = value;

            if (children != 0)
            {
                this.next = new MemoryNode[children];
                this.lastNode = false;
            }
            else
            {
                this.lastNode = true;
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

        public long GetLength()
        {
            long length = DiskNode.NODE_SIZE;

            if (!(next is null))
            {
                foreach (MemoryNode cnode in next)
                {
                    length += DiskNode.NEXT_SIZE;
                    length += cnode?.GetLength() ?? 0;
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

            MatchResult pool = MatchResult.Route;

            if (dataRow.MatchesOutput)
            {
                pool |= MatchResult.Output;
            }

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
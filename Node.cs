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
    public class Node : INode<Node>
    {
        #region Fields

        private long? key;

        public long Key
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

        [JsonProperty("R", Order = 1)]
        public int[] Results { get; set; } = new int[4];

        public int this[MatchResult result]
        {
            get => this.Results[(int)result];
            set => this.Results[(int)result] = value;
        }

        #endregion Fields

        #region Properties

        public Accuracy Accuracy => this.GetAccuracy();

        public byte Depth => this.GetDepth();

        [JsonProperty("H", Order = 2)]
        public sbyte Header { get; set; }

        [JsonProperty("L", Order = 4)]
        public bool LastNode { get; set; }

        /// <summary>
        /// The number of times this route has been matched against
        /// </summary>
        public int Matched => this.GetMatched();

        [JsonProperty("N", Order = 5)]
        public Node[] Next { get; set; }

        [JsonProperty("P", Order = 0)]
        public Node ParentNode { get; set; }

        [JsonProperty("V", Order = 3)]
        public int Value { get; set; }

        INode INode.GetNextByValue(int Value)
        {
            return this.GetNextByValue(Value);
        }

        public double GetScore(float BaseRate)
        {
            return this.CalculateScore(BaseRate);
        }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Deserialization only. Dont use this unless you're a deserializer
        /// </summary>
        public Node() { }

        public Node(sbyte header, int value, int children, int backingRows)
        {
            this.Header = header;

            this.MatchingRows = new List<TypelessDataRow>(backingRows);

            this[MatchResult.None] = backingRows;

            this.Value = value;

            if (children != 0)
            {
                this.Next = new Node[children];
                this.LastNode = false;
            }
            else
            {
                this.LastNode = true;
            }
        }

        #endregion Constructors

        #region Methods

        public int ChildCount => this.Next?.Length ?? 0;

        public sbyte ChildHeader => (sbyte)(this.ChildCount > 0 ? this.Next.Select(n => n.Header).Distinct().Single() : -1);

        IEnumerable<INode> INode.Next => this.Next?.Cast<INode>()?.ToArray();

        IEnumerable<Node> INode<Node>.Next => this.Next;

        INode INode.ParentNode => this.ParentNode;

        public bool Evaluate(Evaluation e, bool MultiThread = true)
        {
            return this.StandardEvaluate(e);
        }

        public void Flush(int depth)
        {
        }

        public Node GetNextByValue(int Value)
        {
            if (this.ChildCount == 0)
            {
                return null;
            }
            else
            {
                foreach (Node n in this.Next)
                {
                    if (n.Value == Value)
                    {
                        return n;
                    }
                }
            }

            return null;
        }

        public void Preload(int depth)
        {
        }

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
                    this.MatchingRows.Clear();
                    foreach (Node n in this.Next)
                    {
                        try
                        {
                            n.Dispose();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    this.Next = Array.Empty<Node>();
                    this.ParentNode = null;
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
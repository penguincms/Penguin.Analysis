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

        public IList<TypelessDataRow> MatchingRows { get; set; }

        [JsonProperty("R", Order = 1)]
        public int[] Results { get; set; } = new int[4];

        public int Key
        {
            get
            {
                if (key is null)
                {
                    key = this.GetKey();
                }
                return key.Value;
            }
        }

        private int? key;

        #endregion Fields

        #region Properties
        INode INode.GetNextByValue(int Value) => this.GetNextByValue(Value);
        public float Accuracy => this.GetAccuracy();

        public byte Depth => this.GetDepth();

        public float GetScore(float BaseRate)
        {
            return this.CalculateScore(BaseRate);
        }

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

        public void Preload(int depth)
        {

        }

        public void Flush(int depth)
        {

        }

        public bool Evaluate(Evaluation e) => this.StandardEvaluate(e);

        public Node GetNextByValue(int Value)
        {
            if(ChildCount == 0)
            {
                return null;
            } else
            {
                foreach(Node n in Next)
                {
                    if(n.Value == Value)
                    {
                        return n;
                    }
                }
            }

            return null;
        }

        INode INode.ParentNode => this.ParentNode;

        IEnumerable<INode> INode.Next => this.Next?.Cast<INode>()?.ToArray();

        IEnumerable<Node> INode<Node>.Next
        {
            get => this.Next;
        }

        public int ChildCount => this.Next?.Length ?? 0;

        public sbyte ChildHeader => (sbyte)(ChildCount > 0 ? this.Next.Select(n => n.Header).Distinct().Single() : -1);

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    MatchingRows.Clear();
                    foreach(Node n in Next)
                    {
                        try
                        {
                            n.Dispose();
                        } catch(Exception)
                        {

                        }
                    }

                    Next = Array.Empty<Node>();
                    ParentNode = null;
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

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        #endregion Methods
    }
}
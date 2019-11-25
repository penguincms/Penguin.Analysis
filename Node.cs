using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Penguin.Analysis
{
    [Serializable]
    public class Node
    {
        #region Fields

        [JsonIgnore]
        public IList<TypelessDataRow> MatchingRows;

        public int[] Results = new int[4];

        #endregion Fields

        #region Properties

        public float Accuracy
        {
            get
            {
                if (this.Results[(int)MatchResult.Route] == 0 && this.Results[(int)MatchResult.Both] > 0)
                {
                    return 1;
                }

                float d = (this.Results[(int)MatchResult.Route] + this.Results[(int)MatchResult.Both]);

                return d == 0 ? 0 : this.Results[(int)MatchResult.Both] / d;
            }
        }

        [JsonIgnore]
        public int Depth
        {
            get
            {
                Node toCheck = this;
                int depth = 0;

                while (toCheck != null && toCheck.Header != -1)
                {
                    depth++;

                    toCheck = toCheck.ParentNode;
                }

                return depth;
            }
        }

        public sbyte Header { get; set; }

        public bool LastNode { get; set; }

        /// <summary>
        /// The number of times this route has been matched against
        /// </summary>
        public int Matched => this.Results[(int)MatchResult.Route] + this.Results[(int)MatchResult.Both];

        public Node[] Next { get; set; }

        //public List<string> Paths { get; set; } = new List<string>();
        public Node ParentNode { get; set; }

        [JsonIgnore]
        public float Score => (this.Results[(int)MatchResult.Both] + 1) / (this.Results[(int)MatchResult.Route] + this.Results[(int)MatchResult.Both]) - (float)(this.Results[(int)MatchResult.Route] + 1) / (this.Results[(int)MatchResult.Route] + this.Results[(int)MatchResult.Both]);

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

        #endregion Methods
    }
}
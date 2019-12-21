using Newtonsoft.Json;
using Penguin.Analysis.Interfaces;
using System;

namespace Penguin.Analysis
{
    [Serializable]
    public class AnalysisResults
    {
        #region Properties

        public float BaseRate => this.PositiveIndicators / this.TotalRows;
        public double ExpectedMatches { get; set; }
        public float PositiveIndicators { get; set; }

        [JsonIgnore]
        public TypelessDataTable RawData { get; set; }

        [JsonIgnore]
        public INode RootNode { get; set; }

        [JsonIgnore]
        public Node BuilderRootNote {
            get
            {
                if(RootNode is Node n)
                {
                    return n;
                } else
                {
                    throw new Exception($"Attempt has been made to access root node that is not of type {nameof(Node)}. The node type is {RootNode.GetType().ToString()}");
                }
            }
            set
            {
                RootNode = value;
            }
        }
        public int TotalRoutes { get; set; }
        public float TotalRows { get; set; }

        #endregion Properties
    }
}
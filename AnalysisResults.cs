using Newtonsoft.Json;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;

namespace Penguin.Analysis
{
    [Serializable]
    public class AnalysisResults
    {
        #region Properties

        public float BaseRate => this.PositiveIndicators / this.TotalRows;
        public double ExpectedMatches { get; set; }
        public float PositiveIndicators { get; set; }

        public int[] ColumnInstances = new int[256];

        public int GraphInstances { get; set; } = 0;

        [JsonIgnore]
        public TypelessDataTable RawData { get; set; }

        [JsonIgnore]
        public INode RootNode { get; set; }

        [JsonIgnore]
        public Node BuilderRootNote
        {
            get
            {
                if (this.RootNode is Node n)
                {
                    return n;
                }
                else
                {
                    throw new Exception($"Attempt has been made to access root node that is not of type {nameof(Node)}. The node type is {this.RootNode.GetType().ToString()}");
                }
            }
            set => this.RootNode = value;
        }

        public int TotalRoutes { get; set; }
        public float TotalRows { get; set; }

        #endregion Properties
    }
}
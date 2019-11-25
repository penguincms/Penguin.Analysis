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
        public TypelessDataTable RawData { get; set; }
        public Node RootNode { get; set; }
        public int TotalRoutes { get; set; }
        public float TotalRows { get; set; }

        #endregion Properties
    }
}
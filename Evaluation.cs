using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Penguin.Analysis
{
    public class Evaluation
    {
        #region Properties

        public TypelessDataRow DataRow { get; set; }

        public ConcurrentDictionary<int, INode> MatchedRoutes { get; } = new ConcurrentDictionary<int, INode>();

        public int MatchingRoutes { get; set; }

        public AnalysisResults Result { get; set; }

        public float Score => !this.MatchedRoutes.Any() ? 0 : this.Scores.Average();

        public ConcurrentBag<float> Scores { get; }
        protected AnalysisResults AnalysisResults { get; set; }

        #endregion Properties

        #region Constructors

        public Evaluation(TypelessDataRow dataRow, AnalysisResults BuilderResults)
        {
            this.AnalysisResults = BuilderResults;
            this.Scores = new ConcurrentBag<float>();
            this.DataRow = dataRow;
        }

        #endregion Constructors

        #region Methods

        public void MatchRoute(INode n)
        {
            if (n is null)
            {
                throw new ArgumentNullException(nameof(n));
            }

            int Key = n.Key;

            if (!this.MatchedRoutes.ContainsKey(Key))
            {
                this.MatchedRoutes.TryAdd(Key, n);

                int counts = this.AnalysisResults.GraphInstances / this.AnalysisResults.ColumnInstances[n.Header];

                for (int i = 0; i < counts; i++)
                {
                    this.Scores.Add(n.GetScore(this.Result.BaseRate));
                }
            }
        }

        #endregion Methods
    }
}
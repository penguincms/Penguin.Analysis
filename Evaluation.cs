using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis
{
    public class Evaluation
    {
        #region Properties

        public Dictionary<string, string> CalculatedData { get; set; } = new Dictionary<string, string>();
        public TypelessDataRow DataRow { get; set; }
        public Dictionary<string, string> InputData { get; set; } = new Dictionary<string, string>();
        public ConcurrentDictionary<long, INode> MatchedRoutes { get; } = new ConcurrentDictionary<long, INode>();

        public int MatchingRoutes { get; set; }

        public AnalysisResults Result { get; set; }

        public ConcurrentBag<Score> Scores { get; }

        public double Value
        {
            get
            {
                if (!this.MatchedRoutes.Any())
                {
                    return 0;
                }

                double score = 0;
                double counts = 0;

                foreach (Score Escore in this.Scores)
                {
                    counts += Escore.Count;

                    score += Escore.Count * Escore.Value;
                }

                return score / counts;
            }
        }

        protected AnalysisResults AnalysisResults { get; set; }

        public class Score
        {
            public double Count { get; set; }
            public double Value { get; set; }
        }

        #endregion Properties

        #region Constructors

        public Evaluation(TypelessDataRow dataRow, AnalysisResults BuilderResults)
        {
            this.AnalysisResults = BuilderResults;
            this.Scores = new ConcurrentBag<Score>();
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

            long Key = n.Key;

            if (!this.MatchedRoutes.ContainsKey(Key))
            {
                this.MatchedRoutes.TryAdd(Key, n);

                if (this.AnalysisResults.ColumnInstances.ContainsKey(n.Key))
                {
                    this.Scores.Add(new Score()
                    {
                        Value = n.GetScore(this.Result.BaseRate),
                        Count = this.AnalysisResults.GraphInstances / this.AnalysisResults.ColumnInstances[n.Key]
                    });
                }
            }
        }

        #endregion Methods
    }
}
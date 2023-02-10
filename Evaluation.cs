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

        public Dictionary<string, string> CalculatedData { get; } = new Dictionary<string, string>();

        public TypelessDataRow DataRow { get; set; }

        public Dictionary<string, string> InputData { get; set; } = new Dictionary<string, string>();

        public ConcurrentDictionary<long, INode> MatchedRoutes { get; } = new ConcurrentDictionary<long, INode>();

        public int MatchingRoutes { get; set; }

        public AnalysisResults Result { get; set; }

        public AnalysisScore Score
        {
            get
            {
                if (!MatchedRoutes.Any())
                {
                    return new AnalysisScore()
                    {
                        Value = 0,
                        OldValue = 0
                    };
                }

                AnalysisScore toReturn = new();

                double oldScore = 0;
                double oldCounts = 0;
                double score = 0;
                double counts = 0;

                foreach (AnalysisScore Escore in Scores.Select(k => k.Value))
                {
                    oldCounts += Escore.OldCount;

                    oldScore += Escore.OldCount * Escore.OldValue;

                    double count = Result.GraphInstances / Escore.ColumnInstances;

                    counts += count;

                    score += count * Escore.Value;
                }

                toReturn.OldCount = oldCounts;
                toReturn.OldValue = oldScore / oldCounts;
                toReturn.ColumnInstances = counts;
                toReturn.Value = score / counts;

                return toReturn;
            }
        }

        public ConcurrentDictionary<long, AnalysisScore> Scores { get; }

        protected AnalysisResults AnalysisResults { get; set; }

        public class AnalysisScore
        {
            public double ColumnInstances { get; set; }

            public double OldCount { get; set; }

            public double OldValue { get; set; }

            public double Value { get; set; }
        }

        #endregion Properties

        #region Constructors

        public Evaluation(TypelessDataRow dataRow, AnalysisResults BuilderResults)
        {
            AnalysisResults = BuilderResults;
            Scores = new ConcurrentDictionary<long, AnalysisScore>();
            DataRow = dataRow;
        }

        #endregion Constructors

        #region Methods

        public void MatchRoute(INode n, long Key)
        {
            if (n is null)
            {
                throw new ArgumentNullException(nameof(n));
            }

            if (Key == 0)
            {
                throw new ArgumentException("Can not match node with key = 0", nameof(n));
            }

            if (!MatchedRoutes.ContainsKey(Key))
            {
                _ = MatchedRoutes.TryAdd(Key, n);

                if (AnalysisResults.ColumnInstances.TryGetValue(Key, out int value))
                {
                    if (!Scores.TryGetValue(Key, out AnalysisScore score))
                    {
                        score = new AnalysisScore();
                        _ = Scores.TryAdd(Key, score);
                    }

                    score.Value = n.GetScore(Result.BaseRate);
                    score.OldCount = (double)AnalysisResults.GraphInstances / value;
                    score.OldValue = n.GetScore(Result.BaseRate);
                    score.ColumnInstances++;

                    LongByte lb = new(Key);

                    while (lb > 0)
                    {
                        if (!Scores.TryGetValue(lb, out AnalysisScore s))
                        {
                            s = new AnalysisScore();
                            _ = Scores.TryAdd(lb, s);
                        }

                        s.ColumnInstances++;

                        _ = lb.TrimLeft();
                    }
                }
            }
        }

        #endregion Methods
    }
}
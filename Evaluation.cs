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
                if (!this.MatchedRoutes.Any())
                {
                    return new AnalysisScore()
                    {
                        Value = 0,
                        OldValue = 0
                    };
                }

                AnalysisScore toReturn = new AnalysisScore();

                double oldScore = 0;
                double oldCounts = 0;
                double score = 0;
                double counts = 0;

                foreach (AnalysisScore Escore in this.Scores.Select(k => k.Value))
                {
                    oldCounts += Escore.OldCount;

                    oldScore += Escore.OldCount * Escore.OldValue;

                    double count = this.Result.GraphInstances / Escore.ColumnInstances;

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
            this.AnalysisResults = BuilderResults;
            this.Scores = new ConcurrentDictionary<long, AnalysisScore>();
            this.DataRow = dataRow;
        }

        #endregion Constructors

        #region Methods

        ConcurrentDictionary<long, long> CachedKeys = new ConcurrentDictionary<long, long>();
        public long GetKey(INode startNode)
        {
            if (startNode is null)
            {
                throw new ArgumentNullException(nameof(startNode));
            }

            if (startNode is DiskNode dn)
            {
                long Key = 0;

                DiskNode n = dn;

                while (n != null && n.Header != -1)
                {
                    if (CachedKeys.TryGetValue(n.Offset, out long key))
                    {
                        Key |= key;

                        CachedKeys.TryAdd(dn.Offset, Key);

                        return Key;
                    }

                    Key |= ((long)1 << n.Header);

                    n = n.ParentNode as DiskNode;
                }

                CachedKeys.TryAdd(dn.Offset, Key);

                return Key;
            } else
            {
                return startNode.Key;
            }
        }
        public void MatchRoute(INode n)
        {
            if (n is null)
            {
                throw new ArgumentNullException(nameof(n));
            }

            long Key = GetKey(n);

            if (Key == 0)
            {
                throw new ArgumentException("Can not match node with key = 0", nameof(n));
            }

            if (!this.MatchedRoutes.ContainsKey(Key))
            {
                this.MatchedRoutes.TryAdd(Key, n);

                if (this.AnalysisResults.ColumnInstances.ContainsKey(n.Key))
                {
                    if (!this.Scores.TryGetValue(n.Key, out AnalysisScore score))
                    {
                        score = new AnalysisScore();
                        this.Scores.TryAdd(n.Key, score);
                    }

                    score.Value = n.GetScore(this.Result.BaseRate);
                    score.OldCount = (double)this.AnalysisResults.GraphInstances / this.AnalysisResults.ColumnInstances[n.Key];
                    score.OldValue = n.GetScore(this.Result.BaseRate);
                    score.ColumnInstances++;

                    LongByte lb = new LongByte(n.Key);

                    while (lb > 0)
                    {
                        if (!this.Scores.TryGetValue(lb, out AnalysisScore s))
                        {
                            s = new AnalysisScore();
                            this.Scores.TryAdd(lb, s);
                        }

                        s.ColumnInstances++;

                        lb.TrimLeft();
                    }
                }
            }
        }

        #endregion Methods
    }
}
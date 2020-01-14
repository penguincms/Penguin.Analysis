﻿using Penguin.Analysis.Extensions;
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

        public TypelessDataRow DataRow { get; set; }

        public ConcurrentDictionary<int, INode> MatchedRoutes { get; } = new ConcurrentDictionary<int, INode>();

        public int MatchingRoutes { get; set; }

        public AnalysisResults Result { get; set; }

        public float Score => !this.MatchedRoutes.Any() ? 0 : this.Scores.Average();

        protected AnalysisResults AnalysisResults { get; set; }
        public ConcurrentBag<float> Scores { get; }

        #endregion Properties

        #region Constructors

        public Evaluation(TypelessDataRow dataRow, AnalysisResults BuilderResults)
        {
            AnalysisResults = BuilderResults;
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

                int sign = n.Accuracy < this.Result.BaseRate ? -1 : 1;

                int counts = AnalysisResults.GraphInstances / AnalysisResults.ColumnInstances[n.Header];

                for (int i = 0; i < counts; i++)
                {
                    this.Scores.Add(n.GetScore(Result.BaseRate));
                }
            }
        }

        #endregion Methods
    }
}
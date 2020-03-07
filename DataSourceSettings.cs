using Newtonsoft.Json;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;

namespace Penguin.Analysis
{
    public class DataSourceSettings
    {
        [JsonIgnore]
        public Action<IEnumerable<string>, bool> CheckedConstraint = null;

        [JsonIgnore]
        public Action<INode> TrimmedNode = null;

        public ulong MinFreeMemory { get; set; } = 1_000_000_000;
        public int NodeFlushDepth { get; set; } = 0;

        [JsonIgnore]
        public Action<DataTable> PostTransform { get; set; }

        public ulong RangeFreeMemory { get; set; } = 500_000_000;

        #region Classes

        public ResultSettings Results = new ResultSettings();

        public class ResultSettings
        {
            #region Properties

            /// <summary>
            /// Only build trees that contain positive output matches
            /// </summary>
            public bool MatchOnly { get; set; } = false;

            /// <summary>
            /// Anything with a variance off the base rate below this amount will not be considered a predictor and will be left off the tree
            /// </summary>
            public float MinimumAccuracy { get; set; } = .2f;

            /// <summary>
            /// The minimum total times a route must be matched to be considered
            /// </summary>
            public int MinimumHits { get; set; } = 5;

            #endregion Properties
        }

        #endregion Classes
    }
}
using Newtonsoft.Json;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;

namespace Penguin.Analysis
{
    public class DataSourceSettings
    {
        /// <summary>
        /// Action to be called when the engine removes a route based on contraint checking
        /// </summary>
        [JsonIgnore]
        public Action<IEnumerable<string>, ValidationResult> CheckedConstraint = null;

        /// <summary>
        /// Action to be called when the engine approves a route based on contraint checking
        /// </summary>
        [JsonIgnore]
        public Action<IEnumerable<string>, LongByte> NoCheckedConstraint = null;

        /// <summary>
        /// Action to be called when a node is removed due to fewer matches than configured to allow
        /// </summary>
        [JsonIgnore]
        public Action<INode> TrimmedNode = null;

        public ulong MinFreeMemory { get; set; } = 1_000_000_000;

        [JsonIgnore]
        public Action<NodeSetGraphProgress> NodeEnumProgress { get; set; }
        public int NodeFlushDepth { get; set; } = 0;

        /// <summary>
        /// Executed after the graph is generated and the node count is enumerated
        /// </summary>
        [JsonIgnore]
        public Action<NodeSetGraph> PostGraphCalculation { get; set; }

        [JsonIgnore]
        public Action<DataTable> PostTransform { get; set; }

        public ulong RangeFreeMemory { get; set; } = 500_000_000;
        public int PreloadChunkSize { get; set; } = 15000;
        public int PreloadTimeoutMs { get; set; } = 5000;
        public int MaxCacheCount { get; set; } = 1_000_000;
        public bool CacheNodes { get; set; } = true;

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
            public float MinumumScore { get; set; } = .1f;

            /// <summary>
            /// The minimum total times a route must be matched to be considered
            /// </summary>
            public int MinimumHits { get; set; } = 5;

            #endregion Properties
        }

        #endregion Classes
    }
}
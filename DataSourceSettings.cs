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
        public int NodeFlushDepth { get; set; } = 0;

        [JsonIgnore]
        public Action<DataTable> PostTransform { get; set; }

        /// <summary>
        /// Executed after the graph is generated and the node count is enumerated
        /// </summary>
        [JsonIgnore]
        public Action<NodeSetGraph> PostGraphCalculation { get; set; }

        public ulong RangeFreeMemory { get; set; } = 500_000_000;
        public Action<NodeSetGraphProgress> NodeEnumProgress { get; set; }

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
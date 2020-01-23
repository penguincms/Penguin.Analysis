using System;

namespace Penguin.Analysis
{
    [Flags]
    public enum MatchResult : sbyte
    {
        /// <summary>
        /// The record matches neither the nodes path, nor is a positive indicator of outcome
        /// </summary>
        None = 0,

        /// <summary>
        /// The record matches the node path, but is not a positive indicator of outcome
        /// </summary>
        Route = 1,

        /// <summary>
        /// The record is a positive indicator of outcome, but does not match the node path
        /// </summary>
        Output = 2,

        /// <summary>
        /// The record matches the node path, and is a positive indicator of outcome
        /// </summary>
        Both = 3
    }
}
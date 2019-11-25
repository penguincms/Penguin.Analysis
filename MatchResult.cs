using System;

namespace Penguin.Analysis
{
    [Flags]
    public enum MatchResult : sbyte
    {
        None = 0,
        Route = 1,
        Output = 2,
        Both = 3
    }
}
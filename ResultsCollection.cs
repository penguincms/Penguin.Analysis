using System;
using System.Collections.Generic;
using System.Text;

namespace Penguin.Analysis
{
    public interface IResultsCollection
    {
        ref int this[MatchResult resultKind] { get; }
    }
}

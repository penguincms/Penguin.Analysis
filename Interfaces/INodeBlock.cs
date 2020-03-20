using System;
using System.Collections.Generic;
using System.Text;

namespace Penguin.Analysis.Interfaces
{
    public interface INodeBlock
    {
        long Offset { get; }
        long NextOffset { get; }
    }
}

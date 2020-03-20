using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Penguin.Analysis
{
    public class NodeBlock : INodeBlock
    {
        public long Offset { get; internal set; }
        public long NextOffset { get; internal set; }
        public int Index { get; set; }
    }
}

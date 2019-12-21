using System.Collections.Generic;

namespace Penguin.Analysis.Interfaces
{
    public interface INode<TChild> : INode where TChild : INode
    {

        new IEnumerable<TChild> Next { get; set; }
        new TChild ParentNode { get; set; }

    }

    public interface INode
    {
        int[] Results { get; set; }
        float Score { get; }
        int Value { get; set; }
        float Accuracy { get; }
        int Depth { get; }
        sbyte Header { get; set; }
        bool LastNode { get; set; }
        int Matched { get; }
        IList<TypelessDataRow> MatchingRows { get; set; }
        string ToString();

        IEnumerable<INode> Next { get; }

        INode ParentNode { get; }
    }
}
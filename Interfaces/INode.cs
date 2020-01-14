using System.Collections.Generic;

namespace Penguin.Analysis.Interfaces
{
    public interface INode<TChild> : INode where TChild : INode
    {
        new IEnumerable<TChild> Next { get; }
        new TChild ParentNode { get; }
        new TChild GetNextByValue(int Value);
    }

    public interface INode
    {
        bool Evaluate(Evaluation e);
        void Preload(int depth);
        void Flush(int depth);
        int[] Results { get; }
        float GetScore(float BaseRate);
        int Value { get; }
        float Accuracy { get; }
        byte Depth { get; }
        sbyte Header { get; }
        bool LastNode { get; }
        int Matched { get; }
        int Key { get; }
        string ToString();
        int ChildCount { get; }
        IEnumerable<INode> Next { get; }
        INode ParentNode { get; }
        INode GetNextByValue(int Value);
        sbyte ChildHeader { get; }
    }
}
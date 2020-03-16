using System;
using System.Collections.Generic;

namespace Penguin.Analysis.Interfaces
{
    public interface INode<TChild> : INode where TChild : INode
    {
        new IEnumerable<TChild> Next { get; }
        new TChild ParentNode { get; }

        new TChild GetNextByValue(int Value);
    }

    public interface INode : IDisposable
    {
        Accuracy Accuracy { get; }
        int ChildCount { get; }
        sbyte ChildHeader { get; }
        byte Depth { get; }
        sbyte Header { get; }
        long Key { get; }
        bool LastNode { get; }
        int Matched { get; }
        IEnumerable<INode> Next { get; }
        INode ParentNode { get; }

        int[] Results { get; }

        int Value { get; }

        int this[MatchResult result] { get; set; }

        void Evaluate(Evaluation e, bool MultiThread = true);

        bool Evaluate(TypelessDataRow row);

        void Flush(int depth);

        double GetScore(float BaseRate);

        INode NextAt(int index);

        void Preload(int depth);

        string ToString();
    }
}
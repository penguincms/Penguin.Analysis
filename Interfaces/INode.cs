using System;
using System.Collections.Generic;

namespace Penguin.Analysis.Interfaces
{
    public interface INode<TChild> : INode where TChild : INode
    {
        new IEnumerable<TChild> Next { get; }
        new TChild ParentNode { get; }

        TChild GetNextByValue(int Value);
    }

    public interface INode : IDisposable
    {
        Accuracy Accuracy { get; }
        int ChildCount { get; }
        sbyte ChildHeader { get; }
        byte Depth { get; }
        sbyte Header { get; }
        long Key { get; }
        IEnumerable<INode> Next { get; }
        INode ParentNode { get; }

        ushort[] Results { get; }

        ushort Value { get; }

        ushort this[MatchResult result] { get; set; }

        void Evaluate(Evaluation e, long routeKey, bool MultiThread = true);

        bool Evaluate(TypelessDataRow row);

        void Flush(int depth);

        double GetScore(float BaseRate);

        INode NextAt(int index);

        void Preload(int depth);

        string ToString();
    }
}
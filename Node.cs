using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Penguin.Analysis
{
    public abstract class Node : INode
    {
        public virtual ushort this[MatchResult result]
        {
            get => Results[(int)result];
            set => Results[(int)result] = value;
        }

        protected bool disposedValue;

        private byte? depth;
        public virtual Accuracy Accuracy => new(this[MatchResult.Route] + this[MatchResult.Both], this[MatchResult.Both]);

        public abstract int ChildCount { get; }

        public abstract sbyte ChildHeader { get; }

        public virtual byte Depth
        {
            get
            {
                depth ??= GetDepth();
                return depth.Value;
            }
        }

        public abstract sbyte Header { get; }

        public abstract long Key { get; }

        public abstract IEnumerable<INode> Next { get; }

        public abstract INode ParentNode { get; }

        public virtual ushort[] Results { get; } = new ushort[4];

        public abstract ushort Value { get; }

        // To detect redundant calls
        public virtual void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        public virtual void Evaluate(Evaluation e, long routeKey, bool MultiThread = true)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (Header == -1)
            {
                foreach (INode child in Next)
                {
                    if (child.Evaluate(e.DataRow))
                    {
                        child.Evaluate(e, routeKey, MultiThread);
                    }
                }
            }
            else
            {
                routeKey |= 1 << Header;

                e.MatchRoute(this, routeKey);

                if (ChildCount > 0 && ChildHeader >= 0)
                {
                    INode Next = NextAt(e.DataRow[ChildHeader]);

                    Next?.Evaluate(e, routeKey);
                }
            }
        }

        public virtual bool Evaluate(TypelessDataRow row)
        {
            return row is null ? throw new ArgumentNullException(nameof(row)) : row.Equals(Header, Value);
        }

        public virtual void Flush(int depth)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetDepth()
        {
            INode toCheck = this;
            byte depth = 0;

            while (toCheck != null && toCheck.Header != -1)
            {
                depth++;

                toCheck = toCheck.ParentNode;
            }

            return depth;
        }

        public long GetKey()
        {
            long Key = 0;

            INode n = this;

            foreach (INode tn in GetTree(n).Where(tnn => tnn.Header != -1))
            {
                Key |= (long)1 << tn.Header;
            }

            return Key;
        }

        public virtual double GetScore(float BaseRate)
        {
            double accuracy = Accuracy.Next;

            //This is pivoted around the base rate instead of 50% because a value
            //that has an accuracy matching the base rate has 0 effect on the rate,
            //and is therefor 0 in terms of likelyhood. Stop changing this because
            //you forgot how it works.
            double toReturn = accuracy > BaseRate ? (accuracy - BaseRate) / (1 - BaseRate) : (accuracy / BaseRate) - 1;

            return toReturn;
        }

        public abstract INode NextAt(int index);

        public virtual void Preload(int depth)
        {
        }

        protected abstract void Dispose(bool disposing);

        private static IEnumerable<INode> GetTree(INode np)
        {
            INode n = np;
            while (n != null)
            {
                yield return n;

                if (n is DiskNode dn && dn.ParentOffset == DiskNode.HEADER_BYTES)
                {
                    yield break;
                }

                n = n.ParentNode;
            }
        }
    }
}
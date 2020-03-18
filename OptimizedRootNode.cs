using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Penguin.Analysis
{
    public class OptimizedRootNode : Node
    {
        private List<INode>[][] next;

        public override int ChildCount { get; }

        public override sbyte ChildHeader => -1;

        public override sbyte Header { get; } = -1;

        public override long Key { get; }

        public override IEnumerable<INode> Next
        {
            get
            {
                for (int header = 0; header < next.Length; header++)
                {
                    for (int value = 0; value < next[header].Length; value++)
                    {
                        foreach (INode n in next[header][value])
                        {
                            yield return n;
                        }
                    }
                }
            }
        }

        public override INode ParentNode { get; }

        public override ushort[] Results { get; } = new ushort[4];

        public override ushort Value { get; } = 0;

        public OptimizedRootNode(INode source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            int MaxHeader = 0;

            IEnumerable<INode> Parents = source.Next.Where(n => !(n is null));
            IEnumerable<INode> Children = Parents.SelectMany(n => n.Next).Where(n => !(n is null));

            foreach (INode n in Parents)
            {
                MaxHeader = Math.Max(MaxHeader, n.ChildHeader);
            }

            int[] MaxValues = new int[MaxHeader + 1];
            next = new List<INode>[MaxValues.Length][];

            foreach (INode n in Parents)
            {
                MaxValues[n.ChildHeader] = Math.Max(MaxValues[n.ChildHeader], n.ChildCount);
            }

            for (int header = 0; header <= MaxHeader; header++)
            {
                next[header] = new List<INode>[MaxValues[header]];

                for (int value = 0; value < MaxValues[header]; value++)
                {
                    next[header][value] = new List<INode>();
                }
            }

            foreach (INode c in Children)
            {
                next[c.Header][c.Value].Add(c);
            }
        }

        public void Evaluate(Evaluation e, bool MultiThread = true) => this.Evaluate(e, 0, MultiThread);

        public override void Evaluate(Evaluation e, long routeKey, bool MultiThread = true)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            IEnumerable<INode> GetChildren()
            {
                for (int header = 0; header < next.Length; header++)
                {
                    int value = e.DataRow[header];

                    if (next[header].Length > value)
                    {
                        foreach (INode n in next[header][value])
                        {
                            yield return n;
                        }
                    }
                }
            }

            if (MultiThread)
            {
                Parallel.ForEach(GetChildren(), (n) =>
                {
                    n.Evaluate(e, 0, MultiThread);
                });
            }
            else
            {
                foreach (INode n in GetChildren())
                {
                    n.Evaluate(e, 0, MultiThread);
                }
            }
        }

        #region IDisposable Support

        public override INode NextAt(int index) => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    foreach (INode n in this.Next)
                    {
                        try
                        {
                            n.Dispose();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    this.next = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~OptimizedRootNode()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support
    }
}
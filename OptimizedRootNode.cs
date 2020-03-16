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
        public List<int> HeaderBreaks = new List<int>() { 0 };

        public Dictionary<int, int> ValueJumpList = new Dictionary<int, int>();

        private readonly List<INode> next = new List<INode>();

        public override int ChildCount { get; }

        public override sbyte ChildHeader => -1;

        public override sbyte Header { get; } = -1;

        public override long Key { get; }

        public override bool LastNode { get; } = false;

        public override int Matched { get; } = 0;

        public override IEnumerable<INode> Next => this.next;

        public override INode ParentNode { get; }

        public override int[] Results { get; } = new int[4];

        public override int Value { get; } = 0;

        public OptimizedRootNode(INode source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            foreach (INode n in source.Next)
            {
                if (n?.Next != null)
                {
                    foreach (INode c in n.Next)
                    {
                        if (c != null)
                        {
                            this.next.Add(c);
                        }
                    }
                }
            }

            this.next = this.next.OrderBy(n => n.Header).ThenByDescending(n => n.Matched).ToList();

            sbyte lastHeader = this.Next.First().Header;
            int lastValue = this.Next.First().Value;

            this.ChildCount = this.next.Count;

            int lastValueIndex = 0;

            for (int i = 0; i < this.ChildCount; i++)
            {
                INode thisNode = this.Next.ElementAt(i);

                if (thisNode.Header != lastHeader || thisNode.Value != lastValue)
                {
                    if (thisNode.Header != lastHeader)
                    {
                        this.HeaderBreaks.Add(i);
                    }

                    lastHeader = thisNode.Header;
                    lastValue = thisNode.Value;

                    this.ValueJumpList.Add(lastValueIndex, i);
                    lastValueIndex = i;
                }
            }

            this.ValueJumpList.Add(lastValueIndex, this.ChildCount);
            this.HeaderBreaks.Add(this.ChildCount);
        }

        public void Evaluate(int headerBreak, Evaluation e, bool MultiThread = true)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            bool Matched = false;

            if (headerBreak == this.HeaderBreaks.Last())
            {
                return;
            }

            int i = headerBreak;

            int stop = this.HeaderBreaks.Where(h => h > headerBreak).Min();
            do
            {
                if (this.next.Count <= i)
                {
                    Debug.WriteLine($"Skipped to far while evaluating root. List ends at {this.next.Count} and we ended up at {i}");
                    return;
                }

                if (!this.next.ElementAt(i).Evaluate(e.DataRow))
                {
                    if (Matched)
                    {
                        Matched = false;
                        return;
                    }
                    else
                    {
                        i = this.ValueJumpList[i];
                    }
                    continue;
                }
                else
                {
                    this.next.ElementAt(i).Evaluate(e, MultiThread);
                    Matched = true;
                }

                if (++i >= stop)
                {
                    return;
                };
            } while (true);
        }

        public bool Evaluate(Evaluation e, bool MultiThread = true)
        {
            if (MultiThread)
            {
                Parallel.ForEach(this.HeaderBreaks, (headerBreak) =>
                {
                    Evaluate(headerBreak, e);
                });
            }
            else
            {
                foreach (int headerBreak in this.HeaderBreaks)
                {
                    Evaluate(headerBreak, e);
                }
            }

            return true;
        }

        #region IDisposable Support

        public override INode NextAt(int index) => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    foreach (INode n in this.next)
                    {
                        try
                        {
                            n.Dispose();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    this.next.Clear();

                    this.HeaderBreaks.Clear();
                    this.ValueJumpList.Clear();
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
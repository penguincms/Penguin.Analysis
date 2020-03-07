using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Penguin.Analysis
{
    public class OptimizedRootNode : INode<INode>
    {
        public List<int> HeaderBreaks = new List<int>() { 0 };

        public Dictionary<int, int> ValueJumpList = new Dictionary<int, int>();

        private List<INode> next = new List<INode>();
        public int this[MatchResult result]
        {
            get => this.Results[(int)result];
            set => this.Results[(int)result] = value;
        }
        public float Accuracy { get; }

        public int ChildCount { get; internal set; }

        public sbyte ChildHeader => -1;

        public byte Depth { get; }

        public sbyte Header { get; } = -1;

        public long Key { get; }

        public bool LastNode { get; } = false;

        public int Matched { get; } = 0;

        public IEnumerable<INode> Next => this.next;

        public INode ParentNode { get; }

        public int[] Results { get; } = new int[4];

        public int Value { get; } = 0;

        public OptimizedRootNode(INode source)
        {
            foreach (INode n in source.Next)
            {
                foreach (INode c in n.Next)
                {
                    this.next.Add(c);
                }
            }

            this.next = this.next.OrderBy(n => n.Header).ThenByDescending(n => n.GetMatched()).ToList();

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

                if (!this.next.ElementAt(i).Evaluate(e, MultiThread))
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

        public void Flush(int depth)
        {
        }

        public INode GetNextByValue(int Value)
        {
            throw new NotImplementedException();
        }

        public double GetScore(float BaseRate)
        {
            return 0;
        }

        public void Preload(int depth)
        {
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
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
using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Penguin.Analysis
{
    public class OptimizedRootNode : INode<INode>
    {
        public bool Evaluate(Evaluation e)
        {
            

            Parallel.ForEach(HeaderBreaks, (headerBreak) =>
            {
                bool Matched = false;
                
                if(headerBreak == HeaderBreaks.Last())
                {
                    return;
                }

                int i = headerBreak;

                int stop = HeaderBreaks.Where(h => h > headerBreak).Min();
                do
                {
                    if (!next.ElementAt(i).Evaluate(e))
                    {
                        if (Matched)
                        {
                            Matched = false;
                            return;
                        }
                        else
                        {
                            i = ValueJumpList[i];
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
            });

            //for (int i = 0; i < ChildCount; )
            //{
            //    if (!next.ElementAt(i).Evaluate(e))
            //    {
            //        if (Matched)
            //        {
            //            Matched = false;
            //            i = HeaderBreaks.Where(h => h > i).Min();
            //        }
            //        else
            //        {
            //            i = ValueJumpList[i];
            //        }
            //        continue;
            //    } else
            //    {
            //        Matched = true;
            //    }

            //    i++;
            //}

            return true;
        }

        public OptimizedRootNode(INode source)
        {
            foreach (INode n in source.Next)
            {
                foreach (INode c in n.Next)
                {
                    next.Add(c);
                }
            }

            next = next.OrderBy(n => n.Header).ThenByDescending(n => n.GetMatched()).ToList();


            sbyte lastHeader = this.Next.First().Header;
            int lastValue = this.Next.First().Value;

            ChildCount = next.Count;

            int lastValueIndex = 0;

          

            for(int i = 0; i < ChildCount; i++)
            {
                INode thisNode = Next.ElementAt(i);

                if(thisNode.Header != lastHeader || thisNode.Value != lastValue)
                {
                    if(thisNode.Header != lastHeader)
                    {
                        HeaderBreaks.Add(i);
                    }

                    lastHeader = thisNode.Header;
                    lastValue = thisNode.Value;

                    ValueJumpList.Add(lastValueIndex, i);
                    lastValueIndex = i;
                }
            }

            ValueJumpList.Add(lastValueIndex, ChildCount);
            HeaderBreaks.Add(ChildCount);
        }

        private List<INode> next = new List<INode>();

        public Dictionary<int, int> ValueJumpList = new Dictionary<int, int>();
        
        public List<int> HeaderBreaks = new List<int>() { 0 };

        public IEnumerable<INode> Next => this.next;
        public INode ParentNode { get; }
        public int[] Results { get; } = new int[4];
        public float GetScore(float BaseRate)
        {
            return 0;
        }

        public int Value { get; } = 0;
        public float Accuracy { get; }
        public byte Depth { get; }
        public sbyte Header { get; } = -1;
        public bool LastNode { get; } = false;
        public int Matched { get; } = 0;
        public int Key { get; }
        public int ChildCount { get; internal set; }
        public sbyte ChildHeader => -1;

        public void Flush(int depth)
        {
            
        }

        public void Preload(int depth)
        {
            
        }

        public INode GetNextByValue(int Value)
        {
            throw new NotImplementedException();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach(INode n in this.next)
                    {
                        try
                        {
                            n.Dispose();
                        } catch(Exception)
                        {

                        }
                    }

                    next.Clear();

                    this.HeaderBreaks.Clear();
                    this.ValueJumpList.Clear();

                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~OptimizedRootNode()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}

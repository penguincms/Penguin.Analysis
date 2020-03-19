using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Penguin.Analysis
{
    public class OptimizedRootNode : Node
    {
        private List<DiskNode>[][] next;

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
            next = new List<DiskNode>[MaxValues.Length][];

            foreach (INode n in Parents)
            {
                MaxValues[n.ChildHeader] = Math.Max(MaxValues[n.ChildHeader], n.ChildCount);
            }

            for (int header = 0; header <= MaxHeader; header++)
            {
                next[header] = new List<DiskNode>[MaxValues[header]];

                for (int value = 0; value < MaxValues[header]; value++)
                {
                    next[header][value] = new List<DiskNode>();
                }
            }

            foreach (DiskNode c in Children)
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

            string FilePath = DiskNode._backingStream.FilePath;

            IEnumerable<DiskNode> GetChildren()
            {
                for (int header = 0; header < next.Length; header++)
                {
                    int value = e.DataRow[header];

                    if (next[header].Length > value)
                    {
                        foreach (DiskNode n in next[header][value])
                        {
                            yield return n;
                        }
                    }
                }
            }

            void Evaluate(DiskNode n)
            {
                using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] backingData = new byte[n.NextOffset - n.Offset];

                    fs.Seek(n.Offset, SeekOrigin.Begin);

                    fs.Read(backingData, 0, backingData.Length);

                    using (DiskNode nRoot = new DiskNode(backingData, 0, n.Offset))
                    {
                        nRoot.Evaluate(e, 0, MultiThread);
                    }
                }
            }

            if (MultiThread)
            {
                Parallel.ForEach(GetChildren(), Evaluate);
            }
            else
            {
                foreach (DiskNode n in GetChildren())
                {
                    Evaluate(n);
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
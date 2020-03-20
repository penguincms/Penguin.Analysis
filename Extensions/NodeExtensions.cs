using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Penguin.Analysis.Extensions
{
    public static class NodeExtensions
    {
        #region Methods




        public static int Depth(this MemoryNode tnode)
        {
            int Depth = 0;

            MemoryNode n = tnode;
            List<MemoryNode> tree = new List<MemoryNode>();

            while (n != null)
            {
                tree.Add(n);
                n = n.parentNode;
            }

            foreach (MemoryNode tn in tree.Where(tnn => tnn.Header != -1))
            {
                Depth++;
            }

            return Depth;
        }

        public static IEnumerable<T> FullTree<T>(this T tn) where T : INode
        {
            if (tn is null)
            {
                throw new ArgumentNullException(nameof(tn));
            }

            Queue<T> Nodes = new Queue<T>();

            Nodes.Enqueue(tn);

            while (Nodes.Any())
            {
                T thisNode = Nodes.Dequeue();

                if (thisNode.Header != -1)
                {
                    yield return thisNode;
                }

                if (thisNode.Next != null)
                {
                    foreach (T n in thisNode.Next)
                    {
                        if (n != null)
                        {
                            Nodes.Enqueue(n);
                        }
                    }
                }
            }
        }

        #endregion Methods
    }
}
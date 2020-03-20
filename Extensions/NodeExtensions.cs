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


        public static void TrimNodesWithNoBearing(this MemoryNode target, DataSourceBuilder sourceBuilder)
        {
            if (sourceBuilder is null)
            {
                throw new ArgumentNullException(nameof(sourceBuilder));
            }

            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            

            if (target.next != null && target.next.Any())
            {
                int lastGoodNode = 0;

                for(int i = target.next.Length; i > 0; i--)
                {
                    if(target.next[i - 1] != null)
                    {
                        lastGoodNode = i;
                        break;
                    }
                }

                if(lastGoodNode != target.next.Length)
                {
                    target.next = target.next.Take(lastGoodNode).ToArray();
                }

                foreach (MemoryNode next in target.next)
                {
                    if (next != null)
                    {
                        next.TrimNodesWithNoBearing(sourceBuilder);
                    }
                }
            }

            if (target.Header != -1 && (target.next is null || !target.next.Where(n => n != null).Any()))
            {
                if (target.GetScore(sourceBuilder.Result.BaseRate) < sourceBuilder.Settings.Results.MinumumScore && target.ParentNode != null)
                {
                    sourceBuilder.Settings.TrimmedNode?.Invoke(target);

                    target.parentNode.RemoveNode(target);
                }
#if DEBUG
                else
                {
                }
#endif
                target.LastNode = true;
            }
        }



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

        public static void Trim(this MemoryNode tnode)
        {
            if (tnode is null)
            {
                throw new ArgumentNullException(nameof(tnode));
            }

            tnode.CheckValidity();

            tnode.MatchingRows = null;
        }

        #endregion Methods
    }
}
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

        public static void FillNodeData(this MemoryNode n, float PositiveIndicators, int RawRowCount)
        {
            if (n is null)
            {
                throw new ArgumentNullException(nameof(n));
            }

            if (n.Header != -1)
            {
                float MissingMatches = PositiveIndicators - n[MatchResult.Both];

                float MissingMisses = RawRowCount - (MissingMatches + n[MatchResult.Route] + n[MatchResult.Both]);

                n[MatchResult.None] = (int)MissingMisses;
                n[MatchResult.Output] = (int)MissingMatches;
            }

            if (n.Next != null)
            {
                foreach (MemoryNode nChild in n.next)
                {
                    nChild?.FillNodeData(PositiveIndicators, RawRowCount);
                }
            }
        }

        public static void TrimNodesWithNoBearing(this MemoryNode target, DataSourceBuilder sourceBuilder)
        {
            if (sourceBuilder is null)
            {
                throw new ArgumentNullException(nameof(sourceBuilder));
            }

            float MinimumAccuracy = sourceBuilder.Settings.Results.MinimumAccuracy;
            float BaseRate = sourceBuilder.Result.BaseRate;

            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (target.next != null)
            {
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
                if ((target.Accuracy.Next > BaseRate - (BaseRate * MinimumAccuracy) && target.Accuracy.Next < ((1 - BaseRate) * MinimumAccuracy) + BaseRate) && target.ParentNode != null)
                {
                    sourceBuilder.Settings.TrimmedNode?.Invoke(target);

                    target.parentNode.RemoveNode(target);
                }
#if DEBUG
                else
                {
                }
#endif
                target.lastNode = true;
            }
        }

        internal static SerializationResults Serialize(this MemoryNode tnode, INodeFileStream lockedNodeFileStream, long ParentOffset = 0)
        {
            SerializationResults results = new SerializationResults(lockedNodeFileStream, tnode, ParentOffset);
            long thisOffset = results.Single().Offset;
            //parent 0 - 8

            byte[] toWrite = new byte[DiskNode.NODE_SIZE];

            BitConverter.GetBytes(ParentOffset).CopyTo(toWrite, 0);

            for (int i = 0; i < 4; i++)
            {
                BitConverter.GetBytes(tnode.Results[i]).CopyTo(toWrite, 8 + i * 4);
            }

            unchecked
            {
                toWrite[24] = (byte)tnode.Header;
            }

            BitConverter.GetBytes(tnode.Value).CopyTo(toWrite, 25);

            toWrite[29] = tnode.LastNode ? (byte)1 : (byte)0;

            toWrite[30] = (byte)tnode.ChildHeader;

            int nCount = tnode.ChildCount;

            BitConverter.GetBytes(nCount).CopyTo(toWrite, toWrite.Length - 4);

            lockedNodeFileStream.Write(toWrite);

            if (nCount > 0)
            {
                int skip = (nCount * DiskNode.NEXT_SIZE);

                long ChildListOffset = lockedNodeFileStream.Offset;

                byte[] skipBytes = new byte[skip];

                lockedNodeFileStream.Write(skipBytes);

                byte[] nextOffsets = new byte[nCount * DiskNode.NEXT_SIZE];

                int i;

                for (i = 0; i < nCount; i++)
                {
                    MemoryNode next = tnode.next[i];

                    long offset = 0;
                    int value = -1;

                    if (next != null)
                    {
                        offset = lockedNodeFileStream.Offset;
                        value = next.Value;
                    }
                    BitConverter.GetBytes(offset).CopyTo(nextOffsets, i * DiskNode.NEXT_SIZE);

                    BitConverter.GetBytes(value).CopyTo(nextOffsets, i * DiskNode.NEXT_SIZE + 8);

                    if (next != null)
                    {
                        results.AddRange(next.Serialize(lockedNodeFileStream, thisOffset));
                    }
                }

                long lastOffset = lockedNodeFileStream.Offset;

                lockedNodeFileStream.Seek(ChildListOffset);

                lockedNodeFileStream.Write(nextOffsets);

                lockedNodeFileStream.Seek(lastOffset);
            }

            return results;
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
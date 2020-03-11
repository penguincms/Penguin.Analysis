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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateScore(this INode tnode, float BaseRate)
        {
            if (tnode is null)
            {
                throw new ArgumentNullException(nameof(tnode));
            }

            double Accuracy = tnode.GetAccuracy().Next;

            //This is pivoted around the base rate instead of 50% because a value
            //that has an accuracy matching the base rate has 0 effect on the rate,
            //and is therefor 0 in terms of likelyhood. Stop changing this because
            //you forgot how it works.
            double toReturn = Accuracy > BaseRate ? (Accuracy - BaseRate) / (1 - BaseRate) : (Accuracy / BaseRate) - 1;

            return toReturn;
        }

        internal static bool StandardEvaluate(this INode node, Evaluation e)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (node.Header == -1)
            {
                foreach (INode child in node.Next)
                {
                    if (child.Evaluate(e) && child.Header != -1)
                    {
                        break;
                    }
                }

                return true;
            }

            bool MatchesRoute = e.DataRow.Equals(node.Header, node.Value);

            if (MatchesRoute)
            {
                if (node.ChildCount > 0)
                {
                    INode Next = node.GetNextByValue(e.DataRow[node.ChildHeader]);

                    if (Next != null)
                    {
                        Next.Evaluate(e);
                    }
                }

                e.MatchRoute(node);

                return true;
            }
            else
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMatched(this INode tnode)
        {
            if (tnode is null)
            {
                throw new ArgumentNullException(nameof(tnode));
            }

            return tnode[MatchResult.Route] + tnode[MatchResult.Both];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Accuracy GetAccuracy(this INode tnode)
        {
            if (tnode is null)
            {
                throw new ArgumentNullException(nameof(tnode));
            }

            return new Accuracy(tnode[MatchResult.Route] + tnode[MatchResult.Both], tnode[MatchResult.Both]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetDepth(this INode tnode)
        {
            INode toCheck = tnode;
            byte depth = 0;

            while (toCheck != null && toCheck.Header != -1)
            {
                depth++;

                toCheck = toCheck.ParentNode;
            }

            return depth;
        }

        public static void FillNodeData(this Node n, float PositiveIndicators, int RawRowCount)
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
                foreach (Node nChild in n.Next)
                {
                    nChild.FillNodeData(PositiveIndicators, RawRowCount);
                }
            }
        }

        public static void TrimNodesWithNoBearing(this Node target, DataSourceBuilder sourceBuilder)
        {
            float MinimumAccuracy = sourceBuilder.Settings.Results.MinimumAccuracy;
            float BaseRate = sourceBuilder.Result.BaseRate;

            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (target.Next != null)
            {
                foreach (Node next in target.Next)
                {
                    next.TrimNodesWithNoBearing(sourceBuilder);
                }
            }

            if (target.Header != -1 && (target.Next is null || !target.Next.Where(n => n != null).Any()))
            {
                if ((target.Accuracy.Next > BaseRate - (BaseRate * MinimumAccuracy) && target.Accuracy.Next < ((1 - BaseRate) * MinimumAccuracy) + BaseRate) && target.ParentNode != null)
                {
                    sourceBuilder.Settings.TrimmedNode?.Invoke(target);

                    target.ParentNode.RemoveNode(target);
                }
#if DEBUG
                else
                {
                }
#endif
                target.LastNode = true;
            }
        }

        public static long GetLength(this Node tnode)
        {
            if (tnode is null)
            {
                throw new ArgumentNullException(nameof(tnode));
            }

            long length = DiskNode.NODE_SIZE;

            if (!(tnode.Next is null))
            {
                foreach (Node cnode in tnode.Next)
                {
                    length += DiskNode.NEXT_SIZE;
                    length += cnode.GetLength();
                }
            }

            return length;
        }

        internal static SerializationResults Serialize(this Node tnode, INodeFileStream lockedNodeFileStream, long ParentOffset = 0)
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
                List<Node> orderedList = tnode.Next.OrderBy(tn => tn.Header).ThenByDescending(tn => tn.GetMatched()).ToList();

                int skip = (nCount * DiskNode.NEXT_SIZE);

                long ChildListOffset = lockedNodeFileStream.Offset;

                byte[] skipBytes = new byte[skip];

                lockedNodeFileStream.Write(skipBytes);

                byte[] nextOffsets = new byte[nCount * DiskNode.NEXT_SIZE];

                int i;

                for (i = 0; i < nCount; i++)
                {
                    BitConverter.GetBytes(lockedNodeFileStream.Offset).CopyTo(nextOffsets, i * DiskNode.NEXT_SIZE);
                    BitConverter.GetBytes(orderedList.ElementAt(i).Value).CopyTo(nextOffsets, i * DiskNode.NEXT_SIZE + 8);

                    results.AddRange(orderedList.ElementAt(i).Serialize(lockedNodeFileStream, thisOffset));
                }

                long lastOffset = lockedNodeFileStream.Offset;

                lockedNodeFileStream.Seek(ChildListOffset);

                lockedNodeFileStream.Write(nextOffsets);

                lockedNodeFileStream.Seek(lastOffset);
            }

            return results;
        }

        public static bool AddNext(this Node tnode, DataSourceBuilder sourceBuilder, Node next, int i, bool trim = true)
        {
            if (tnode is null)
            {
                throw new ArgumentNullException(nameof(tnode));
            }

            if (next is null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (trim)
            {
                foreach (TypelessDataRow row in tnode.MatchingRows)
                {
                    if (next.Evaluate(row))
                    {
                    }
                }

                int hits = next.Matched;

                if (sourceBuilder.Settings.Results.MatchOnly && next[MatchResult.Both] == 0)
                {
                    hits = 0;
                }

                if (hits >= sourceBuilder.Settings.Results.MinimumHits)
                {
                    tnode.Next[i] = next;
                    next.ParentNode = tnode;
                    return true;
                }
            }
            else
            {
                tnode.Next[i] = next;
                next.ParentNode = tnode;
                return true;
            }

            return false;
        }

        public static void CheckValidity(this Node tnode)
        {
            if (tnode is null)
            {
                throw new ArgumentNullException(nameof(tnode));
            }

            if (!tnode.LastNode && !tnode.Next.Where(n => n != null).Any())
            {
                tnode.ParentNode?.RemoveNode(tnode);
            }
        }

        public static int Depth(this Node tnode)
        {
            int Depth = 0;

            Node n = tnode;
            List<Node> tree = new List<Node>();

            while (n != null)
            {
                tree.Add(n);
                n = n.ParentNode;
            }

            foreach (Node tn in tree.Where(tnn => tnn.Header != -1))
            {
                Depth++;
            }

            return Depth;
        }

        public static bool Evaluate(this Node tnode, TypelessDataRow dataRow)
        {
            if (tnode is null)
            {
                throw new ArgumentNullException(nameof(tnode));
            }

            if (dataRow is null)
            {
                throw new ArgumentNullException(nameof(dataRow));
            }

            if (tnode.Header == -1)
            {
                tnode.MatchingRows.Add(dataRow);
                return true;
            }

            MatchResult pool = MatchResult.None;

            bool MatchesRoute = dataRow.Equals(tnode.Header, tnode.Value);

            if (dataRow.MatchesOutput)
            {
                pool |= MatchResult.Output;
            }

            if (MatchesRoute)
            {
                pool |= MatchResult.Route;

                tnode.MatchingRows.Add(dataRow);
            }

            if (pool != MatchResult.None)
            {
                tnode[MatchResult.None]--;
                tnode[pool]++;
            }

            return MatchesRoute;
        }

        public static IEnumerable<INode> FullTree(this INode tnode)
        {
            if (tnode is null)
            {
                throw new ArgumentNullException(nameof(tnode));
            }

            if (tnode.Header != -1)
            {
                yield return tnode;
            }

            if (tnode.Next != null)
            {
                foreach (INode child in tnode.Next)
                {
                    if (child is null)
                    {
                        continue;
                    }

                    foreach (INode n in child.FullTree())
                    {
                        yield return n;
                    }
                }
            }
        }

        public static long GetKey(this INode tnode)
        {
            long Key = 0;

            INode n = tnode;

            static IEnumerable<INode> GetTree(INode np)
            {
                INode n = np;
                while (n != null)
                {
                    yield return n;

                    n = n.ParentNode;
                }
            }

            foreach (INode tn in GetTree(n).Where(tnn => tnn.Header != -1))
            {
                Key |= ((long)1 << tn.Header);
            }

            return Key;
        }

        public static void RemoveNode(this Node tnode, Node n)
        {
            if (tnode is null)
            {
                throw new ArgumentNullException(nameof(tnode));
            }

            tnode.Next = tnode.Next.Where(ni => ni != n).ToArray();

            tnode.CheckValidity();
        }

        public static void Trim(this Node tnode)
        {
            if (tnode is null)
            {
                throw new ArgumentNullException(nameof(tnode));
            }

            if (tnode.Next != null)
            {
                tnode.Next = tnode.Next.Where(n => n != null).ToArray();
            }

            tnode.CheckValidity();

            tnode.MatchingRows = null;
        }

        #endregion Methods
    }
}
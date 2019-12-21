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
        public static float GetScore(this INode tnode)
        {
            int RB = (tnode.Results[(int)MatchResult.Route] + tnode.Results[(int)MatchResult.Both]);

            if(RB == 0)
            {
                return 0;
            }

            return (tnode.Results[(int)MatchResult.Both] + 1) / RB - (float)(tnode.Results[(int)MatchResult.Route] + 1) / RB;
        }

        public static int GetMatched(this INode tnode)
        {
            return tnode.Results[(int)MatchResult.Route] + tnode.Results[(int)MatchResult.Both];
        }

        public static float GetAccuracy(this INode tnode)
        {
            if (tnode.Results[(int)MatchResult.Route] == 0 && tnode.Results[(int)MatchResult.Both] > 0)
            {
                return 1;
            }

            float d = (tnode.Results[(int)MatchResult.Route] + tnode.Results[(int)MatchResult.Both]);

            return d == 0 ? 0 : tnode.Results[(int)MatchResult.Both] / d;
        }

        public static int GetDepth(this INode tnode)
        {
            INode toCheck = tnode;
            int depth = 0;

            while (toCheck != null && toCheck.Header != -1)
            {
                depth++;

                toCheck = toCheck.ParentNode;
            }

            return depth;
        }

        public static long Serialize(this Node tnode, LockedNodeFileStream lockedNodeFileStream, long ParentOffset = 0)
        {
            long thisNodeOffset = lockedNodeFileStream.Offset;

            //parent 0 - 8
            
            byte[] toWrite = new byte[DiskNode.NodeSize];

            BitConverter.GetBytes(ParentOffset).CopyTo(toWrite, 0);

            for(int i = 0; i < 4; i++)
            {
                BitConverter.GetBytes(tnode.Results[i]).CopyTo(toWrite, 8 + i * 4);
            }

            unchecked
            {
                toWrite[24] = (byte)tnode.Header;
            }

            BitConverter.GetBytes(tnode.Value).CopyTo(toWrite, 25);

            toWrite[29] = tnode.LastNode ? (byte)1 : (byte)0;

            lockedNodeFileStream.Write(toWrite);

            lockedNodeFileStream.Flush();

            if (!(tnode.Next is null))
            {
                long ChildrenStartOffset = lockedNodeFileStream.Offset + (tnode.Next.Length * 8) + 8;
                long ChildListOffset = lockedNodeFileStream.Offset;
                long CurrentChildOffset = ChildrenStartOffset;



                for (int i = 0; i < tnode.Next.Length; i++)
                {

                    lockedNodeFileStream.Seek(ChildListOffset + (i * 8));

                    lockedNodeFileStream.Write(CurrentChildOffset);

                    lockedNodeFileStream.Seek(CurrentChildOffset);

                    tnode.Next[i].Serialize(lockedNodeFileStream, thisNodeOffset);

                    CurrentChildOffset = lockedNodeFileStream.Offset;

                    
                }
            }

            lockedNodeFileStream.Write(0);

            return thisNodeOffset;
        }

        public static bool AddNext(this Node tnode, Node next, int i, bool trim = true)
        {
            if (trim)
            {
                foreach (TypelessDataRow row in tnode.MatchingRows)
                {
                    if (next.Evaluate(row))
                    {
                    }
                }

                int hits = next.Matched;

                if (DataSourceBuilder.Settings.Results.MatchOnly && next.Results[(int)MatchResult.Both] == 0)
                {
                    hits = 0;
                }

                if (hits >= DataSourceBuilder.Settings.Results.MinimumHits)
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
            if (!tnode.LastNode && tnode.Next.Where(n => n != null).Count() == 0)
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

        public static bool Evaluate(this INode tnode, Evaluation e)
        {
            if (tnode.Header == -1)
            {
                foreach (Node child in tnode.Next)
                {
                    if (child.Evaluate(e) && child.Header != -1)
                    {
                        break;
                    }
                }

                return true;
            }

            bool MatchesRoute = e.DataRow.Equals(tnode.Header, tnode.Value);

            if (MatchesRoute)
            {
                if (tnode.Next != null)
                {
                    foreach (Node child in tnode.Next)
                    {
                        if (child.Evaluate(e) && child.Header != -1)
                        {
                            break;
                        }
                    }
                }

                e.MatchRoute(tnode);

                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool Evaluate(this Node tnode, TypelessDataRow dataRow)
        {
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

            tnode.Results[(int)pool]++;

            return MatchesRoute;
        }

        public static IEnumerable<Node> FullTree(this Node tnode)
        {
            if (tnode.Header != -1)
            {
                yield return tnode;
            }

            if (tnode.Next != null)
            {
                foreach (Node child in tnode.Next)
                {
                    if (child is null)
                    {
                        continue;
                    }

                    foreach (Node n in child.FullTree())
                    {
                        yield return n;
                    }
                }
            }
        }

        public static int GetKey(this INode tnode)
        {
            int Key = 0;

            INode n = tnode;
            List<INode> tree = new List<INode>();

            while (n != null)
            {
                tree.Add(n);
                n = n.ParentNode;
            }

            foreach (INode tn in tree.Where(tnn => tnn.Header != -1))
            {
                Key |= (1 << tn.Header);
            }

            return Key;
        }

        public static float GetWeight(this Node n, AnalysisResults r)
        {
            float Weight;
            if (n.Accuracy > r.BaseRate)
            {
                Weight = n.Accuracy / r.BaseRate;
            }
            else
            {
                Weight = 0 - (r.BaseRate / n.Accuracy);
            }

            return Weight;
        }

        public static void RemoveNode(this Node tnode, Node n)
        {
            tnode.Next = tnode.Next.Where(ni => ni != n).ToArray();

            tnode.CheckValidity();
        }

        public static void Trim(this Node tnode)
        {
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
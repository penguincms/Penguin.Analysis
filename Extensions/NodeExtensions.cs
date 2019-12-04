using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis.Extensions
{
    public static class NodeExtensions
    {
        #region Methods

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

        public static bool Evaluate(this Node tnode, Evaluation e)
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

        public static int GetKey(this Node tnode)
        {
            int Key = 0;

            Node n = tnode;
            List<Node> tree = new List<Node>();

            while (n != null)
            {
                tree.Add(n);
                n = n.ParentNode;
            }

            foreach (Node tn in tree.Where(tnn => tnn.Header != -1))
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
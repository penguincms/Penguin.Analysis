using Penguin.Analysis.Constraints;
using Penguin.Analysis.DataColumns;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Penguin.Analysis
{
    internal class NodeSetGraph : IEnumerable<NodeSetCollection>
    {
        private readonly DataSourceBuilder Builder;
        private readonly object collectionLock = new object();
        public long Index { get; private set; } = -1;
        public long MaxCount { get; private set; }
        public long RealCount { get; private set; }
        public List<sbyte> Individuals = new List<sbyte>();

        internal NodeSetGraph(DataSourceBuilder builder)
        {
            Builder = builder;

            foreach (Exclusive e in Builder.RouteConstraints.OfType<Exclusive>()) //Move this check to the interface (NOTEXC)
            {
                if (e.Key == 0)
                {
                    e.SetKey(Builder.Registrations.ToArray());
                }

                foreach (sbyte i in new LongByte((long)e.Key))
                {
                    Individuals.Add(i);
                }
            }

            MaxCount = (long)Math.Pow(2, ColumnsToProcess.Where(r => !Individuals.Contains(r.ColumnIndex)).Count() - 1) + Individuals.Count;

            float LastPer = 0;

            HashSet<long> AlteredNodes = new HashSet<long>();
            foreach (long h in BuildNodeDefinitions((n) => ValidateNode(n, AlteredNodes, Builder)))
            {
                RealCount++;

                float thisPer = ((int)((Index * 10000) / MaxCount) / 100f);

                if (LastPer != thisPer)
                {
                    LastPer = thisPer;
                    Console.WriteLine($"Reading node count: {RealCount} - {thisPer}%");
                }
            }

            Console.Clear();
        }

        public IEnumerator<NodeSetCollection> GetEnumerator()
        {
            HashSet<long> AlteredNodes = new HashSet<long>();

            IEnumerator<NodeSetCollection> cEnum = BuildNodeDefinitions((n) => (ValidateNode(n, AlteredNodes, Builder) != 0) ? new NodeSetCollection(n) : null).GetEnumerator();

            NodeSetCollection next;

            Monitor.Enter(collectionLock);

            bool success = cEnum.MoveNext();

            while (success)
            {
                next = cEnum.Current;

                Monitor.Exit(collectionLock);

                yield return next;

                Monitor.Enter(collectionLock);

                success = cEnum.MoveNext();
            };

            Monitor.Exit(collectionLock);
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private static IEnumerable<T> BuildComplexTree<T>((sbyte ColumnIndex, int[] Values)[] ColumnData, Func<IList<(sbyte ColumnIndex, int[] Values)>, T> ValidateNode)
        {
            long Hc = (long)Math.Pow(2, ColumnData.Length);

            for (long Hi = 0; Hi < Hc; Hi += 2)
            {
                byte sbits = 0;

                long Hb = Hi + 1;

                while (Hb != 0)
                {
                    if ((Hb & 1) != 0)
                    {
                        sbits++;
                    }

                    Hb >>= 1;
                }

                List<(sbyte ColumnIndex, int[] Values)> thisGraph = new List<(sbyte ColumnIndex, int[] Values)>(sbits);

                for (int Wi = ColumnData.Length - 1; Wi >= 0; Wi--)
                {
                    if ((((Hi + 1) >> Wi) & 1) != 0)
                    {
                        thisGraph.Add(ColumnData[Wi]);
                    }
                }

                if (thisGraph.Any())
                {
                    yield return ValidateNode.Invoke(thisGraph);
                }
            }
        }

        private static long ValidateNode(IList<(sbyte ColumnIndex, int[] Values)> toValidate, HashSet<long> AlteredNodes, DataSourceBuilder Builder)
        {
            LongByte key = new LongByte(toValidate.Select(v => v.ColumnIndex));

            long toReturn = 0;

            //Check the valid function to see what the most valid subset of the current headers is
            if (Builder.IfValid(key, (ckey) =>  //If there is any valid subset
            {
                //Has the set been trimmed?
                bool altered = key != ckey;

                if (altered)
                {
                    //We're only counting modified ones to save memory. Unmodified sets wont show up twice,
                    //but modified sets could show up in the pre-post form depending on what order they show up in.
                    //it should be short first so it shouldn't be needed, but you never know
                    AlteredNodes.Add(key);

                    //Make sure we havent counted this valid subset yet as part of another group
                    if (AlteredNodes.Add(ckey))
                    {
                        toReturn = ckey;
                    }
                }
                else
                {
                    //No alteration + valid, return as is
                    toReturn = ckey;
                }
            }))
            {
                //The above func sets this value while finding a valid subset
                return toReturn;
            }
            else
            {
                //No valid subsets? Log it.
                Builder.Settings.CheckedConstraint?.Invoke(key.Select(k => Builder.Registrations[k].Header), true);

                //return null. We do nothing
                return 0;
            }
        }

        private IEnumerable<(sbyte ColumnIndex, int[] Values)> ColumnsToProcess
        {
            get
            {
                return Builder.Registrations
                              //.OrderByDescending(r => r.Column.GetOptions().Count())
                              .Where(r => !(r.Column is Key))
                              .Select(r => (
                                ColumnIndex: (sbyte)Builder.Registrations.IndexOf(r),
                                Values: r.Column.GetOptions().ToArray()
                              ));
            }
        }
        private IEnumerable<T> BuildNodeDefinitions<T>(Func<IList<(sbyte ColumnIndex, int[] Values)>, T> ValidateNode)
        {
            Index = 0;

            foreach(T t in  BuildComplexTree(ColumnsToProcess
                                                     .Where(v => !Individuals.Contains(v.ColumnIndex))
                                                     .ToArray(), 
                                             ValidateNode)
                            .Where((v)
                             =>
                            {
                                bool r = !EqualityComparer<T>.Default.Equals(v, default);
                                if (r)
                                {
                                    Index++;
                                }
                                return r;
                            }))
            {
                yield return t;
            }
        }
    }
}
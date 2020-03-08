using Penguin.Analysis.Constraints;
using Penguin.Analysis.DataColumns;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Penguin.Analysis
{
    public struct NodeSetGraphProgress
    {
        public float Progress { get; set; }
        public long Index { get; set; }
        public long MaxCount { get; set; }
        public long RealCount { get; set; }
    }
    public class NodeSetGraph : IEnumerable<NodeSetCollection>
    {
        private readonly DataSourceBuilder Builder;
        private readonly object collectionLock = new object();
        public long Index { get; private set; } = -1;
        public long MaxCount { get; private set; }
        public long RealCount { get; private set; } = 0;

        public Action<NodeSetGraphProgress> ReportProgress { get; set; }

        internal NodeSetGraph(DataSourceBuilder builder, Action<NodeSetGraphProgress> reportProgress = null)
        {
            Builder = builder;
            
            ReportProgress = reportProgress;

            MaxCount = (long)Math.Pow(2, ColumnsToProcess.Count());

            HashSet<long> AlteredNodes = new HashSet<long>();

            Action<ValidationResult> badRoute = null;

            if (builder.Settings.CheckedConstraint != null)
            {
                badRoute = (v) =>
                {
                    IEnumerable<string> headers = v.Checked == 0 ? new List<string>() : v.Checked.Select(k => Builder.Registrations[k].Header);
                    Builder.Settings.CheckedConstraint.Invoke(headers, v);
                };
            }

            Action<LongByte> goodRoute = null;

            if (builder.Settings.NoCheckedConstraint != null)
            {
                goodRoute = (v) =>
                {
                    IEnumerable<string> headers = v == 0 ? new List<string>() : v.Select(k => Builder.Registrations[k].Header);
                    Builder.Settings.NoCheckedConstraint.Invoke(headers, v);
                };
            }

            RealCount = BuildNodeDefinitions((n) => ValidateNode(n, AlteredNodes, Builder, badRoute, goodRoute)).Count();

            ReportProgress?.Invoke(new NodeSetGraphProgress()
            {
                Index = Index,
                MaxCount = MaxCount,
                Progress = 1,
                RealCount = RealCount
            });
        }

        public IEnumerator<NodeSetCollection> GetEnumerator()
        {
            HashSet<long> AlteredNodes = new HashSet<long>();

            IEnumerator<NodeSetCollection> cEnum = BuildNodeDefinitions((n) => ValidateNode(n, AlteredNodes, Builder) != 0 ? new NodeSetCollection(n) : null).GetEnumerator();

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
            int[][] data = new int[ColumnData.Length][];

            for(int i = 0; i < ColumnData.Length; i++)
            {
                data[i] = ColumnData[i].Values;
            }

            long Hc = (long)Math.Pow(2, ColumnData.Length) - 1;

            for (long Hi = Hc; Hi >= 1; Hi -= 2)
            {
                byte sbits = 0;

                long Hb = Hi;

                while (Hb != 0)
                {
                    if ((Hb & 1) != 0)
                    {
                        sbits++;
                    }

                    Hb >>= 1;
                }

                List<(sbyte ColumnIndex, int[] Values)> thisGraph = new List<(sbyte ColumnIndex, int[] Values)>(sbits);

                for (sbyte Wi = (sbyte)(ColumnData.Length - 1); Wi >= 0; Wi--)
                {
                    if (((Hi >> Wi) & 1) != 0)
                    {
                        thisGraph.Add((Wi, data[Wi]));
                    }
                }

                if (thisGraph.Any())
                {
                    yield return ValidateNode.Invoke(thisGraph);
                }
            }
        }

        private static long ValidateNode(IList<(sbyte ColumnIndex, int[] Values)> toValidate, HashSet<long> AlteredNodes, DataSourceBuilder Builder, Action<ValidationResult> OnFailure = null, Action<LongByte> OnSuccess = null)
        {
            LongByte key = new LongByte(toValidate.Select(v => v.ColumnIndex));

            long toReturn = 0;

            if(AlteredNodes.Contains(key))
            {
                return 0;
            }

            //Check the valid function to see what the most valid subset of the current headers is
            if (Builder.IfValid(key, OnFailure, (ckey) =>  //If there is any valid subset
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
            }).IsValid)
            {
                if (OnSuccess != null && toReturn != 0)
                {
                    OnSuccess(toReturn);
                }
                //The above func sets this value while finding a valid subset
                return toReturn;
            } else
            {
                return 0;
            }

        }

        private IEnumerable<(sbyte ColumnIndex, int[] Values)> ColumnsToProcess
        {
            get
            {
                return Builder.Registrations
                              .Select(r => (
                                ColumnIndex: (sbyte)Builder.Registrations.IndexOf(r),
                                Values: r.Column.GetOptions().ToArray()
                              ));
            }
        }
        private IEnumerable<T> BuildNodeDefinitions<T>(Func<IList<(sbyte ColumnIndex, int[] Values)>, T> ValidateNode)
        {
            Index = 0;
            IEnumerable<(sbyte ColumnIndex, int[] Values)> columnsToProcess = ColumnsToProcess;
            float LastPer = 0;


            foreach (T t in  BuildComplexTree(columnsToProcess.ToArray(), (r) => ValidateNode(r)))
            {
                if (ReportProgress != null)
                {
                    Index++;

                    float thisPer = ((int)((Index * 10000) / MaxCount) / 10000f);

                    if (LastPer != thisPer)
                    {
                        LastPer = thisPer;

                        ReportProgress.Invoke(new NodeSetGraphProgress()
                        {
                            Index = Index,
                            MaxCount = MaxCount,
                            Progress = thisPer,
                            RealCount = RealCount
                        });
                    }
                }

                if (!EqualityComparer<T>.Default.Equals(t, default))
                {
                    yield return t;
                }
            }
        }
    }
}
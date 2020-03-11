using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Penguin.Extensions.Strings.Security;
using Penguin.Extensions.Strings;
using System.Threading;
using Penguin.Analysis.Constraints;

namespace Penguin.Analysis
{
    public struct NodeSetGraphProgress
    {
        public long Index;
        public long MaxCount;
        public float Progress;
        public long RealCount;
    }

    public class NodeSetGraph : IEnumerable<NodeSetCollection>, IDisposable
    {
        private readonly DataSourceBuilder Builder;
        private readonly object collectionLock = new object();
        private Stream ValidationCache;
        public long Index { get; private set; } = -1;
        public long MaxCount { get; private set; }
        public long RealCount { get; private set; } = 0;
        public long RealIndex { get; private set; }
        public Action<NodeSetGraphProgress> ReportProgress { get; set; }

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

        private string ValidationCacheFileName
        {
            get
            {
                string ch = string.Empty;

                unchecked
                {
                    foreach (ColumnRegistration r in Builder.Registrations)
                    {
                        ch += r.Header + "|";
                    }
                }

                return $"NodeValidation_{ch.ComputeSha1Hash()}.cache";
            }
        }

        internal NodeSetGraph(DataSourceBuilder builder, Action<NodeSetGraphProgress> reportProgress = null)
        {
            Builder = builder;
            ReportProgress = reportProgress;

            if (File.Exists(ValidationCacheFileName))
            {
                ValidationCache = new FileStream(ValidationCacheFileName, FileMode.Open, FileAccess.Read, FileShare.Read);

                byte[] bytes = new byte[8];
                ValidationCache.Read(bytes, 0, 8);

                RealCount = BitConverter.ToInt64(bytes, 0);

                if (RealCount == 0)
                {
                    ValidationCache.Dispose();
                    ValidationCache = null;
                    File.Delete(ValidationCacheFileName);
                }
            }

            MaxCount = (long)Math.Pow(2, ColumnsToProcess.Count() - 1);

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

            if (RealCount == 0)
            {
                foreach (long _ in BuildNodeDefinitions(badRoute, goodRoute, ReportProgress))
                {
                    RealCount++;
                }
            }

            foreach ((sbyte ColumnIndex, int[] Values) cv in ColumnsToProcess)
            {
                NodeSet ns = new NodeSet(cv);

                NodeSetCollection.NodeSetCache[cv.ColumnIndex] = ns;
            }

            ReportProgress?.Invoke(new NodeSetGraphProgress()
            {
                Index = Index,
                MaxCount = MaxCount,
                Progress = 1,
                RealCount = RealCount
            });
        }

        public void Dispose()
        {
            ValidationCache?.Dispose();
        }

        public IEnumerator<NodeSetCollection> GetEnumerator()
        {
            HashSet<long> AlteredNodes = new HashSet<long>();

            IEnumerator<long> cEnum = BuildNodeDefinitions().GetEnumerator();

            Monitor.Enter(collectionLock);

            while (cEnum.MoveNext())
            {
                Monitor.Exit(collectionLock);

                yield return new NodeSetCollection(cEnum.Current);

                Monitor.Enter(collectionLock);
            }

            Monitor.Exit(collectionLock);
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private static IEnumerable<List<(sbyte ColumnIndex, int[] Values)>> BuildComplexTree((sbyte ColumnIndex, int[] Values)[] ColumnData)
        {
            int[][] data = new int[ColumnData.Length][];

            for (int i = 0; i < ColumnData.Length; i++)
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

                yield return thisGraph;
            }
        }

        private static long ValidateNode(LongByte key, HashSet<long> AlteredNodes, DataSourceBuilder Builder, Action<ValidationResult> OnFailure = null, Action<LongByte> OnSuccess = null)
        {
            long toReturn = 0;

            if (AlteredNodes.Contains(key))
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
            }
            else
            {
                return 0;
            }
        }

        private IEnumerable<long> BuildNodeDefinitions(Action<ValidationResult> OnFailure = null, Action<LongByte> OnSuccess = null, Action<NodeSetGraphProgress> reportProgress = null)
        {
            //string jumpListFname = DateTime.Now.ToString("yyyyMMddHHmmss") + "_NodeCacheGeneration.log";
            //string jumpListLoadFname = DateTime.Now.ToString("yyyyMMddHHmmss") + "_NodeCacheLoad.log";

            //long SkipMask = new LongByte(Builder.Registrations.OfType<Exclusive>().Select(e => e.Key));

            Index = 0;
            RealIndex = 0;
            IEnumerable<(sbyte ColumnIndex, int[] Values)> columnsToProcess = ColumnsToProcess;
            HashSet<long> AlteredNodes = new HashSet<long>();
            bool LastValid = true;
            bool ExistingStream = !(ValidationCache is null);
            long NextFlip = 0;
            long NextKey = 0;

            int Step = (int)(MaxCount / 10000);

            void ReadNextFlip()
            {
                byte[] bytes = new byte[8];
                ValidationCache.Read(bytes, 0, 8);
                NextFlip = BitConverter.ToInt64(bytes, 0);
                ValidationCache.Read(bytes, 0, 8);
                NextKey = BitConverter.ToInt64(bytes, 0);
            }

            if (!ExistingStream)
            {
                ValidationCache = new FileStream(ValidationCacheFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                ValidationCache.Write(BitConverter.GetBytes((long)0), 0, 8);
            }
            else
            {
                ReadNextFlip();
            }

            void CheckReportProgress()
            {
                if (reportProgress != null && Index % Step == 0)
                {
                    reportProgress.Invoke(new NodeSetGraphProgress()
                    {
                        Index = Index,
                        MaxCount = MaxCount,
                        Progress = ((int)((Index * 10000) / MaxCount) / 10000f),
                        RealCount = RealCount
                    });
                }
            }

            IEnumerator<List<(sbyte ColumnIndex, int[] Values)>> TreeEnumerator = BuildComplexTree(columnsToProcess.ToArray()).GetEnumerator();

            bool hasNext = TreeEnumerator.MoveNext();

            while (hasNext)
            {
                long key = 0;

                if (!ExistingStream)
                {
                    LongByte thisKey = new LongByte(TreeEnumerator.Current.Select(c => c.ColumnIndex));

                    long newKey = ValidateNode(thisKey, AlteredNodes, Builder, OnFailure, OnSuccess);

                    bool stateChange = (newKey != 0) != LastValid;
                    bool keyChange = newKey != thisKey.Value && newKey != 0;
                    bool write = stateChange || keyChange;

                    if (write)
                    {
                        //List<string> lines = new List<string>
                        //{
                        //    $"{thisKey}:"
                        //};

                        //if (stateChange)
                        //{
                        //    lines.Add($"\tNew State: {(newKey != 0)}");
                        //}

                        //if (keyChange)
                        //{
                        //    lines.Add($"\tNew Key: {new LongByte(newKey)}");
                        //}

                        //lines.Add($"\t\tWriting: {Index}, {newKey}");

                        //File.AppendAllLines(jumpListFname, lines);

                        //foreach (string s in lines)
                        //{
                        //    Console.WriteLine(s);
                        //}

                        ValidationCache.Write(BitConverter.GetBytes(Index), 0, 8);
                        ValidationCache.Write(BitConverter.GetBytes(newKey), 0, 8);

                        if (stateChange)
                        {
                            LastValid = (newKey != 0);
                        }
                    }

                    if (newKey != 0)
                    {
                        key = newKey;
                    }
                }
                else
                {
                    if (Index == NextFlip)
                    {
                        //File.AppendAllText(jumpListLoadFname, $"{new LongByte(TreeEnumerator.Current.Select(c => c.ColumnIndex))}:" + System.Environment.NewLine);

                        if (NextKey != 0)
                        {
                            //if(!LastValid)
                            //{
                            //    File.AppendAllText(jumpListLoadFname, $"\tNew State: {true}" + System.Environment.NewLine);
                            //}

                            //if (new LongByte(TreeEnumerator.Current.Select(c => c.ColumnIndex)) != new LongByte(NextKey))
                            //{
                            //    File.AppendAllText(jumpListLoadFname, $"\tNew Key: {new LongByte(NextKey)}" + System.Environment.NewLine);
                            //}

                            LastValid = true;
                            key = NextKey;
                        }
                        else
                        {
                            //File.AppendAllText(jumpListLoadFname, $"\tNew State: {!LastValid}" + System.Environment.NewLine);

                            LastValid = !LastValid;
                            if (LastValid)
                            {
                                key = new LongByte(TreeEnumerator.Current.Select(c => c.ColumnIndex));
                            }
                        }
                        //File.AppendAllText(jumpListLoadFname, $"\t\tRead: {NextFlip}, {NextKey}" + System.Environment.NewLine);
                        ReadNextFlip();
                    }
                    else
                    {
                        key = new LongByte(TreeEnumerator.Current.Select(c => c.ColumnIndex));
                    }
                }

                if (LastValid && key != 0)
                {
                    if (ExistingStream)
                    {
                        long KeyValidation = ValidateNode(key, new HashSet<long>(), Builder);
                        if (KeyValidation != key)
                        {
                            if (Debugger.IsAttached)
                            {
                                Debugger.Break();
                            }
                            else
                            {
                                throw new Exception($"Node revalidation failed for node {key}. The existing node cache does not appear to be valid");
                            }
                        }

                        OnSuccess?.Invoke(key);
                    }
                    yield return key;
                    RealIndex++;
                }

                if (ExistingStream && !LastValid)
                {
                    for (; Index < NextFlip; Index++)
                    {
                        hasNext = TreeEnumerator.MoveNext();
                        CheckReportProgress();
                    }
                }
                else
                {
                    Index++;
                    hasNext = TreeEnumerator.MoveNext();

                    if (!ExistingStream && ReportProgress != null)
                    {
                        CheckReportProgress();
                    }
                }
            }

            try
            {
                if (!ExistingStream)
                {
                    ValidationCache.Seek(0, SeekOrigin.Begin);
                    ValidationCache.Write(BitConverter.GetBytes(RealCount), 0, 8);
                }
                else
                {
                    ValidationCache.Seek(8, SeekOrigin.Begin);
                }
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
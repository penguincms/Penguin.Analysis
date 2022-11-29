using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Penguin.Extensions.String.Security;
using Penguin.Extensions.String;
using System.Threading;
using Penguin.Analysis.Constraints;

namespace Penguin.Analysis
{
    public struct NodeSetGraphProgress
    {
        public long Index;
        public long MaxCount;
        public float Progress;
        public int RealCount;
    }

    public class NodeSetGraph : IEnumerable<NodeSetCollection>, IDisposable
    {
        private readonly DataSourceBuilder Builder;
        private readonly object collectionLock = new object();
        private Stream ValidationCache;
        public long Index { get; private set; } = -1;
        public long MaxCount { get; private set; }
        public int RealCount { get; private set; } = 0;
        public int RealIndex { get; private set; }
        public Action<NodeSetGraphProgress> ReportProgress { get; set; }

        private IEnumerable<(sbyte ColumnIndex, int Values)> ColumnsToProcess
        {
            get
            {
                return Builder.Registrations
                              .Select(r => (
                                ColumnIndex: (sbyte)Builder.Registrations.IndexOf(r),
                                Values: r.Column.OptionCount
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

                RealCount = BitConverter.ToInt32(bytes, 0);

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

            foreach ((sbyte ColumnIndex, int Values) cv in ColumnsToProcess)
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

            while (cEnum.MoveNext())
            {
                yield return new NodeSetCollection(cEnum.Current);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

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
            Index = 0;
            RealIndex = 0;
            IEnumerable<(sbyte ColumnIndex, int Values)> columnsToProcess = ColumnsToProcess;
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

            ComplexTree complexTree = new ComplexTree(columnsToProcess);

            IEnumerator<List<(sbyte ColumnIndex, int Values)>> TreeEnumerator = complexTree.Build().GetEnumerator();

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
                        if (NextKey != 0)
                        {
                            LastValid = true;
                            key = NextKey;
                        }
                        else
                        {
                            LastValid = !LastValid;
                            if (LastValid)
                            {
                                key = new LongByte(TreeEnumerator.Current.Select(c => c.ColumnIndex));
                            }
                        }

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
                    complexTree.JumpToIndex(NextFlip - 1);
                    Index = NextFlip;
                    hasNext = TreeEnumerator.MoveNext();

                    CheckReportProgress();
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
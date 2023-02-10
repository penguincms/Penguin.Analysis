using Newtonsoft.Json;
using Penguin.Analysis.Constraints;
using Penguin.Analysis.DataColumns;
using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using Penguin.Analysis.Transformations;
using Penguin.Extensions.Collections;
using Penguin.IO.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Penguin.Analysis
{
    [Serializable]
    public class DataSourceBuilder : IColumnRegistrar, IDisposable
    {
        #region Fields

        public List<ColumnRegistration> Registrations = new();
        private readonly List<ITransform> Transformations = new();

        public ColumnRegistration TableKey { get; set; }

        #endregion Fields

        #region Properties

        private readonly List<IRouteConstraint> routeConstraint = new();
        private DataTable TempTable;

        public AnalysisResults Result { get; set; } = new AnalysisResults();

        [JsonIgnore]
        public IEnumerable<IRouteConstraint> RouteConstraints => routeConstraint;

        #endregion Properties

        #region Classes

        [NonSerialized]
        public DataSourceSettings Settings = new();

        #endregion Classes

        public static JsonSerializer DefaultJsonSerializer => new()
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            TypeNameHandling = TypeNameHandling.Auto
        };

        public static JsonSerializerSettings DefaultSerializerSettings => new()
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            TypeNameHandling = TypeNameHandling.Auto
        };

        [JsonIgnore]
        public JsonSerializer JsonSerializer { get; set; }

        [JsonIgnore]
        public JsonSerializerSettings JsonSerializerSettings { get; set; } = DefaultSerializerSettings;

        #region Constructors

        public DataSourceBuilder(string FileName) : this(new FileInfo(FileName).ToDataTable())
        {
        }

        public DataSourceBuilder()
        {
            JsonSerializer = JsonSerializer.Create(JsonSerializerSettings);
        }

        public DataSourceBuilder(DataTable dt) : this()
        {
            TempTable = dt;
        }

        #endregion Constructors

        #region Methods

        private static readonly string MemLog = DateTime.Now.ToString("yyyyMMddHHmmss") + ".log";

        private FileStream ManagedMemoryStream;

        [JsonIgnore]
        public bool IsPreloaded { get; private set; }

        [JsonIgnore]
        public Task MemoryManagementTask { get; private set; }

        [JsonIgnore]
        public Task PreloadTask { get; private set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
        public static DataSourceBuilder Deserialize(string FilePath, MemoryManagementStyle memoryManagementStyle = MemoryManagementStyle.Unmanaged, int maxCacheCount = 10_000_000, JsonSerializerSettings jsonSerializerSettings = null)
        {
            DiskNode._backingStream = null;

            DiskNode.CacheNodes = memoryManagementStyle is not MemoryManagementStyle.NoCache and not MemoryManagementStyle.Unmanaged;

            DiskNode.MaxCacheCount = maxCacheCount;

            FileStream fstream = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            LockedNodeFileStream stream = new(fstream);

            long JsonOffset;
            byte[] jbytes = new byte[8];

            _ = fstream.Read(jbytes, 0, 8);

            JsonOffset = BitConverter.ToInt64(jbytes, 0);

            _ = fstream.Seek(JsonOffset, SeekOrigin.Begin);

            byte[] b64Bytes = new byte[fstream.Length - JsonOffset];
            int b64P = 0;
            int tbyte;

            while ((tbyte = fstream.ReadByte()) != -1)
            {
                b64Bytes[b64P++] = (byte)tbyte;
            }

            string b64 = System.Text.Encoding.UTF8.GetString(b64Bytes);

            string Json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));

            DataSourceBuilder toReturn = JsonConvert.DeserializeObject<DataSourceBuilder>(Json, jsonSerializerSettings ?? DefaultSerializerSettings);

            DiskNode virtualRoot = new(stream, DiskNode.HEADER_BYTES);

            OptimizedRootNode.Flush();
            OptimizedRootNode gNode = new(virtualRoot, toReturn.Settings);

            toReturn.Result.RootNode = gNode;
            toReturn.Settings.MaxCacheCount = maxCacheCount;

            if (memoryManagementStyle.HasFlag(MemoryManagementStyle.Preload))
            {
                toReturn.Preload(FilePath, memoryManagementStyle);
            }
            else
            {
                toReturn.PreloadTask = gNode.Preload(FilePath);
            }

            return toReturn;
        }

        public void AddRouteConstraint(IRouteConstraint constraint)
        {
            routeConstraint.Add(constraint);
        }

        public void Build(string outputFilePath)
        {
            Transform();

            using FileStream fstream = new(outputFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 1_000_000_00);
            using LockedNodeFileStream stream = new(fstream, false);
            stream.Write((long)0);

            Generate(stream);

            _ = fstream.Seek(0, SeekOrigin.End);

            long JsonOffset = stream.Offset;

            try
            {
                string Json = JsonConvert.SerializeObject(this, JsonSerializerSettings);

                stream.Write(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Json)));

                _ = stream.Seek(0);

                stream.Write(JsonOffset);

                stream.Flush();
            }
            catch (Exception)
            {
                throw;
            }
        }

        public Evaluation Evaluate(DataRow dr, bool MultiThread = true)
        {
            if (dr is null)
            {
                throw new ArgumentNullException(nameof(dr));
            }

            Dictionary<string, string> toEvaluate = new();

            foreach (DataColumn dc in dr.Table.Columns)
            {
                toEvaluate.Add(dc.ColumnName, dr[dc].ToString());
            }

            return Evaluate(toEvaluate, MultiThread);
        }

        public Evaluation Evaluate(Dictionary<string, string> dataRow, bool MultiThread = true)
        {
            Evaluation evaluation = new(Transform(dataRow), Result)
            {
                //evaluation.Score = this.Result.BaseRate;

                Result = Result,
                InputData = dataRow
            };

            int[] vints = evaluation.DataRow.ToArray();

            for (int index = 0; index < vints.Length - 1; index++)
            {
                string k = Registrations[index].Header;
                string v = Registrations[index].Column.Display(vints[index]);

                evaluation.CalculatedData.Add(k, v);
            }

            evaluation.CalculatedData.Add(TableKey.Header, TableKey.Column.Display(vints[^1]));

            INode rootNode = Result.RootNode;

            rootNode.Evaluate(evaluation, 0, MultiThread);

            return evaluation;
        }

        public void Generate(LockedNodeFileStream outputStream)
        {
            if (outputStream is null)
            {
                throw new ArgumentNullException(nameof(outputStream));
            }

            Console.Clear();

            Console.WriteLine($"Building complex tree");

            object rootLock = new();

            NodeSetGraph graph = new(this, Settings.NodeEnumProgress);

            Settings.PostGraphCalculation?.Invoke(graph);

            Console.WriteLine($"Building decision tree", 0);

            long RootChildListOffset = DiskNode.HEADER_BYTES + DiskNode.NODE_SIZE + 4; //4 byte child list size

            outputStream.Seek(DiskNode.HEADER_BYTES + 9);

            outputStream.Write((sbyte)-1);
            outputStream.Write((ushort)0);
            outputStream.Write((sbyte)-1);
            outputStream.Write(ushort.MaxValue);

            //Main list too big for UShort. Wonky logic to handle
            outputStream.Seek(RootChildListOffset - 4);

            outputStream.Write(graph.RealCount);

            long CurrentNodeOffset = (graph.RealCount * DiskNode.NEXT_SIZE) + RootChildListOffset;

            object offsetLock = new();

            outputStream.Seek(CurrentNodeOffset);

            int threads = Environment.ProcessorCount * 1;

            Result.PositiveIndicators = Result.RawData.Rows.Where(r => r.MatchesOutput).Count();
            Result.TotalRows = Result.RawData.RowCount;

            bool generatingNodes = true;

            ConcurrentQueue<MemoryNodeFileStream> flushCommands = new();
            Task flushToDisk = Task.Run(() =>
            {
                do
                {
                    while (flushCommands.Any())
                    {
                        MemoryNodeFileStream command;

                        while (!flushCommands.TryDequeue(out command))
                        {
                        }

                        while (!command.Ready)
                        {
                            Thread.Sleep(50);
                        }

                        outputStream.Write(command.ToArray());
                    }

                    System.Threading.Thread.Sleep(50);
                } while (generatingNodes || flushCommands.Any());
            });

            SerializationResults results = new();
            Task progress = Task.Run(async () =>
            {
                while (generatingNodes)
                {
#if DEBUG
                    Debug.WriteLine($"--------Graph: [{graph.RealIndex + 1}/{graph.RealCount}]");
#endif

#if !DEBUG
                    Console.WriteLine($"--------Graph: [{graph.RealIndex + 1}/{graph.RealCount}]");
#endif
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            });

            Parallel.ForEach(graph, new ParallelOptions()
            {
                MaxDegreeOfParallelism = threads
            }, nodeSetData =>
            {
                int nodeseti = 0;
                double nodesetc = 0;

                MemoryNode thisRoot = new(-1, ushort.MaxValue, nodeSetData[0].Values, Result.RawData.RowCount);

                IEnumerable<MemoryNode> thisRootList = new List<MemoryNode> { thisRoot };

                nodesetc = nodeSetData.Count;

                double dataRowc = Result.RawData.RowCount;

                thisRoot.MatchingRows.AddRange(Result.RawData.Rows);

                for (nodeseti = 0; nodeseti < nodesetc; nodeseti++)
                {
                    sbyte ColumnIndex = nodeSetData[nodeseti].ColumnIndex;
                    int Values = nodeSetData[nodeseti].Values;
                    int childCount = 0;
                    dataRowc = Values * thisRootList.Count();

                    if (nodeseti < nodesetc - 1)
                    {
                        childCount = nodeSetData[nodeseti + 1].Values;
                    }

                    foreach (MemoryNode node in thisRootList)
                    {
                        for (ushort i = 0; i < Values; i++)
                        {
                            MemoryNode n = new(ColumnIndex, i, childCount, (ushort)node.MatchingRows.Count);

                            node.AddNext(n, i);
                        }

                        node.TrimNext(this);
                    }

                    thisRootList = thisRootList.SelectMany(n => n.next).Where(n => n != null).ToList();
                }

                while (thisRoot.Trim(this))
                {
                }

                //no children on the header node. No point keeping it
                if (thisRoot.next is null || !thisRoot.next.Any(n => n != null))
                {
                    return;
                }

                Result.RegisterTree(thisRoot, this);

                MemoryNodeFileStream memCache;

                lock (offsetLock)
                {
                    memCache = new MemoryNodeFileStream(CurrentNodeOffset);
                    CurrentNodeOffset += thisRoot.GetLength(2); //not real root, ushort child list
                    flushCommands.Enqueue(memCache);
                }

                results.AddRange(thisRoot.Serialize(memCache, DiskNode.HEADER_BYTES));

                memCache.Ready = true;

                thisRoot = null;

                thisRootList = null;
            });

            generatingNodes = false;

            flushToDisk.Wait();

            IEnumerable<NodeMeta> PreloadResults = results.OrderByDescending(n => n.Accuracy);
            IEnumerable<NodeMeta> RootOffsets = results.Where(n => n.Root).OrderBy(n => n.Header).ThenByDescending(n => n.Accuracy);

            long sortPos = outputStream.Offset;

            outputStream.Seek(RootChildListOffset);

            foreach (long l in RootOffsets.Select(n => n.Offset))
            {
                outputStream.Write(l.ToInt40Array());
            }

            outputStream.Seek(8);
            outputStream.Write(sortPos);

            outputStream.Seek(sortPos);

            foreach (NodeMeta nm in PreloadResults)
            {
                if (nm.Offset != 0)
                {
                    outputStream.Write(nm.Offset.ToInt40Array());
                }
            }

            try
            {
                Result.RootNode = new DiskNode(outputStream, DiskNode.HEADER_BYTES);
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine(ex.Message);
                    Debug.WriteLine(ex.StackTrace);
                    Debugger.Break();
                }
                else
                {
                    throw;
                }
            }
        }

        public string GetNodeName(INode toCheck)
        {
            string toReturn = string.Empty;

            while (toCheck != null && toCheck.Header != -1)
            {
                string next = $"{Registrations[toCheck.Header].Header}:{Registrations[toCheck.Header].Column.Display(toCheck.Value)}";

                toReturn = !string.IsNullOrEmpty(toReturn) ? $"{next} => {toReturn}" : next;

                toCheck = toCheck.ParentNode;
            }

            return toReturn;
        }

        public ValidationResult IfValid(LongByte Key, Action<ValidationResult> OnFailure = null, Action<long> HeaderAction = null)
        {
            ValidationResult result = null;

            if (Key == 0)
            {
                result = new ValidationResult("Key can not be 0", Key);
            }
            else
            {
                LongByte cKey = Key;

                do
                {
                    IEnumerator<IRouteConstraint> RouteViolations = this.RouteViolations(cKey).GetEnumerator();

                    bool HasViolation = RouteViolations.MoveNext();

                    if (!HasViolation)
                    {
                        break;
                    }

                    result = new ValidationResult(RouteViolations.Current, cKey);

                    if (OnFailure != null)
                    {
                        while (RouteViolations.MoveNext())
                        {
                            result.Violations.Add(RouteViolations.Current);
                        }

                        OnFailure?.Invoke(result);
                    }

                    _ = cKey.TrimRight();

                    if (cKey.Value == 0)
                    {
                        result = new ValidationResult("Key can not be 0", Key);
                        OnFailure?.Invoke(result);
                        return result;
                    }
                } while (true);

                HeaderAction?.Invoke(cKey);

                result = new ValidationResult(cKey);
            }

            return result;
        }

        public async void Preload(string Engine, MemoryManagementStyle memoryManagementStyle)
        {
            ManagedMemoryStream = new FileStream(Engine, FileMode.Open, FileAccess.Read, FileShare.Read);

            byte[] offsetBytes = new byte[DiskNode.HEADER_BYTES];

            _ = ManagedMemoryStream.Read(offsetBytes, 0, offsetBytes.Length);

            long jsonOffset = offsetBytes.GetLong(0);
            long SortOffset = offsetBytes.GetLong(8);

            _ = ManagedMemoryStream.Seek(SortOffset, SeekOrigin.Begin);

            if (memoryManagementStyle.HasFlag(MemoryManagementStyle.Preload))
            {
                PreloadTask ??= Task.Run(() => PreloadFunc(ManagedMemoryStream, SortOffset, jsonOffset, memoryManagementStyle));

                await PreloadTask.ConfigureAwait(false);
            }

            if (MemoryManagementTask is null)
            {
                MemoryManagementTask = new Task(() =>
                {
                    do
                    {
                        if (Disposing)
                        {
                            return;
                        }
                        try
                        {
                            PreloadFunc(ManagedMemoryStream, SortOffset, jsonOffset, memoryManagementStyle);
                        }
                        catch (Exception ex)
                        {
                            Penguin.Debugging.StaticLogger.Log("Prelod function failed to execute...");
                            Penguin.Debugging.StaticLogger.Log(ex);
                        }

                        Task.Delay(Settings.PreloadTimeoutMs).Wait();
                    } while (true);
                });

                MemoryManagementTask.Start();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        public void PreloadFunc(FileStream stream, long SortOffset, long JsonOffset, MemoryManagementStyle memoryManagementStyle)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (Disposing)
            {
                return;
            }

            static void Log(string toLog)
            {
                string toWrite = $"[{DateTime.Now:yyyy MM dd HH:mm:ss}]: {toLog}";
                Debug.WriteLine(toWrite);
                Penguin.Debugging.StaticLogger.Log(toWrite);
                File.AppendAllLines(MemLog, new List<string>() { toWrite });
            }

            try
            {
                Log("Running Preload...");

                ulong freeMem = SystemInterop.Memory.Status.ullAvailPhys;

                Log($"{freeMem} available");

                Log($"{Settings.MinFreeMemory} Min Free Memory");

                Log($"{Settings.RangeFreeMemory} Range Free Memory");

                Log($"Memory management mode {memoryManagementStyle}");

                if (memoryManagementStyle.HasFlag(MemoryManagementStyle.Preload))
                {
                    while (freeMem > Settings.MinFreeMemory + Settings.RangeFreeMemory && DiskNode.CurrentCacheCount < DiskNode.MaxCacheCount)
                    {
                        Log($"Found Free Memory. Filling...");

                        for (int i = 0; i < Settings.PreloadChunkSize / 2; i++)
                        {
                            if (Disposing)
                            {
                                return;
                            }

                            if (stream.Position >= JsonOffset)
                            {
                                return;
                            }

                            byte[] thisNodeBytes = new byte[8];

                            _ = stream.Read(thisNodeBytes, 0, thisNodeBytes.Length);

                            _ = DiskNode.LoadNode(DiskNode._backingStream, thisNodeBytes.GetLong(0), true);

                            if (DiskNode.CurrentCacheCount >= DiskNode.MaxCacheCount)
                            {
                                break;
                            }
                        }

                        freeMem = SystemInterop.Memory.Status.ullAvailPhys;
                    }
                }

                if (freeMem < Settings.MinFreeMemory)
                {
                    int chunkBytes = Settings.PreloadChunkSize * 8;

                    while (freeMem < Settings.MinFreeMemory + Settings.RangeFreeMemory)
                    {
                        if (Disposing)
                        {
                            return;
                        }

                        Log($"Need more memory... Clearing cache.");
                        _ = DiskNode.ClearCache();

                        Log($"Collecting Garbage...");
                        GC.Collect();

                        Log($"Waiting...");
                        Task.Delay(1000).Wait();

                        freeMem = SystemInterop.Memory.Status.ullAvailPhys;

                        Log($"{freeMem} Free memory");

                        if (freeMem > Settings.MinFreeMemory || stream.Position == SortOffset || !memoryManagementStyle.HasFlag(MemoryManagementStyle.Preload))
                        {
                            Log($"Nothing left to do.");
                            return;
                        }

                        Log($"Reducing managed nodes...");

                        long oldPost = stream.Position;

                        long newPos = Math.Max(SortOffset, stream.Position - chunkBytes);

                        _ = stream.Seek(newPos, SeekOrigin.Begin);

                        for (int i = 0; i < Settings.PreloadChunkSize; i++)
                        {
                            if (Disposing)
                            {
                                return;
                            }

                            byte[] thisBlock = new byte[8];

                            _ = stream.Read(thisBlock, 0, thisBlock.Length);

                            long offset = thisBlock.GetLong(0);

                            if (DiskNode.MemoryManaged.TryGetValue(offset, out DiskNode dn))
                            {
                                if (DiskNode.MemoryManaged.Remove(offset))
                                {
                                    DiskNode.CurrentCacheCount--;
                                }
                            }

                            if (stream.Position >= stream.Length || stream.Position >= oldPost)
                            {
                                break;
                            }
                        }

                        Log(DiskNode.MemoryManaged.Count + " Memory Managed nodes remaining.");
                        GC.Collect();

                        Task.Delay(5000).Wait();

                        freeMem = SystemInterop.Memory.Status.ullAvailPhys;

                        _ = stream.Seek(newPos, SeekOrigin.Begin);
                    }
                }

                IsPreloaded = true;
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                Log(ex.StackTrace);
                Debugger.Break();
            }
        }

        public void RegisterColumn(string ColumnName, IDataColumn registration)
        {
            ColumnRegistration r = new() { Header = ColumnName, Column = registration };

            if (registration is Key)
            {
                TableKey = r;
            }
            else
            {
                Registrations.Add(r);
            }
        }

        public void RegisterColumn<T>(params string[] ColumnNames) where T : IDataColumn
        {
            if (ColumnNames is null)
            {
                throw new ArgumentNullException(nameof(ColumnNames));
            }

            foreach (string ColumnName in ColumnNames)
            {
                RegisterColumn(ColumnName, Activator.CreateInstance<T>());
            }
        }

        public void RegisterTransformation(ITransform transform)
        {
            Transformations.Add(transform);
        }

        /// <summary>
        /// Runs registered table transformations to create the final analysis table
        /// </summary>
        public void Transform() //I really feel like transforms and column transforms should be sequential (interlaced) in order added
        {
            Penguin.Debugging.StaticLogger.Log("Applying transformations");

            Result.RawData = Transform(TempTable, true);

            TempTable = null;
        }

        public TypelessDataRow Transform(Dictionary<string, string> dataRow) //I really feel like transforms and column transforms should be sequential (interlaced) in order added
        {
            if (dataRow is null)
            {
                throw new ArgumentNullException(nameof(dataRow));
            }

            using DataTable dt = new();

            List<object> values = new();

            foreach (KeyValuePair<string, string> kvp in dataRow)
            {
                dt.Columns.Add(new DataColumn(kvp.Key));
                values.Add(kvp.Value);
            }

            _ = dt.Rows.Add(values.ToArray());

            return Transform(dt, false).Rows.First();
        }

        private IEnumerable<IRouteConstraint> RouteViolations(LongByte Key)
        {
            foreach (IRouteConstraint constraint in routeConstraint) //Move this check to the interface (NOTEXC)
            {
                if (constraint.Key == 0)
                {
                    constraint.SetKey(Registrations.ToArray());
                }

                if (!constraint.Evaluate(Key))
                {
                    yield return constraint;
                }
            }
        }

        private TypelessDataTable Transform(DataTable dt, bool seedRegistrations)
        {
            TypelessDataTable toReturn;

            foreach (ITransform transform in Transformations)
            {
                dt = transform.TransformTable(dt);

                foreach (DataRow dr in dt.Rows)
                {
                    transform.TransformRow(dr);
                }
            }

            foreach (ITransform transform in Transformations)
            {
                transform.Cleanup(dt);
            }

            Settings.PostTransform?.Invoke(dt);

            toReturn = new TypelessDataTable(dt.Rows.Count);

            if (seedRegistrations) // We only want to seed on the initial generation
            {                      // Seeding on an individual transform would blow away
                                   // the values since all instances would have a count of 1

                foreach (ColumnRegistration registration in Registrations)
                {
                    if (registration.Column.SeedMe)
                    {
                        foreach (DataRow dr in dt.Rows)
                        {
                            registration.Column.Seed(dr[registration.Header].ToString(), Bool.GetValue(dr[TableKey.Header].ToString()) == 1);
                        }

                        registration.Column.EndSeed();
                    }
                }
            }

            foreach (DataRow dr in dt.Rows)
            {
                List<int> values = new();

                foreach (ColumnRegistration registration in Registrations)
                {
                    values.Add(registration.Column.Transform(dr[registration.Header].ToString()));
                }

                values.Add(Bool.GetValue(dr[TableKey.Header].ToString()));

                toReturn.AddRow(values.ToArray());
            }

            foreach (TypelessDataRow dr in toReturn.Rows)
            {
                dr.MatchesOutput = dr.Last() == 1;
            }

            return toReturn;
        }

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        private bool Disposing;

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Disposing = true;

                    try
                    {
                        if (PreloadTask?.IsCompleted ?? false)
                        {
                            MemoryManagementTask?.Dispose();
                        }
                    }
                    catch (Exception)
                    {
                    }

                    try
                    {
                        if (PreloadTask?.IsCompleted ?? false)
                        {
                            PreloadTask?.Dispose();
                        }
                    }
                    catch (Exception)
                    {
                    }

                    try
                    {
                        ManagedMemoryStream?.Dispose();
                    }
                    catch (Exception)
                    {
                    }

                    ManagedMemoryStream = null;

                    foreach (ColumnRegistration x in Registrations)
                    {
                        x.Dispose();
                    }

                    Registrations.Clear();

                    foreach (ITransform x in Transformations)
                    {
                        x.Dispose();
                    }

                    Transformations.Clear();

                    foreach (IRouteConstraint x in routeConstraint)
                    {
                        x.Dispose();
                    }

                    routeConstraint.Clear();

                    try
                    {
                        Result.Dispose();
                    }
                    catch (Exception)
                    {
                    }

                    Result = null;

                    IsPreloaded = false;
                    MemoryManagementTask = null;
                    PreloadTask = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DataSourceBuilder()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support

        #endregion Methods
    }
}
﻿using Newtonsoft.Json;
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

        public List<ColumnRegistration> Registrations = new List<ColumnRegistration>();
        private readonly List<ITransform> Transformations = new List<ITransform>();
        public ColumnRegistration TableKey { get; set; }

        #endregion Fields

        #region Properties

        private readonly List<IRouteConstraint> routeConstraint = new List<IRouteConstraint>();
        private DataTable TempTable;
        public AnalysisResults Result { get; set; } = new AnalysisResults();

        [JsonIgnore]
        public IEnumerable<IRouteConstraint> RouteConstraints => this.routeConstraint;

        #endregion Properties

        #region Classes

        public DataSourceSettings Settings = new DataSourceSettings();

        #endregion Classes

        public static JsonSerializer DefaultJsonSerializer => new JsonSerializer
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            TypeNameHandling = TypeNameHandling.Auto
        };

        public static JsonSerializerSettings DefaultSerializerSettings => new JsonSerializerSettings()
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
            this.JsonSerializer = JsonSerializer.Create(this.JsonSerializerSettings);
        }

        public DataSourceBuilder(DataTable dt) : this()
        {
            this.TempTable = dt;
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

            if (memoryManagementStyle == MemoryManagementStyle.NoCache || memoryManagementStyle == MemoryManagementStyle.Unmanaged)
            {
                DiskNode.CacheNodes = false;
            }
            else
            {
                DiskNode.CacheNodes = true;
            }

            DiskNode.MaxCacheCount = maxCacheCount;

            FileStream fstream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            LockedNodeFileStream stream = new LockedNodeFileStream(fstream);

            long JsonOffset;
            byte[] jbytes = new byte[8];

            fstream.Read(jbytes, 0, 8);

            JsonOffset = BitConverter.ToInt64(jbytes, 0);

            fstream.Seek(JsonOffset, SeekOrigin.Begin);

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

            DiskNode virtualRoot = new DiskNode(stream, DiskNode.HEADER_BYTES);

            OptimizedRootNode.Flush();
            OptimizedRootNode gNode = new OptimizedRootNode(virtualRoot, toReturn.Settings);

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
            this.routeConstraint.Add(constraint);
        }

        public void Build(string outputFilePath)
        {
            this.Transform();

            using (FileStream fstream = new FileStream(outputFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 1_000_000_00))
            {
                using (LockedNodeFileStream stream = new LockedNodeFileStream(fstream, false))
                {
                    stream.Write((long)0);

                    this.Generate(stream);

                    fstream.Seek(0, SeekOrigin.End);

                    long JsonOffset = stream.Offset;

                    try
                    {
                        string Json = JsonConvert.SerializeObject(this, this.JsonSerializerSettings);

                        stream.Write(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Json)));

                        stream.Seek(0);

                        stream.Write(JsonOffset);

                        stream.Flush();
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }
            }
        }

        public Evaluation Evaluate(DataRow dr, bool MultiThread = true)
        {
            if (dr is null)
            {
                throw new ArgumentNullException(nameof(dr));
            }

            Dictionary<string, string> toEvaluate = new Dictionary<string, string>();

            foreach (DataColumn dc in dr.Table.Columns)
            {
                toEvaluate.Add(dc.ColumnName, dr[dc].ToString());
            }

            return this.Evaluate(toEvaluate, MultiThread);
        }

        public Evaluation Evaluate(Dictionary<string, string> dataRow, bool MultiThread = true)
        {
            Evaluation evaluation = new Evaluation(this.Transform(dataRow), this.Result)
            {
                //evaluation.Score = this.Result.BaseRate;

                Result = this.Result,
                InputData = dataRow
            };

            int[] vints = evaluation.DataRow.ToArray();

            for (int index = 0; index < vints.Length - 1; index++)
            {
                string k = this.Registrations[index].Header;
                string v = this.Registrations[index].Column.Display(vints[index]);

                evaluation.CalculatedData.Add(k, v);
            }

            evaluation.CalculatedData.Add(this.TableKey.Header, this.TableKey.Column.Display(vints[vints.Length - 1]));

            INode rootNode = this.Result.RootNode;

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

            object rootLock = new object();

            NodeSetGraph graph = new NodeSetGraph(this, this.Settings.NodeEnumProgress);

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

            object offsetLock = new object();

            outputStream.Seek(CurrentNodeOffset);

            int threads = Environment.ProcessorCount * 1;

            this.Result.PositiveIndicators = this.Result.RawData.Rows.Where(r => r.MatchesOutput).Count();
            this.Result.TotalRows = this.Result.RawData.RowCount;

            bool generatingNodes = true;

            ConcurrentQueue<MemoryNodeFileStream> flushCommands = new ConcurrentQueue<MemoryNodeFileStream>();
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

            SerializationResults results = new SerializationResults();

            bool stillRunning = true;
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
                    await Task.Delay(1000);
                }
            });

            Parallel.ForEach(graph, new ParallelOptions()
            {
                MaxDegreeOfParallelism = threads
            }, nodeSetData =>
            {
                int nodeseti = 0;
                double nodesetc = 0;

                MemoryNode thisRoot = new MemoryNode(-1, ushort.MaxValue, nodeSetData[0].Values, this.Result.RawData.RowCount);

                IEnumerable<MemoryNode> thisRootList = new List<MemoryNode> { thisRoot };

                nodesetc = nodeSetData.Count;

                double dataRowc = this.Result.RawData.RowCount;

                thisRoot.MatchingRows = this.Result.RawData.Rows.ToList();

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
                            MemoryNode n = new MemoryNode(ColumnIndex, i, childCount, (ushort)node.MatchingRows.Count);

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

                this.Result.RegisterTree(thisRoot, this);

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
                this.Result.RootNode = new DiskNode(outputStream, DiskNode.HEADER_BYTES);
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
                string next = $"{this.Registrations[toCheck.Header].Header}:{ this.Registrations[toCheck.Header].Column.Display(toCheck.Value)}";

                if (!string.IsNullOrEmpty(toReturn))
                {
                    toReturn = $"{next} => {toReturn}";
                }
                else
                {
                    toReturn = next;
                }

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

                    cKey.TrimRight();

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
            this.ManagedMemoryStream = new FileStream(Engine, FileMode.Open, FileAccess.Read, FileShare.Read);

            byte[] offsetBytes = new byte[DiskNode.HEADER_BYTES];

            this.ManagedMemoryStream.Read(offsetBytes, 0, offsetBytes.Length);

            long jsonOffset = offsetBytes.GetLong(0);
            long SortOffset = offsetBytes.GetLong(8);

            this.ManagedMemoryStream.Seek(SortOffset, SeekOrigin.Begin);

            if (memoryManagementStyle.HasFlag(MemoryManagementStyle.Preload))
            {
                if (this.PreloadTask is null)
                {
                    this.PreloadTask = Task.Run(() => this.PreloadFunc(this.ManagedMemoryStream, SortOffset, jsonOffset, memoryManagementStyle));
                }

                await this.PreloadTask;
            }

            if (this.MemoryManagementTask is null)
            {
                this.MemoryManagementTask = new Task(() =>
                {
                    do
                    {
                        if (this.Disposing)
                        {
                            return;
                        }
                        try
                        {
                            this.PreloadFunc(this.ManagedMemoryStream, SortOffset, jsonOffset, memoryManagementStyle);
                        }
                        catch (Exception ex)
                        {
                            Penguin.Debugging.StaticLogger.Log("Prelod function failed to execute...");
                            Penguin.Debugging.StaticLogger.Log(ex);
                        }

                        Task.Delay(Settings.PreloadTimeoutMs).Wait();
                    } while (true);
                });

                this.MemoryManagementTask.Start();
            }
        }

        public async void PreloadFunc(FileStream stream, long SortOffset, long JsonOffset, MemoryManagementStyle memoryManagementStyle)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (this.Disposing)
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

                Log($"{this.Settings.MinFreeMemory} Min Free Memory");

                Log($"{this.Settings.RangeFreeMemory} Range Free Memory");

                Log($"Memory management mode {memoryManagementStyle}");

                if (memoryManagementStyle.HasFlag(MemoryManagementStyle.Preload))
                {
                    while (freeMem > this.Settings.MinFreeMemory + this.Settings.RangeFreeMemory && DiskNode.CurrentCacheCount < DiskNode.MaxCacheCount)
                    {
                        Log($"Found Free Memory. Filling...");

                        for (int i = 0; i < this.Settings.PreloadChunkSize / 2; i++)
                        {
                            if (this.Disposing)
                            {
                                return;
                            }

                            if (stream.Position >= JsonOffset)
                            {
                                return;
                            }

                            byte[] thisNodeBytes = new byte[8];

                            stream.Read(thisNodeBytes, 0, thisNodeBytes.Length);

                            DiskNode _ = DiskNode.LoadNode(DiskNode._backingStream, thisNodeBytes.GetLong(0), true);

                            if (DiskNode.CurrentCacheCount >= DiskNode.MaxCacheCount)
                            {
                                break;
                            }
                        }

                        freeMem = SystemInterop.Memory.Status.ullAvailPhys;
                    }
                }

                if (freeMem < this.Settings.MinFreeMemory)
                {
                    int chunkBytes = this.Settings.PreloadChunkSize * 8;

                    while (freeMem < this.Settings.MinFreeMemory + this.Settings.RangeFreeMemory)
                    {
                        if (this.Disposing)
                        {
                            return;
                        }

                        Log($"Need more memory... Clearing cache.");
                        DiskNode.ClearCache();

                        Log($"Collecting Garbage...");
                        GC.Collect();

                        Log($"Waiting...");
                        Task.Delay(1000).Wait();

                        freeMem = SystemInterop.Memory.Status.ullAvailPhys;

                        Log($"{freeMem} Free memory");

                        if (freeMem > this.Settings.MinFreeMemory || stream.Position == SortOffset || !memoryManagementStyle.HasFlag(MemoryManagementStyle.Preload))
                        {
                            Log($"Nothing left to do.");
                            return;
                        }

                        Log($"Reducing managed nodes...");

                        long oldPost = stream.Position;

                        long newPos = Math.Max(SortOffset, stream.Position - chunkBytes);

                        stream.Seek(newPos, SeekOrigin.Begin);

                        for (int i = 0; i < this.Settings.PreloadChunkSize; i++)
                        {
                            if (this.Disposing)
                            {
                                return;
                            }

                            byte[] thisBlock = new byte[8];

                            stream.Read(thisBlock, 0, thisBlock.Length);

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

                        stream.Seek(newPos, SeekOrigin.Begin);
                    }
                }

                this.IsPreloaded = true;
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
            ColumnRegistration r = new ColumnRegistration() { Header = ColumnName, Column = registration };

            if (registration is Key)
            {
                this.TableKey = r;
            }
            else
            {
                this.Registrations.Add(r);
            }
        }

        public void RegisterColumn<T>(params string[] ColumnNames) where T : IDataColumn
        {
            foreach (string ColumnName in ColumnNames)
            {
                this.RegisterColumn(ColumnName, Activator.CreateInstance<T>());
            }
        }

        public void RegisterTransformation(ITransform transform)
        {
            this.Transformations.Add(transform);
        }

        /// <summary>
        /// Runs registered table transformations to create the final analysis table
        /// </summary>
        public void Transform() //I really feel like transforms and column transforms should be sequential (interlaced) in order added
        {
            Penguin.Debugging.StaticLogger.Log("Applying transformations");

            this.Result.RawData = this.Transform(this.TempTable, true);

            this.TempTable = null;
        }

        public TypelessDataRow Transform(Dictionary<string, string> dataRow) //I really feel like transforms and column transforms should be sequential (interlaced) in order added
        {
            if (dataRow is null)
            {
                throw new ArgumentNullException(nameof(dataRow));
            }

            using DataTable dt = new DataTable();

            List<object> values = new List<object>();

            foreach (KeyValuePair<string, string> kvp in dataRow)
            {
                dt.Columns.Add(new DataColumn(kvp.Key));
                values.Add(kvp.Value);
            }

            dt.Rows.Add(values.ToArray());

            return this.Transform(dt, false).Rows.First();
        }

        private IEnumerable<IRouteConstraint> RouteViolations(LongByte Key)
        {
            foreach (IRouteConstraint constraint in this.routeConstraint) //Move this check to the interface (NOTEXC)
            {
                if (constraint.Key == 0)
                {
                    constraint.SetKey(this.Registrations.ToArray());
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

            foreach (ITransform transform in this.Transformations)
            {
                dt = transform.TransformTable(dt);

                foreach (DataRow dr in dt.Rows)
                {
                    transform.TransformRow(dr);
                }
            }

            foreach (ITransform transform in this.Transformations)
            {
                transform.Cleanup(dt);
            }

            this.Settings.PostTransform?.Invoke(dt);

            toReturn = new TypelessDataTable(dt.Rows.Count);

            if (seedRegistrations) // We only want to seed on the initial generation
            {                      // Seeding on an individual transform would blow away
                                   // the values since all instances would have a count of 1

                foreach (ColumnRegistration registration in this.Registrations)
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
                List<int> values = new List<int>();

                foreach (ColumnRegistration registration in this.Registrations)
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

        private bool disposedValue = false; // To detect redundant calls

        private bool Disposing = false;

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.Disposing = true;

                    try
                    {
                        if (this.PreloadTask?.IsCompleted ?? false)
                        {
                            this.MemoryManagementTask?.Dispose();
                        }
                    }
                    catch (Exception)
                    {
                    }

                    try
                    {
                        if (this.PreloadTask?.IsCompleted ?? false)
                        {
                            this.PreloadTask?.Dispose();
                        }
                    }
                    catch (Exception)
                    {
                    }

                    try
                    {
                        this.ManagedMemoryStream?.Dispose();
                    }
                    catch (Exception)
                    {
                    }

                    this.ManagedMemoryStream = null;

                    foreach (ColumnRegistration x in this.Registrations)
                    {
                        x.Dispose();
                    }

                    this.Registrations.Clear();

                    foreach (ITransform x in this.Transformations)
                    {
                        x.Dispose();
                    }

                    this.Transformations.Clear();

                    foreach (IRouteConstraint x in this.routeConstraint)
                    {
                        x.Dispose();
                    }

                    this.routeConstraint.Clear();

                    try
                    {
                        this.Result.Dispose();
                    }
                    catch (Exception)
                    {
                    }

                    this.Result = null;

                    this.IsPreloaded = false;
                    this.MemoryManagementTask = null;
                    this.PreloadTask = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
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
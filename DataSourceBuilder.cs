using Newtonsoft.Json;
using Penguin.Analysis.Constraints;
using Penguin.Analysis.DataColumns;
using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using Penguin.Analysis.Transformations;
using Penguin.IO.Extensions;
using System;
using System.Collections;
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

        #endregion Fields

        #region Properties

        public AnalysisResults Result { get; set; } = new AnalysisResults();

        private List<IRouteConstraint> RouteConstraints { get; set; } = new List<IRouteConstraint>();

        private DataTable TempTable { get; set; }

        #endregion Properties

        #region Classes

        public DataSourceSettings Settings = new DataSourceSettings();

        public class DataSourceSettings
        {
            public ulong MinFreeMemory { get; set; } = 1_000_000_000;
            public ulong RangeFreeMemory { get; set; } = 500_000_000;

            [JsonIgnore]
            public Action<List<string>, bool> CheckedConstraint = null;

            [JsonIgnore]
            public Action<INode> TrimmedNode = null;

            public int NodeFlushDepth { get; set; } = 0;
            #region Classes

            public ResultSettings Results = new ResultSettings();

            public class ResultSettings
            {
                #region Properties

                /// <summary>
                /// Only build trees that contain positive output matches
                /// </summary>
                public bool MatchOnly { get; set; } = false;

                /// <summary>
                /// The minimum total times a route must be matched to be considered
                /// </summary>
                public int MinimumHits { get; set; } = 5;

                /// <summary>
                /// Anything with a variance off the base rate below this amount will not be considered a predictor and will be left off the tree
                /// </summary>
                public float MinimumAccuracy { get; set; } = .4f;

                #endregion Properties
            }

            #endregion Classes
        }

        private class NodeSetCollection : IList<NodeSet>
        {
            private List<NodeSet> nodeSets;

            public int Count => ((IList<NodeSet>)this.nodeSets).Count;

            public bool IsReadOnly => ((IList<NodeSet>)this.nodeSets).IsReadOnly;

            public NodeSet this[int index] { get => ((IList<NodeSet>)this.nodeSets)[index]; set => ((IList<NodeSet>)this.nodeSets)[index] = value; }

            public NodeSetCollection(IEnumerable<NodeSet> set)
            {
                this.nodeSets = set.ToList();
            }

            private static ConcurrentDictionary<sbyte, NodeSet> DefinedSets = new ConcurrentDictionary<sbyte, NodeSet>();

            public NodeSetCollection(IEnumerable<(sbyte columnIndex, int[] values)> set)
            {
                this.nodeSets = new List<NodeSet>();

                foreach ((sbyte columnIndex, int[] values) x in set)
                {
                    if (!DefinedSets.TryGetValue(x.columnIndex, out NodeSet n))
                    {
                        n = new NodeSet(x);
                        DefinedSets.TryAdd(x.columnIndex, n);
                    }

                    this.nodeSets.Add(n);
                }
            }

            public NodeSetCollection()
            {
                this.nodeSets = new List<NodeSet>();
            }

            public int IndexOf(NodeSet item)
            {
                return ((IList<NodeSet>)this.nodeSets).IndexOf(item);
            }

            public void Insert(int index, NodeSet item)
            {
                ((IList<NodeSet>)this.nodeSets).Insert(index, item);
            }

            public void RemoveAt(int index)
            {
                ((IList<NodeSet>)this.nodeSets).RemoveAt(index);
            }

            public void Add(NodeSet item)
            {
                ((IList<NodeSet>)this.nodeSets).Add(item);
            }

            public void Clear()
            {
                ((IList<NodeSet>)this.nodeSets).Clear();
            }

            public bool Contains(NodeSet item)
            {
                return ((IList<NodeSet>)this.nodeSets).Contains(item);
            }

            public void CopyTo(NodeSet[] array, int arrayIndex)
            {
                ((IList<NodeSet>)this.nodeSets).CopyTo(array, arrayIndex);
            }

            public bool Remove(NodeSet item)
            {
                return ((IList<NodeSet>)this.nodeSets).Remove(item);
            }

            public IEnumerator<NodeSet> GetEnumerator()
            {
                return ((IList<NodeSet>)this.nodeSets).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IList<NodeSet>)this.nodeSets).GetEnumerator();
            }

            public static implicit operator NodeSetCollection(List<NodeSet> n)
            {
                return new NodeSetCollection(n);
            }

            public static bool operator ==(NodeSetCollection obj1, NodeSetCollection obj2)
            {
                if (ReferenceEquals(obj1, obj2))
                {
                    return true;
                }

                if (obj1 is null || obj2 is null)
                {
                    return false;
                }

                if (obj1.Count != obj2.Count)
                {
                    return false;
                }

                for (int i = 0; i < obj1.Count; i++)
                {
                    if (!obj2.Contains(obj1.ElementAt(i)))
                    {
                        return false;
                    }
                }

                return true;
            }

            // this is second one '!='
            public static bool operator !=(NodeSetCollection obj1, NodeSetCollection obj2)
            {
                return !(obj1 == obj2);
            }

            public bool Equals(NodeSetCollection other)
            {
                if (other is null)
                {
                    return false;
                }
                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                return this == other;
            }

            public override int GetHashCode()
            {
                return this.nodeSets.Sum(n => n.ColumnIndex);
            }

            public override bool Equals(object obj)
            {
                if (obj is null)
                {
                    return false;
                }
                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                return obj.GetType() == this.GetType() && this.Equals((NodeSetCollection)obj);
            }
        }

        private class NodeSet
        {
            #region Properties

            public sbyte ColumnIndex { get; set; }

            public int[] Values { get; set; }

            #endregion Properties

            #region Constructors

            public NodeSet((sbyte columnIndex, int[] values) r) : this(r.columnIndex, r.values)
            {
            }

            public override int GetHashCode()
            {
                return this.ColumnIndex;
            }

            public NodeSet(sbyte columnIndex, int[] values)
            {
                this.ColumnIndex = columnIndex;
                this.Values = values.ToArray();
            }
            public static bool operator ==(NodeSet obj1, NodeSet obj2)
            {
                if (ReferenceEquals(obj1, obj2))
                {
                    return true;
                }

                if (obj1 is null || obj2 is null)
                {
                    return false;
                }

                return obj1.ColumnIndex == obj2.ColumnIndex;
            }

            // this is second one '!='
            public static bool operator !=(NodeSet obj1, NodeSet obj2)
            {
                return !(obj1 == obj2);
            }

            public bool Equals(NodeSet other)
            {
                if (other is null)
                {
                    return false;
                }
                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                return this.ColumnIndex == other.ColumnIndex;
            }

            public override bool Equals(object obj)
            {
                if (obj is null)
                {
                    return false;
                }
                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                return obj.GetType() == this.GetType() && this.Equals((NodeSet)obj);
            }


            #endregion Constructors
        }

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

        [JsonIgnore]
        public JsonSerializerSettings JsonSerializerSettings { get; set; } = DefaultSerializerSettings;

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

        #region Constructors

        public DataSourceBuilder(string FileName) : this(new FileInfo(FileName).ReadToDataTable())
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

        public void AddRouteConstraint(IRouteConstraint constraint)
        {
            this.RouteConstraints.Add(constraint);
        }

        [JsonIgnore]
        public Task PreloadTask { get; private set; }

        [JsonIgnore]
        public Task MemoryManagementTask { get; private set; }

        [JsonIgnore]
        public bool IsPreloaded { get; private set; }

        [Flags]
        public enum MemoryManagementStyle
        {
            None = 0,
            Preload = 1,
            MemoryFlush = 2,
            PreloadAndFlush = 3
        }

        public static DataSourceBuilder Deserialize(string FilePath, MemoryManagementStyle memoryManagementStyle = MemoryManagementStyle.MemoryFlush, JsonSerializerSettings jsonSerializerSettings = null)
        {
            DiskNode._backingStream = null;

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

            string Json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(System.Text.Encoding.UTF8.GetString(b64Bytes)));

            DataSourceBuilder toReturn = JsonConvert.DeserializeObject<DataSourceBuilder>(Json, jsonSerializerSettings ?? DefaultSerializerSettings);

            DiskNode virtualRoot = new DiskNode(stream, DiskNode.HeaderBytes);

            OptimizedRootNode gNode = new OptimizedRootNode(virtualRoot);

            toReturn.Result.RootNode = gNode;



            if (memoryManagementStyle != MemoryManagementStyle.None)
            {
                toReturn.Preload(FilePath, memoryManagementStyle);
            }

            return toReturn;
        }

        private static string MemLog = DateTime.Now.ToString("yyyyMMddHHmmss") + ".log";
        public async void PreloadFunc(FileStream stream, long SortOffset, long JsonOffset, MemoryManagementStyle memoryManagementStyle)
        {
            if (Disposing)
            {
                return;
            }

            const int Chunks = 15000;

            static void Log(string toLog)
            {
                string toWrite = $"[{DateTime.Now.ToString("yyyy MM dd HH:mm:ss")}]: {toLog}";
                Debug.WriteLine(toWrite);
                Console.WriteLine(toWrite);
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
                    while (freeMem > this.Settings.MinFreeMemory + this.Settings.RangeFreeMemory)
                    {
                        Log($"Found Free Memory. Filling...");

                        for (int i = 0; i < Chunks / 2; i++)
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

                            stream.Read(thisNodeBytes, 0, thisNodeBytes.Length);

                            DiskNode _ = DiskNode.LoadNode(DiskNode._backingStream, thisNodeBytes.GetLong(0), true);
                        }

                        freeMem = SystemInterop.Memory.Status.ullAvailPhys;
                    }
                }

                if (freeMem < this.Settings.MinFreeMemory)
                {



                    int chunkBytes = Chunks * 8;

                    while (freeMem < this.Settings.MinFreeMemory + this.Settings.RangeFreeMemory)
                    {
                        if (Disposing)
                        {
                            return;
                        }

                        Log($"Need more memory... Clearing cache.");
                        DiskNode.ClearCache(memoryManagementStyle);

                        Log($"Collecting Garbage...");
                        GC.Collect();

                        Log($"Waiting...");
                        Task.Delay(5000).Wait();

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

                        for (int i = 0; i < Chunks; i++)
                        {
                            if (Disposing)
                            {
                                return;
                            }

                            byte[] thisBlock = new byte[8];

                            stream.Read(thisBlock, 0, thisBlock.Length);

                            long offset = thisBlock.GetLong(0);

                            if (DiskNode.MemoryManaged.TryGetValue(offset, out DiskNode dn))
                            {
                                DiskNode.MemoryManaged.Remove(offset);
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

        FileStream ManagedMemoryStream;

        public async void Preload(string Engine, MemoryManagementStyle memoryManagementStyle)
        {
            ManagedMemoryStream = new FileStream(Engine, FileMode.Open, FileAccess.Read, FileShare.Read);


            byte[] offsetBytes = new byte[DiskNode.HeaderBytes];

            ManagedMemoryStream.Read(offsetBytes, 0, offsetBytes.Length);

            long jsonOffset = offsetBytes.GetLong(0);
            long SortOffset = offsetBytes.GetLong(8);

            ManagedMemoryStream.Seek(SortOffset, SeekOrigin.Begin);

            if (memoryManagementStyle.HasFlag(MemoryManagementStyle.Preload))
            {
                if (this.PreloadTask is null)
                {
                    this.PreloadTask = Task.Run(() => this.PreloadFunc(ManagedMemoryStream, SortOffset, jsonOffset, memoryManagementStyle));
                }

                await this.PreloadTask;
            }

            if (this.MemoryManagementTask is null)
            {
                this.MemoryManagementTask = new Task(() =>
                {


                    do
                    {
                    if(Disposing)
                    {
                        return;
                    }
                        try
                        {
                            this.PreloadFunc(ManagedMemoryStream, SortOffset, jsonOffset, memoryManagementStyle);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Prelod function failed to execute...");
                            Console.WriteLine(ex.Message);
                        }

                        Task.Delay(5000).Wait();
                    } while (true);
                });

                this.MemoryManagementTask.Start();
            }

        }

        public bool IfValid(List<string> Headers, Action<List<string>> HeaderAction = null)
        {
            while (!this.ValidateRouteConstraints(Headers) && Headers.Count > 0)
            {
                Headers = Headers.Take(Headers.Count - 1).ToList();

                if (Headers.Count == 0)
                {
                    return false;
                }
            }

            HeaderAction?.Invoke(Headers);

            return true;
        }

        public void Generate(LockedNodeFileStream outputStream)
        {


            ScreenBuffer.Clear();

            ScreenBuffer.ReplaceLine($"Building complex tree", 0);



            int KeyIndex = this.Registrations.IndexOf(this.Registrations.Single(r => r.Column.GetType() == typeof(Key)));

            object rootLock = new object();

            this.Result.ExpectedMatches = Math.Pow(2, (this.Registrations.Count - 1));

            NodeSetCollection[] rawGraph = this.BuildComplexTree(this.Registrations
                                                                     //.OrderByDescending(r => r.Column.GetOptions().Count())
                                                                     .Where(r => r.Column.GetType() != typeof(Key))
                                                                     .Select(r =>
                                                                        ((sbyte)this.Registrations.IndexOf(r),
                                                                         r.Column.GetOptions()
                                                                                 .ToArray())
                                                                        ).ToArray()).Select(n => new NodeSetCollection(n)).ToArray();

            HashSet<NodeSetCollection> graph = new HashSet<NodeSetCollection>();

            ScreenBuffer.ReplaceLine($"Applying Constraints", 0);

            foreach (NodeSetCollection nodeSetCollection in rawGraph)
            {
                List<string> Headers = nodeSetCollection.Select(n => this.Registrations[n.ColumnIndex].Header).ToList();

                if (!this.IfValid(Headers, (headers) =>
                 {
                     NodeSetCollection nSet = nodeSetCollection.Take(headers.Count).ToList();

                     if (graph.Add(nSet))
                     {
                         this.Settings.CheckedConstraint?.Invoke(headers, true);
                         this.Result.TotalRoutes++;
                     }
                     else
                     {
                         this.Result.ExpectedMatches--;
                     }
                 }))
                {
                    this.Result.ExpectedMatches--;
                }

                bool Valid = this.ValidateRouteConstraints(Headers);

            }

            rawGraph = null;

            ScreenBuffer.ReplaceLine($"Building decision tree", 0);
            ScreenBuffer.AutoFlush = false;

            long graphi = 0;

            long graphc = graph.Count;

            long RootChildListOffset = DiskNode.HeaderBytes + DiskNode.NodeSize;

            outputStream.Seek(DiskNode.HeaderBytes + 24);

            outputStream.Write((sbyte)-1);
            outputStream.Write((long)-1);

            outputStream.Seek(RootChildListOffset - 4);

            outputStream.Write((int)graphc);

            long CurrentNodeOffset = (graphc * DiskNode.NextSize) + RootChildListOffset;

            object offsetLock = new object();

            outputStream.Seek(CurrentNodeOffset);

            int DisplayLines = 2;
            int threads = Environment.ProcessorCount * 1;

            this.Result.PositiveIndicators = this.Result.RawData.Rows.Where(r => r.MatchesOutput).Count();
            this.Result.TotalRows = this.Result.RawData.RowCount;

            object[] screenLocks = new object[threads];

            for (int i = 0; i < threads; i++)
            {
                screenLocks[i] = new object();
            }

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
                        { }
                        while (!command.Ready)
                        {
                            System.Threading.Thread.Sleep(50);
                        }

                        outputStream.Write(command.ToArray());
                    }

                    System.Threading.Thread.Sleep(50);
                } while (generatingNodes || flushCommands.Any());
            });

            SerializationResults results = new SerializationResults();

            Parallel.ForEach(graph, new ParallelOptions()
            {
                MaxDegreeOfParallelism = threads
            }, nodeSetData =>
            {
                int displaySlot = 0;

                while (!Monitor.TryEnter(screenLocks[displaySlot]))
                {
                    displaySlot++;
                    if (displaySlot == screenLocks.Length)
                    {
                        displaySlot = 0;
                    }
                }

                int nodeseti = 0;
                double nodesetc = 0;
                int RootLine = (displaySlot % threads) * DisplayLines;

                Node thisRoot = new Node(-1, -1, nodeSetData[0].Values.Length, this.Result.RawData.RowCount);

                IEnumerable<Node> thisRootList = new List<Node> { thisRoot };

                nodesetc = nodeSetData.Count;

                ScreenBuffer.ReplaceLine($"--------Graph: [{graphi + 1}/{graphc}]", RootLine);

                double dataRowi = 0;
                double dataRowc = this.Result.RawData.RowCount;
                double thisProgress = 0;
                double lastProgress = 0;

                //ScreenBuffer.ReplaceLine($"Seeding...", RootLine + 1);
                ScreenBuffer.Flush();

                thisRoot.MatchingRows = this.Result.RawData.Rows.ToList();

                for (nodeseti = 0; nodeseti < nodesetc; nodeseti++)
                {
                    dataRowi = 0;
                    sbyte ColumnIndex = nodeSetData[nodeseti].ColumnIndex;
                    int[] Values = nodeSetData[nodeseti].Values;
                    int childCount = 0;
                    dataRowc = Values.Length * thisRootList.Count();
                    thisProgress = 0;
                    lastProgress = 0;
                    double pruned = 0;

                    //ScreenBuffer.ReplaceLine($"--------Graph: [{graphi + 1}/{graphc}] NodeSet: [{nodeseti + 1}/{nodesetc}] Step: Branching  --------", RootLine);

                    if (nodeseti < nodesetc - 1)
                    {
                        childCount = nodeSetData[nodeseti + 1].Values.Length;
                    }

                    for (int i = 0; i < Values.Length; i++)
                    {
                        foreach (Node node in thisRootList)
                        {
                            thisProgress = Math.Round(dataRowi++ / dataRowc * 100, 2);

                            if (thisProgress != lastProgress)
                            {
                                //string output = $"{string.Format("{0:00.00}", thisProgress)}%; Pruned: {string.Format("{0:00.00}", Math.Round(pruned / dataRowc * 100, 2))}%";

                                //ScreenBuffer.ReplaceLine(output, RootLine + 1);

                                //ScreenBuffer.Flush();
                                lastProgress = thisProgress;
                            }

                            if (!node.AddNext(
                                this,
                                new Node(ColumnIndex,
                                Values[i],
                                childCount,
                                node.MatchingRows.Count),
                                i))
                            {
                                pruned++;
                            }
                        }
                    }

                    foreach (Node n in thisRootList)
                    {
                        n.Trim();
                    }

                    thisRootList = thisRootList.Where(n => n.Next != null).SelectMany(n => n.Next);
                }

                foreach (Node n in thisRootList)
                {
                    n.Trim();
                }


                //thisRoot.Header = -1;

                thisRoot.FillNodeData(this.Result.PositiveIndicators, this.Result.RawData.RowCount);

                thisRoot.TrimNodesWithNoBearing(this);

                thisRoot.Trim();

                
                this.Result.RegisterTree(thisRoot);

                long thisNodeIndex = graphi++;
                MemoryNodeFileStream memCache;

                lock (offsetLock)
                {
                    memCache = new MemoryNodeFileStream(CurrentNodeOffset);
                    CurrentNodeOffset += thisRoot.GetLength();
                    flushCommands.Enqueue(memCache);
                }

                results.AddRange(thisRoot.Serialize(memCache, DiskNode.HeaderBytes));

                memCache.Ready = true;

                Monitor.Exit(screenLocks[displaySlot]);

                thisRoot = null;

                thisRootList = null;

                ScreenBuffer.Flush();
            });

            generatingNodes = false;

            flushToDisk.Wait();

            IEnumerable<NodeMeta> PreloadResults = results.OrderByDescending(n => n.Matches);
            IEnumerable<NodeMeta> RootOffsets = results.Where(n => n.Root).OrderBy(n => n.Header).ThenByDescending(n => n.Matches);



            long sortPos = outputStream.Offset;

            outputStream.Seek(RootChildListOffset);

            byte[] v = new byte[] { 0, 0, 0, 0 };

            foreach (long l in RootOffsets.Select(n => n.Offset))
            {
                outputStream.Write(l);
                outputStream.Write(v);
            }

            outputStream.Seek(8);
            outputStream.Write(sortPos);

            outputStream.Seek(sortPos);



            foreach (NodeMeta nm in PreloadResults)
            {
                outputStream.Write(nm.Offset);
            }


            this.Result.RootNode = new DiskNode(outputStream, DiskNode.HeaderBytes);
        }


        public Evaluation Evaluate(DataRow dr)
        {
            Dictionary<string, string> toEvaluate = new Dictionary<string, string>();

            foreach (DataColumn dc in dr.Table.Columns)
            {
                toEvaluate.Add(dc.ColumnName, dr[dc].ToString());
            }

            return this.Evaluate(toEvaluate);


        }

        public Evaluation Evaluate(Dictionary<string, string> dataRow)
        {
            Evaluation evaluation = new Evaluation(this.Transform(dataRow), this.Result)
            {
                //evaluation.Score = this.Result.BaseRate;

                Result = this.Result
            };

            INode rootNode = this.Result.RootNode;

            rootNode.Evaluate(evaluation);

            return evaluation;
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

        public void RegisterColumn(string ColumnName, IDataColumn registration)
        {
            this.Registrations.Add(new ColumnRegistration() { Header = ColumnName, Column = registration });
        }

        public void RegisterColumn<T>(params string[] ColumnNames) where T : IDataColumn
        {
            foreach (string ColumnName in ColumnNames)
            {
                this.Registrations.Add(new ColumnRegistration() { Header = ColumnName, Column = (T)Activator.CreateInstance(typeof(T), this) });
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
            Console.WriteLine("Applying transformations");

            this.Result.RawData = this.Transform(this.TempTable);

            this.TempTable = null;
        }

        public TypelessDataRow Transform(Dictionary<string, string> dataRow) //I really feel like transforms and column transforms should be sequential (interlaced) in order added
        {
            using DataTable dt = new DataTable();

            List<object> values = new List<object>();

            foreach (KeyValuePair<string, string> kvp in dataRow)
            {
                dt.Columns.Add(new DataColumn(kvp.Key));
                values.Add(kvp.Value);
            }

            dt.Rows.Add(values.ToArray());

            return this.Transform(dt).Rows.First();
        }

        private IList<(sbyte ColumnIndex, T[] Values)>[] BuildComplexTree<T>((sbyte ColumnIndex, T[] Values)[] ColumnData)
        {
            double Hc = Math.Pow(2, ColumnData.Length);

            int Hl = (int)(Hc / 2);

            IList<(sbyte ColumnIndex, T[] Values)>[] graph = new IList<(sbyte ColumnIndex, T[] Values)>[Hl];

            for (int Hi = 0; Hi < Hc; Hi += 2)
            {
                int sbits = 0;

                int Hb = Hi + 1;

                while (Hb != 0)
                {
                    if ((Hb & 1) != 0)
                    {
                        sbits++;
                    }

                    Hb >>= 1;
                }

                graph[Hi / 2] = new List<(sbyte ColumnIndex, T[] Values)>(sbits);

                for (int Wi = ColumnData.Length - 1; Wi >= 0; Wi--)
                {
                    if ((((Hi + 1) >> Wi) & 1) != 0)
                    {
                        graph[Hi / 2].Add(ColumnData[Wi]);
                    }
                }
            }

            return graph;
        }

        private TypelessDataTable Transform(DataTable dt)
        {
            TypelessDataTable toReturn;

            foreach (ITransform transform in this.Transformations)
            {
                dt = transform.TransformTable(dt);

                foreach (DataRow dr in dt.Rows)
                {
                    transform.TransformRow(dr);
                }

                transform.Cleanup(dt);
            }

            toReturn = new TypelessDataTable(dt.Rows.Count);

            ColumnRegistration KeyRegistration = this.Registrations.Single(r => r.Column.GetType() == typeof(Key));
            int KeyIndex = this.Registrations.IndexOf(KeyRegistration);

            foreach (DataRow dr in dt.Rows)
            {
                List<int> values = new List<int>();

                foreach (ColumnRegistration registration in this.Registrations)
                {
                    values.Add(registration.Column.Transform(dr[registration.Header].ToString(), Bool.GetValue(dr[KeyRegistration.Header].ToString()) == 1));
                }

                toReturn.AddRow(values.ToArray());
            }

            foreach (TypelessDataRow dr in toReturn.Rows)
            {
                dr.MatchesOutput = dr[KeyIndex] == 1;
            }

            return toReturn;
        }

        private bool ValidateRouteConstraints(IEnumerable<string> Headers)
        {
            foreach (IRouteConstraint constraint in this.RouteConstraints)
            {
                if (!constraint.Evaluate(Headers.ToArray()))
                {
                    return false;
                }
            }

            return true;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        private bool Disposing = true;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    Disposing = true;

                    try
                    {
                        this.MemoryManagementTask?.Dispose();
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {
                        this.PreloadTask?.Dispose();
                    }
                    catch (Exception)
                    {

                    }

                    try
                    {
                        ManagedMemoryStream.Dispose();
                    } catch(Exception)
                    {

                    }

                    ManagedMemoryStream = null;

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

                    foreach (IRouteConstraint x in this.RouteConstraints)
                    {
                        x.Dispose();
                    }

                    this.RouteConstraints.Clear();

                    try
                    {
                        this.Result.Dispose();
                    } catch(Exception)
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

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        #endregion Methods
    }
}
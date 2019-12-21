using Newtonsoft.Json;
using Penguin.Analysis.Constraints;
using Penguin.Analysis.DataColumns;
using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using Penguin.Analysis.Transformations;
using Penguin.IO.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Penguin.Analysis
{
    [Serializable]
    public class DataSourceBuilder : IColumnRegistrar
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

        public static class Settings
        {
            #region Classes

            public static class Results
            {
                #region Properties

                /// <summary>
                /// Only build trees that contain positive output matches
                /// </summary>
                public static bool MatchOnly { get; set; } = false;

                /// <summary>
                /// The minimum total times a route must be matched to be considered
                /// </summary>
                public static int MinimumHits { get; set; } = 5;

                /// <summary>
                /// Anything with a variance off the base rate below this amount will not be considered a predictor and will be left off the tree
                /// </summary>
                public static float MinimumWeight { get; set; } = .01f;

                #endregion Properties
            }

            #endregion Classes
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

            public NodeSet(sbyte columnIndex, int[] values)
            {
                this.ColumnIndex = columnIndex;
                this.Values = values.ToArray();
            }

            #endregion Constructors
        }

        #endregion Classes

        public static JsonSerializer DefaultJsonSerializer
        {
            get
            {
                return new JsonSerializer
                {
                    DefaultValueHandling = DefaultValueHandling.Ignore,
                    Formatting = Formatting.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                    ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                    TypeNameHandling = TypeNameHandling.Auto
                };
            }
        }

        [JsonIgnore]
        public JsonSerializerSettings JsonSerializerSettings { get; set; } = DefaultSerializerSettings;

        public static JsonSerializerSettings DefaultSerializerSettings
        {
            get {
                return new JsonSerializerSettings()
                {
                    DefaultValueHandling = DefaultValueHandling.Ignore,
                    Formatting = Formatting.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                    ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                    TypeNameHandling = TypeNameHandling.Auto
                };
            }
        }

        [JsonIgnore]
        public JsonSerializer JsonSerializer { get; set; } 

        #region Constructors

        public DataSourceBuilder(string FileName) : this(new FileInfo(FileName).ReadToDataTable())
        {
            
        }

        public DataSourceBuilder() 
        {
            JsonSerializer = JsonSerializer.Create(JsonSerializerSettings);
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

        public void Serialize(string FilePath)
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            using (FileStream fstream = new FileStream(FilePath,FileMode.CreateNew)) {
                using (LockedNodeFileStream stream = new LockedNodeFileStream(fstream))
                {
                    stream.Write((long)0);

                    Result.BuilderRootNote.Serialize(stream);

                    fstream.Seek(0, SeekOrigin.End);

                    long JsonOffset = stream.Offset;

                    string Json = JsonConvert.SerializeObject(this, JsonSerializerSettings);

                    stream.Write(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Json)));

                    stream.Seek(0);

                    stream.Write(JsonOffset);
                }
            }
        }

        public static DataSourceBuilder Deserialize(string FilePath, JsonSerializerSettings jsonSerializerSettings = null)
        {
            FileStream fstream = new FileStream(FilePath, FileMode.Open);
            LockedNodeFileStream stream = new LockedNodeFileStream(fstream);

            long JsonOffset;
            byte[] jbytes = new byte[8];

            fstream.Read(jbytes, 0, 8);

            JsonOffset = BitConverter.ToInt64(jbytes, 0);

            fstream.Seek(JsonOffset, SeekOrigin.Begin);

            byte[] b64Bytes = new byte[fstream.Length - JsonOffset];
            int b64P = 0;
            int tbyte;
            
            while((tbyte = fstream.ReadByte()) != -1)
            {
                b64Bytes[b64P++] = (byte)tbyte;    
            }

            string Json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(System.Text.Encoding.UTF8.GetString(b64Bytes)));

            DataSourceBuilder toReturn = JsonConvert.DeserializeObject<DataSourceBuilder>(Json, jsonSerializerSettings ?? DefaultSerializerSettings);

            toReturn.Result.RootNode = new DiskNode(stream, 8);

            return toReturn;
        }

        public void BuildOptions()
        {
            ScreenBuffer.Clear();

            ScreenBuffer.ReplaceLine($"Building complex tree", 0);

            int KeyIndex = this.Registrations.IndexOf(this.Registrations.Single(r => r.Column.GetType() == typeof(Key)));

            ConcurrentBag<Node> root = new ConcurrentBag<Node>() { };
            object rootLock = new object();

            this.Result.ExpectedMatches = Math.Pow(2, (this.Registrations.Count - 1));

            foreach (List<string> headerCombo in Combinations.Get(this.Registrations.Select(r => r.Header).ToList()))
            {
                if (this.ValidateRouteConstraints(headerCombo))
                {
                    this.Result.ExpectedMatches--;
                }
            }

            IList<(sbyte ColumnIndex, int[] Values)>[] rawGraph = this.BuildComplexTree(
                                                                this.Registrations
                                                                    .Where(r => r.Column.GetType() != typeof(Key))
                                                                    .Select(r =>
                                                                        ((sbyte)this.Registrations.IndexOf(r),
                                                                         r.Column.GetOptions()
                                                                                 .ToArray())
                                                                        )
                                                                                                                                        .ToArray());

            List<IList<NodeSet>> graph = new List<IList<NodeSet>>(rawGraph.Length);

            ScreenBuffer.ReplaceLine($"Applying Constraints", 0);
            foreach (IList<(sbyte ColumnIndex, int[] Values)> nodeSet in rawGraph)
            {
                List<string> Headers = nodeSet.Select(n => this.Registrations[n.ColumnIndex].Header).ToList();

                if (this.ValidateRouteConstraints(Headers))
                {
                    graph.Add(nodeSet.Select(ns => new NodeSet(ns)).ToList());
                    this.Result.TotalRoutes++;
                }
            }

            rawGraph = null;

            ScreenBuffer.ReplaceLine($"Building decision tree", 0);
            ScreenBuffer.AutoFlush = false;

            double graphi = 0;
            double graphc = graph.Count;

            int DisplayLines = 2;
            int threads = Environment.ProcessorCount * 1;

            this.Result.PositiveIndicators = this.Result.RawData.Rows.Where(r => r.MatchesOutput).Count();
            this.Result.TotalRows = this.Result.RawData.RowCount;

            object[] screenLocks = new object[threads];

            for (int i = 0; i < threads; i++)
            {
                screenLocks[i] = new object();
            }

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

                root.Add(thisRoot);

                IEnumerable<Node> thisRootList = new List<Node> { thisRoot };

                nodesetc = nodeSetData.Count;

                ScreenBuffer.ReplaceLine($"--------Graph: [{graphi + 1}/{graphc}] NodeSet: [0/{nodesetc}] Step: Evaluating --------", RootLine);

                double dataRowi = 0;
                double dataRowc = this.Result.RawData.RowCount;
                double thisProgress = 0;
                double lastProgress = 0;

                ScreenBuffer.ReplaceLine($"Seeding...", RootLine + 1);
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

                    ScreenBuffer.ReplaceLine($"--------Graph: [{graphi + 1}/{graphc}] NodeSet: [{nodeseti + 1}/{nodesetc}] Step: Branching  --------", RootLine);

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
                                string output = $"{string.Format("{0:00.00}", thisProgress)}%; Pruned: {string.Format("{0:00.00}", Math.Round(pruned / dataRowc * 100, 2))}%";

                                ScreenBuffer.ReplaceLine(output, RootLine + 1);

                                ScreenBuffer.Flush();
                                lastProgress = thisProgress;
                            }

                            if (!node.AddNext(
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

                graphi++;

                Monitor.Exit(screenLocks[displaySlot]);
            });

            List<Node> ComplexTree = root.ToList();

            this.Result.RootNode = new Node(-1, -1, ComplexTree.Count, this.Result.RawData.RowCount);

            this.TrimNodesWithNoBearing();

            for (int i = 0; i < ComplexTree.Count; i++)
            {
                this.Result.BuilderRootNote.AddNext(ComplexTree[i], i, false);
            }

            ScreenBuffer.ReplaceLine("Flattening Tree", 0);
            ScreenBuffer.Flush();

            foreach (Node n in this.Result.BuilderRootNote.FullTree())
            {
                if (n.Header == -1)
                { continue; }

                float MissingMatches = this.Result.PositiveIndicators - n.Results[(int)MatchResult.Both];

                float MissingMisses = this.Result.RawData.RowCount - (MissingMatches + n.Results[(int)MatchResult.Route] + n.Results[(int)MatchResult.Both]);

                n.Results[(int)MatchResult.None] = (int)MissingMisses;
                n.Results[(int)MatchResult.Output] = (int)MissingMatches;
            }

            this.Result.BuilderRootNote.Trim();
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
            Evaluation evaluation = new Evaluation(this.Transform(dataRow))
            {
                //evaluation.Score = this.Result.BaseRate;

                Result = this.Result
            };

            this.Result.RootNode.Evaluate(evaluation);

            //evaluation.Score = (float)((evaluation.Score + ((this.Result.ExpectedMatches - evaluation.Score) / 2)) / this.Result.ExpectedMatches);

            return evaluation;
        }

        public string GetNodeName(Node toCheck)
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

        public void Go()
        {
            this.Transform();

            this.BuildOptions();
        }

        public void RegisterColumn(string ColumnName, IDataColumn registration)
        {
            this.Registrations.Add(new ColumnRegistration() { Header = ColumnName, Column = registration });
        }

        public void RegisterColumn<T>(params string[] ColumnNames) where T : IDataColumn
        {
            foreach (string ColumnName in ColumnNames)
            {
                this.Registrations.Add(new ColumnRegistration() { Header = ColumnName, Column = Activator.CreateInstance<T>() });
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

        public int TrimNodesWithNoBearing()
        {
            int total = 0;
            //Going to attempt to trim any nodes with no bearing on the final result here.
            int altered;
            do
            {
                altered = 0;

                foreach (Node N in this.Result.BuilderRootNote.FullTree())
                {
                    if (N.Next is null || !N.Next.Any())
                    {
                        if (Math.Abs(N.Accuracy - this.Result.BaseRate) < Settings.Results.MinimumWeight)
                        {
                            N.ParentNode.RemoveNode(N);
                            altered++;
                            total++;
                        }
                    }
                }
            } while (altered != 0);

            return total;
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

        #endregion Methods
    }
}
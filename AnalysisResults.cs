using Newtonsoft.Json;
using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis
{
    [Serializable]
    public class AnalysisResults : IDisposable
    {
        #region Properties

        public float BaseRate => PositiveIndicators / TotalRows;

        [JsonIgnore]
        public MemoryNode BuilderRootNote
        {
            get => RootNode is MemoryNode n
                    ? n
                    : throw new Exception($"Attempt has been made to access root node that is not of type {nameof(MemoryNode)}. The node type is {RootNode.GetType()}");
            set => RootNode = value;
        }

        public Dictionary<long, int> ColumnInstances { get; set; } = new Dictionary<long, int>();
        public int GraphInstances { get; set; }
        public float PositiveIndicators { get; set; }

        [JsonIgnore]
        public TypelessDataTable RawData { get; set; }

        [JsonIgnore]
        public INode RootNode { get; set; }

        public float TotalRows { get; set; }

        #region IDisposable Support

        private static readonly object RegistrationLock = new();
        private readonly HashSet<long> RegisteredKeys = new();
        private bool disposedValue; // To detect redundant calls

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~AnalysisResults()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }
        internal void RegisterTree(MemoryNode thisRoot, DataSourceBuilder dsb)
        {
            lock (RegistrationLock)
            {
                List<MemoryNode> AllNodes = thisRoot.FullTree().ToList();

                foreach (MemoryNode n in AllNodes)
                {
                    long Key = n.GetKey();

                    if (RegisteredKeys.Add(Key))
                    {
                        GraphInstances++;

                        LongByte lb = new(Key);

                        while (lb > 0)
                        {
                            if (!ColumnInstances.ContainsKey(lb.Value))
                            {
                                ColumnInstances.Add(lb.Value, 1);
                            }
                            else
                            {
                                ColumnInstances[lb.Value]++;
                            }

                            _ = lb.TrimLeft();
                        }
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    RawData = null;

                    DiskNode.DisposeAll();
                    RootNode.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        #endregion IDisposable Support

        #endregion Properties
    }
}
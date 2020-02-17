using Newtonsoft.Json;
using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;

namespace Penguin.Analysis
{
    [Serializable]
    public class AnalysisResults : IDisposable
    {
        #region Properties

        public float BaseRate => this.PositiveIndicators / this.TotalRows;

        [JsonIgnore]
        public Node BuilderRootNote
        {
            get
            {
                if (this.RootNode is Node n)
                {
                    return n;
                }
                else
                {
                    throw new Exception($"Attempt has been made to access root node that is not of type {nameof(Node)}. The node type is {this.RootNode.GetType().ToString()}");
                }
            }
            set => this.RootNode = value;
        }

        public int[] ColumnInstances { get; set; } = new int[256];
        public double ExpectedMatches { get; set; }
        public int GraphInstances { get; set; } = 0;
        public float PositiveIndicators { get; set; }

        [JsonIgnore]
        public TypelessDataTable RawData { get; set; }

        [JsonIgnore]
        public INode RootNode { get; set; }

        public int TotalRoutes { get; set; }
        public float TotalRows { get; set; }

        #region IDisposable Support

        private static object RegustrationLock = new object();
        private bool disposedValue = false; // To detect redundant calls

        private HashSet<int> RegisteredKeys = new HashSet<int>();

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~AnalysisResults()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }
        internal void RegisterTree(Node thisRoot)
        {
            lock (RegustrationLock)
            {
                foreach (Node n in thisRoot.FullTree())
                {
                    int Key = n.GetKey();

                    if (this.RegisteredKeys.Add(Key))
                    {
                        this.GraphInstances++;

                        for (int i = 0; i < 32; i++)
                        {
                            if (((Key >> i) & 1) == 1)
                            {
                                this.ColumnInstances[i]++;
                            }
                        }
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.RawData = null;

                    DiskNode.DisposeAll();
                    this.RootNode.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
            }
        }

        #endregion IDisposable Support

        #endregion Properties
    }
}
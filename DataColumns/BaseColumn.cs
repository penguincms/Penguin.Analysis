using System;
using System.Collections.Generic;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public abstract class BaseColumn : IDataColumn
    {
        #region Methods

        public BaseColumn(DataSourceBuilder sourceBuilder)
        {
            SourceBuilder = sourceBuilder;
        }

        public DataSourceBuilder SourceBuilder { get; set; }

        public virtual string Display(int Value)
        {
            return Value.ToString();
        }

        public abstract IEnumerable<int> GetOptions();

        public abstract int Transform(string input, bool PositiveIndicator);

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected abstract void OnDispose();

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    OnDispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~BaseColumn()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        #endregion Methods
    }
}
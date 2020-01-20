using Penguin.Analysis.DataColumns;
using System;

namespace Penguin.Analysis
{
    [Serializable]
    public class ColumnRegistration : IDisposable
    {
        #region Properties

        public IDataColumn Column { get; set; }
        public string Header { get; set; }

        #endregion Properties

        public override string ToString()
        {
            return $"{this.Header}: {this.GetType()}";
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        Column.Dispose();
                    }
                    catch (Exception)
                    {
                        
                    }
                    
                    Column = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ColumnRegistration()
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
    }
}
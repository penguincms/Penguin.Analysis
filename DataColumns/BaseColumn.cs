using System;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public abstract class BaseColumn : IDataColumn
    {
        #region Methods

        public abstract int OptionCount { get; }
        public virtual bool SeedMe => false;

        protected BaseColumn()
        {
        }

        public virtual string Display(int Value)
        {
            return Value.ToString();
        }

        public abstract void EndSeed();

        public abstract void Seed(string input, bool PositiveIndicator);

        public abstract int Transform(string input);

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

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
                    OnDispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        protected abstract void OnDispose();

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~BaseColumn()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support

        #endregion Methods
    }
}
using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis.Constraints
{
    /// <summary>
    /// Do not allow any possible combination to appear as a route. Useful if one property is derivative of another
    /// </summary>
    [Serializable]
    public class ExclusiveAny : IRouteConstraint
    {
        #region Fields

        private readonly List<string> Headers = new List<string>();

        #endregion Fields

        #region Constructors

        public ExclusiveAny(params string[] headers)
        {
            this.Headers = headers.ToList();
        }

        #endregion Constructors

        #region Methods

        public bool Evaluate(params string[] headers)
        {
            return headers.Count(h => this.Headers.Contains(h)) <= 1;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Headers.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ExclusiveAny()
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
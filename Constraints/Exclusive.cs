using Penguin.Extensions.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis.Constraints
{
    /// <summary>
    /// Do not allow this exact combination to appear as a route. Useful if one property is derivative of another. Also, if only one parameter then force the property to be evaluated alone
    /// </summary>
    [Serializable]
    public class Exclusive : IRouteConstraint
    {
        #region Fields

        private readonly List<string> Headers = new();
        /// <inheritdoc/>

        #endregion Fields

        #region Constructors

        public LongByte Key { get; set; }
        /// <inheritdoc/>

        public Exclusive(params string[] headers)
        {
            Headers = headers != null && headers.Length > 0
                ? headers.ToList()
                : throw new ArgumentException("At least one header must be specified");
        }

        /// <inheritdoc/>

        public override string ToString()
        {
            return $"{nameof(Exclusive)}: " + Headers.Join();
        }

        /// <inheritdoc/>

        #endregion Constructors

        #region Methods

        public bool Evaluate(LongByte key)
        {
            LongByte tlb = Key;

            return (tlb & key) == 0 || key.Count < 2 || (tlb.Count > 1 && tlb.Count < key.Count);
        }

        /// <inheritdoc/>

        public void SetKey(ColumnRegistration[] columns)
        {
            if (columns is null)
            {
                throw new ArgumentNullException(nameof(columns));
            }

            LongByte lb = 0;

            for (int x = 0; x < columns.Length; x++)
            {
                lb.SetBit(x, Headers.Contains(columns[x].Header));
            }

            Key = lb;
        }

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls
        /// <inheritdoc/>

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>

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
        // ~Exclusive()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support

        #endregion Methods
    }
}
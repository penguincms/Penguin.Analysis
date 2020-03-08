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

        private readonly List<string> Headers = new List<string>();

        #endregion Fields

        #region Constructors

        public LongByte Key { get; set; }

        public Exclusive(params string[] headers)
        {
            if (headers.Length > 0)
            {
                this.Headers = headers.ToList();
            }
            else
            {
                throw new ArgumentException("At least one header must be specified");
            }
        }

        public override string ToString()
        {
            return $"{nameof(Exclusive)}: " + this.Headers.Join();
        }
        #endregion Constructors

        #region Methods

        public bool Evaluate(LongByte key)
        {
            LongByte tlb = this.Key;

            return (tlb & key) == 0 || key.Count < 2 || (tlb.Count > 1 && tlb.Count < key.Count);
        }

        public void SetKey(ColumnRegistration[] registrations)
        {
            LongByte lb = 0;

            for (int x = 0; x < registrations.Length; x++)
            {
                lb.SetBit(x, this.Headers.Contains(registrations[x].Header));
            }

            this.Key = lb;
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

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
                    this.Headers.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
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
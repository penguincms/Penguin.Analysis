﻿using Penguin.Extensions.Collections;
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

        private readonly HashSet<string> Headers = new();

        #endregion Fields

        #region Constructors

        public ExclusiveAny(params string[] headers)
        {
            if (headers is null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            foreach (string h in headers)
            {
                _ = Headers.Add(h);
            }
        }

        #endregion Constructors

        private int MaxBit = -1;

        #region Methods

        public LongByte Key { get; set; }

        public bool Evaluate(LongByte key)
        {
            int i = 0;

            foreach (int _ in LongByte.GetSetBits(key.Value & Key.Value, true))
            {
                if (++i == 2)
                {
                    return false;
                }
            }

            return true;
        }

        public void SetKey(ColumnRegistration[] columns)
        {
            if (columns is null)
            {
                throw new ArgumentNullException(nameof(columns));
            }

            LongByte lb = 0;

            MaxBit = columns.Length;

            for (int x = 0; x < columns.Length; x++)
            {
                lb.SetBit(x, Headers.Contains(columns[x].Header));
            }

            Key = lb;
        }

        public override string ToString()
        {
            return $"{nameof(ExclusiveAny)}: " + Headers.Join();
        }

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

        #endregion IDisposable Support

        #endregion Methods
    }
}
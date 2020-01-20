using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Penguin.Analysis.Transformations
{
    [Serializable]
    public class Replace : ITransform
    {
        #region Properties

        public List<string> ResultColumns => throw new NotImplementedException();

        public string TargetColumn { get; internal set; }

        private Func<string, string> Process;

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Generic column transformation for converting/adding additional data columns
        /// does NOT keep original column so original must be returned if required
        /// </summary>
        /// <param name="ColumnName"></param>
        /// <param name="transformer"></param>
        public Replace(string ColumnName, Func<string, string> transformer)
        {
            this.TargetColumn = ColumnName;
            this.Process = transformer;
        }

        #endregion Constructors

        #region Methods

        public void Cleanup(DataTable table)
        {
        }

        public void TransformRow(DataRow source)
        {
            string Value = source[this.TargetColumn]?.ToString();

            string postTransform = this.Process.Invoke(Value);

            source[this.TargetColumn] = postTransform;
        }

        /// <summary>
        /// Adds new columns that may be required to hold values from row transformation
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public DataTable TransformTable(DataTable table)
        {
            return table;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Process = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Replace()
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
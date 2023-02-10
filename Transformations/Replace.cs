using System;
using System.Collections.Generic;
using System.Data;

namespace Penguin.Analysis.Transformations
{
    [Serializable]
    public class Replace : ITransform
    {
        #region Properties

        private Func<string, object> Process;

        public List<string> ResultColumns => throw new NotImplementedException();

        public string TargetColumn { get; internal set; }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Generic column transformation for converting/adding additional data columns
        /// does NOT keep original column so original must be returned if required
        /// </summary>
        /// <param name="ColumnName"></param>
        /// <param name="transformer"></param>
        public Replace(string ColumnName, Func<string, object> transformer)
        {
            TargetColumn = ColumnName;
            Process = transformer;
        }

        #endregion Constructors

        #region Methods

        public void Cleanup(DataTable table)
        {
        }

        public void TransformRow(DataRow source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.Table.Columns.Contains(TargetColumn))
            {
                string Value = source[TargetColumn]?.ToString();

                string postTransform = $"{Process.Invoke(Value)}";

                source[TargetColumn] = postTransform;
            }
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

        private bool disposedValue; // To detect redundant calls

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            return "Replace: " + TargetColumn;
        }

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

        #endregion IDisposable Support

        #endregion Methods
    }
}
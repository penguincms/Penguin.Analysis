using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Penguin.Analysis.Transformations
{
    [Serializable]
    public class GenericSplit : ITransform
    {
        #region Properties

        private Func<string, IEnumerable<string>> Process;
        public List<string> ResultColumns { get; internal set; }

        public string TargetColumn { get; internal set; }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Generic column transformation for converting/adding additional data columns
        /// does NOT keep original column so original must be returned if required
        /// </summary>
        /// <param name="ColumnName"></param>
        /// <param name="transformer"></param>
        public GenericSplit(string ColumnName, List<string> NewColumnNames, Func<string, IEnumerable<string>> transformer)
        {
            this.TargetColumn = ColumnName;
            this.Process = transformer;
            this.ResultColumns = NewColumnNames;
        }

        #endregion Constructors

        #region Methods

        public void Cleanup(DataTable table)
        {
            if (!this.ResultColumns.Contains(this.TargetColumn))
            {
                table.Columns.Remove(this.TargetColumn);
            }
        }

        public void TransformRow(DataRow source)
        {
            string Value = source[this.TargetColumn]?.ToString();

            List<string> postTransform = this.Process.Invoke(Value).ToList();

            if (postTransform.Count != this.ResultColumns.Count)
            {
                throw new Exception("Result count returned from transform does not match target column count");
            }

            int i = 0;
            foreach (string column in this.ResultColumns)
            {
                source[column] = postTransform[i++];
            }
        }

        /// <summary>
        /// Adds new columns that may be required to hold values from row transformation
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public DataTable TransformTable(DataTable table)
        {
            foreach (string newColumn in this.ResultColumns)
            {
                //Dont add target column again if one target is ALSO source
                if (!table.Columns.Cast<DataColumn>().Any(c => c.ColumnName == newColumn))
                {
                    table.Columns.Add(newColumn);
                }
            }

            return table;
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
                    this.ResultColumns.Clear();

                    this.Process = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                this.disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~GenericSplit()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        #endregion IDisposable Support

        #endregion Methods
    }
}
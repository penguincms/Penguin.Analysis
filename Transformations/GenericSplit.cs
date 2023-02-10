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
        /// <param name="NewColumnNames"></param>
        /// <param name="transformer"></param>
        public GenericSplit(string ColumnName, List<string> NewColumnNames, Func<string, IEnumerable<string>> transformer)
        {
            if (string.IsNullOrEmpty(ColumnName))
            {
                throw new ArgumentException("message", nameof(ColumnName));
            }

            TargetColumn = ColumnName;
            Process = transformer ?? throw new ArgumentNullException(nameof(transformer));
            ResultColumns = NewColumnNames ?? throw new ArgumentNullException(nameof(NewColumnNames));
        }

        #endregion Constructors

        #region Methods

        public void Cleanup(DataTable table)
        {
            if (table is null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (!ResultColumns.Contains(TargetColumn))
            {
                table.Columns.Remove(TargetColumn);
            }
        }

        public void TransformRow(DataRow source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string Value = source[TargetColumn]?.ToString();

            List<string> postTransform = Process.Invoke(Value).ToList();

            if (postTransform.Count != ResultColumns.Count)
            {
                throw new Exception("Result count returned from transform does not match target column count");
            }

            int i = 0;
            foreach (string column in ResultColumns)
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
            if (table is null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            foreach (string newColumn in ResultColumns)
            {
                //Dont add target column again if one target is ALSO source
                if (!table.Columns.Cast<DataColumn>().Any(c => c.ColumnName == newColumn))
                {
                    _ = table.Columns.Add(newColumn);
                }
            }

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
            return $"{TargetColumn} => ({string.Join(", ", ResultColumns)})";
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ResultColumns.Clear();

                    Process = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
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
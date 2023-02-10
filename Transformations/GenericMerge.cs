using System;
using System.Collections.Generic;
using System.Data;

namespace Penguin.Analysis.Transformations
{
    [Serializable]
    public class GenericMerge : ITransform
    {
        public class ColumnDefinition
        {
            public string Name { get; set; }

            public string Value { get; set; }
        }

        public override string ToString()
        {
            return $"({string.Join(", ", SourceColumns)}) => {ResultColumn}";
        }

        #region Properties

        private Func<IEnumerable<ColumnDefinition>, string> Process;

        public string ResultColumn { get; internal set; }

        public List<string> SourceColumns { get; internal set; }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Generic column transformation for converting/adding additional data columns
        /// does NOT keep original column so original must be returned if required
        /// </summary>
        /// <param name="SourceColumnNames"></param>
        /// <param name="NewColumn"></param>
        /// <param name="transformer"></param>
        public GenericMerge(List<string> SourceColumnNames, string NewColumn, Func<IEnumerable<ColumnDefinition>, string> transformer)
        {
            SourceColumns = SourceColumnNames;
            Process = transformer;
            ResultColumn = NewColumn;
        }

        #endregion Constructors

        #region Methods

        public void Cleanup(DataTable table)
        {
            if (table is null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            foreach (string columnName in SourceColumns)
            {
                if (table.Columns.Contains(columnName) && ResultColumn != columnName)
                {
                    table.Columns.Remove(columnName);
                }
            }
        }

        public void TransformRow(DataRow source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            List<ColumnDefinition> toTransform = new();

            foreach (string sourceC in SourceColumns)
            {
                ColumnDefinition cd = new()
                {
                    Name = sourceC,
                    Value = source[sourceC]?.ToString()
                };

                toTransform.Add(cd);
            }

            string postTransform = Process.Invoke(toTransform);

            source[ResultColumn] = postTransform;
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

            if (!table.Columns.Contains(ResultColumn))
            {
                _ = table.Columns.Add(ResultColumn);
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    SourceColumns.Clear();

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
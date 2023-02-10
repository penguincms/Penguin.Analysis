using Penguin.Analysis.DataColumns;
using System;
using System.Collections.Generic;
using System.Data;

namespace Penguin.Analysis.Transformations
{
    [Serializable]
    public class ConcatBits
    {
        #region Properties

        public List<string> ColumnNames { get; internal set; }

        public string TargetColumn => string.Join("|", ColumnNames);

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Generic column transformation for converting/adding additional data columns
        /// does NOT keep original column so original must be returned if required
        /// </summary>
        /// <param name="columnNames"></param>
        public ConcatBits(List<string> columnNames)
        {
            ColumnNames = columnNames;
        }

        #endregion Constructors

        #region Methods

        public void Cleanup(DataTable table)
        {
            if (table is null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            foreach (string ColumnName in ColumnNames)
            {
                table.Columns.Remove(ColumnName);
            }
        }

        public void TransformRow(DataRow source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            int v = 0;

            for (int i = 0; i < ColumnNames.Count; i++)
            {
                v |= Bool.GetValue(source[ColumnNames[i]].ToString()) >> i;
            }

            source[TargetColumn] = v;
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

            _ = table.Columns.Add(TargetColumn);

            return table;
        }

        #endregion Methods
    }
}
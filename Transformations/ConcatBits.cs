using Penguin.Analysis.DataColumns;
using System;
using System.Collections.Generic;
using System.Data;

namespace Penguin.Analysis.Transformations
{
    [Serializable]
    public class ConcatBytes
    {
        #region Properties

        public List<string> ColumnNames { get; internal set; }

        public string TargetColumn => string.Join("|", this.ColumnNames);

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Generic column transformation for converting/adding additional data columns
        /// does NOT keep original column so original must be returned if required
        /// </summary>
        /// <param name="ColumnName"></param>
        /// <param name="transformer"></param>
        public ConcatBytes(List<string> columnNames)
        {
            this.ColumnNames = columnNames;
        }

        #endregion Constructors

        #region Methods

        public void Cleanup(DataTable table)
        {
            foreach (string ColumnName in this.ColumnNames)
            {
                table.Columns.Remove(ColumnName);
            }
        }

        public void TransformRow(DataRow source)
        {
            int v = 0;

            for (int i = 0; i < this.ColumnNames.Count; i++)
            {
                v |= (Bool.GetValue(source[this.ColumnNames[i]].ToString()) >> i);
            }

            source[this.TargetColumn] = v;
        }

        /// <summary>
        /// Adds new columns that may be required to hold values from row transformation
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public DataTable TransformTable(DataTable table)
        {
            table.Columns.Add(this.TargetColumn);

            return table;
        }

        #endregion Methods
    }
}
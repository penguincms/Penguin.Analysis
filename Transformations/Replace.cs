using System;
using System.Collections.Generic;
using System.Data;

namespace Penguin.Analysis.Transformations
{
    [Serializable]
    public class Replace : ITransform
    {
        #region Properties

        public List<string> ResultColumns => throw new NotImplementedException();

        public string TargetColumn { get; internal set; }

        private readonly Func<string, string> Process;

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

        #endregion Methods
    }
}
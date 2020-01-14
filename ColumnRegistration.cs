using Penguin.Analysis.DataColumns;
using System;

namespace Penguin.Analysis
{
    [Serializable]
    public class ColumnRegistration
    {
        #region Properties

        public IDataColumn Column { get; set; }
        public string Header { get; set; }

        #endregion Properties

        public override string ToString()
        {
            return $"{this.Header}: {this.GetType()}";
        }
    }
}
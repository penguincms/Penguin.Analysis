using System;
using System.Collections.Generic;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public abstract class BaseColumn : IDataColumn
    {
        #region Methods

        public BaseColumn(DataSourceBuilder sourceBuilder)
        {
            SourceBuilder = sourceBuilder;
        }

        public DataSourceBuilder SourceBuilder { get; set; }

        public virtual string Display(int Value)
        {
            return Value.ToString();
        }

        public abstract IEnumerable<int> GetOptions();

        public abstract int Transform(string input, bool PositiveIndicator);

        #endregion Methods
    }
}
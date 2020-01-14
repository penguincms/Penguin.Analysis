using System;
using System.Collections.Generic;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class Bool : BaseColumn
    {
        public Bool(DataSourceBuilder sourceBuilder) : base(sourceBuilder)
        {
        }
        #region Methods

        public static int GetValue(string input)
        {
            if (input.StartsWith("t", StringComparison.OrdinalIgnoreCase) || input.StartsWith("1", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public override string Display(int Value)
        {
            return Value == 1 ? "true" : "false";
        }

        public override IEnumerable<int> GetOptions()
        {
            return new List<int>
            {
                1,
                0
            };
        }

        public override int Transform(string input, bool PositiveIndicator)
        {
            return GetValue(input);
        }

        #endregion Methods
    }
}
using System;
using System.Collections.Generic;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class NullableBool : BaseColumn
    {
        public NullableBool(DataSourceBuilder sourceBuilder) : base(sourceBuilder)
        {
        }
        #region Methods

        public override string Display(int Value)
        {
            return ((NBool)Value).ToString();
        }

        public override IEnumerable<int> GetOptions()
        {
            return new List<int>
            {
                0,
                1,
                2
            };
        }

        public override int Transform(string input, bool PositiveIndicator)
        {
            if (input.StartsWith("t", StringComparison.OrdinalIgnoreCase) || input.StartsWith("1", StringComparison.OrdinalIgnoreCase))
            {
                return (int)NBool.True;
            }
            else if (input.StartsWith("f", StringComparison.OrdinalIgnoreCase) || input.StartsWith("0", StringComparison.OrdinalIgnoreCase))
            {
                return (int)NBool.False;
            }
            else
            {
                return (int)NBool.Null;
            }
        }

        protected override void OnDispose()
        {

        }

        #endregion Methods
    }
}
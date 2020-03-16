using System;
using System.Collections.Generic;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class NullableBool : BaseColumn
    {
        public override int OptionCount => 3;

        public NullableBool() : base()
        {
        }

        #region Methods

        public override string Display(int Value)
        {
            return ((NBool)Value).ToString();
        }

        public override void EndSeed() => throw new NotImplementedException();

        public override void Seed(string input, bool PositiveIndicator)
        {
        }

        public override int Transform(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (int)NBool.Null;
            }
            else if (input.StartsWith("t", StringComparison.OrdinalIgnoreCase) || input.StartsWith("1", StringComparison.OrdinalIgnoreCase))
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
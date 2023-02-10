using System;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class Bool : BaseColumn
    {
        public override int OptionCount => 2;

        public Bool() : base()
        {
        }

        #region Methods

        public static int GetValue(string input)
        {
            return input is null
                ? throw new ArgumentNullException(nameof(input))
                : input.StartsWith("t", StringComparison.OrdinalIgnoreCase) || input.StartsWith("1", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        public override string Display(int Value)
        {
            return Value == 1 ? "true" : "false";
        }

        public override void EndSeed()
        {
        }

        public override void Seed(string input, bool PositiveIndicator)
        {
            throw new NotImplementedException();
        }

        public override int Transform(string input)
        {
            return GetValue(input);
        }

        protected override void OnDispose()
        {
        }

        #endregion Methods
    }
}
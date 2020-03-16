using System;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class RoundedInt : Enumeration
    {
        public RoundedInt() : base()
        {
        }

        #region Methods

        public override void Seed(string input, bool PositiveIndicator)
        {
            double d = double.Parse(input);

            d = Math.Round(d);

            base.Seed(d.ToString(), PositiveIndicator);
        }

        public override int Transform(string input)
        {
            double d = double.Parse(input);

            d = Math.Round(d);

            return base.Transform(d.ToString());
        }

        #endregion Methods
    }
}
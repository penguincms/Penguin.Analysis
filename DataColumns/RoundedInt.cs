using System;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class RoundedInt : Enumeration
    {
        #region Methods

        public override int Transform(string input, bool PositiveIndicator)
        {
            double d = double.Parse(input);

            d = Math.Round(d);

            return base.Transform(d.ToString(), PositiveIndicator);
        }

        #endregion Methods
    }
}
using System;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class Exists : Bool
    {
        #region Methods

        public override int Transform(string input, bool PositiveIndicator)
        {
            return string.IsNullOrWhiteSpace(input) ? 0 : 1;
        }

        #endregion Methods
    }
}
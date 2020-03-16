using System;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class Exists : Bool
    {
        public Exists() : base()
        {
        }

        #region Methods

        public override int Transform(string input)
        {
            return string.IsNullOrWhiteSpace(input) ? 0 : 1;
        }

        #endregion Methods
    }
}
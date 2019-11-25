using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class RangedFloat : Enumeration
    {
        #region Properties

        public List<float> RangeStarts { get; set; } = new List<float>();

        #endregion Properties

        #region Constructors

        public RangedFloat()
        {
        }

        public RangedFloat(params int[] rangeStarts)
        {
            foreach (int i in rangeStarts)
            {
                this.RangeStarts.Add(i);
            }
        }

        public RangedFloat(params float[] rangeStarts)
        {
            foreach (float i in rangeStarts)
            {
                this.RangeStarts.Add(i);
            }
        }

        #endregion Constructors

        #region Methods

        public override int Transform(string input, bool PositiveIndicator)
        {
            float test = float.Parse(input);

            float Closest = this.RangeStarts.Min();

            if (this.RangeStarts.Any(r => r <= test))
            {
                Closest = this.RangeStarts.Where(r => r <= test).Max();
            }

            return base.Transform(Closest.ToString(), PositiveIndicator);
        }

        #endregion Methods
    }
}
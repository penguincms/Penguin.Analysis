using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public class RangedFloat : BaseColumn
    {
        #region Properties

        public override int OptionCount => RangeStarts.Length;

        public float[] RangeStarts { get; set; }

        #endregion Properties

        #region Constructors

        public RangedFloat() : base()
        {
        }

        public RangedFloat(int a, int b, int c, params int[] other) : base()
        {
            RangeStarts = new List<int>()
            {
                a,b,c
            }.Concat(other).OrderBy(r => r).Cast<float>().ToArray();
        }

        public RangedFloat(int Max, int Step = 1) : base()
        {
            List<float> rangeStarts = new();

            for (int i = 0; i <= Max; i += Step)
            {
                rangeStarts.Add(i);
            }

            RangeStarts = rangeStarts.ToArray();
        }

        public RangedFloat(params float[] rangeStarts) : base()
        {
            RangeStarts = rangeStarts.OrderBy(r => r).ToArray();
        }

        #endregion Constructors

        #region Methods

        public override void EndSeed()
        {
        }

        public override void Seed(string input, bool PositiveIndicator)
        {
        }

        public override int Transform(string input)
        {
            float test = float.Parse(input);

            for (int i = 0; i < RangeStarts.Length - 1; i++)
            {
                if (RangeStarts[i + 1] > test)
                {
                    return i;
                }
            }

            return RangeStarts.Length - 1;
        }

        protected override void OnDispose()
        {
        }

        #endregion Methods
    }
}
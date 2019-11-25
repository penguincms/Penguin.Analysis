using Penguin.Analysis.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis
{
    public class Evaluation
    {
        #region Properties

        public TypelessDataRow DataRow { get; set; }

        public Dictionary<int, Node> MatchedRoutes { get; set; } = new Dictionary<int, Node>();

        public int MatchingRoutes { get; set; }

        public AnalysisResults Result { get; set; }

        public float Score => this.Scores.Average();

        public List<float> Scores { get; set; }

        #endregion Properties

        #region Constructors

        public Evaluation(TypelessDataRow dataRow)
        {
            this.Scores = new List<float>();
            this.DataRow = dataRow;
        }

        #endregion Constructors

        #region Methods

        public void MatchRoute(Node n)
        {
            int Key = n.GetKey();

            if (!this.MatchedRoutes.ContainsKey(Key))
            {
                this.MatchedRoutes.Add(Key, n);

                int sign = n.Accuracy < this.Result.BaseRate ? -1 : 1;

                this.Scores.Add(sign * this.Weight(n));
            }
        }

        public float Weight(Node n)
        {
            float thisAccuracy = n.Accuracy;

            float weight;
            if (thisAccuracy < this.Result.BaseRate)
            {
                weight = (1 - (thisAccuracy / this.Result.BaseRate));
            }
            else
            {
                weight = ((thisAccuracy - this.Result.BaseRate) / (1 - this.Result.BaseRate));
            }

            weight = (float)Math.Pow(weight, 1f);

            return weight;
        }

        #endregion Methods
    }
}
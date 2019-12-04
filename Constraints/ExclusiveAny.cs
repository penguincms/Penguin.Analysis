using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis.Constraints
{
    /// <summary>
    /// Do not allow any possible combination to appear as a route. Useful if one property is derivative of another
    /// </summary>
    [Serializable]
    public class ExclusiveAny : IRouteConstraint
    {
        #region Fields

        private readonly List<string> Headers = new List<string>();

        #endregion Fields

        #region Constructors

        public ExclusiveAny(params string[] headers)
        {
            this.Headers = headers.ToList();
        }

        #endregion Constructors

        #region Methods

        public bool Evaluate(params string[] headers)
        {
            return headers.Count(h => this.Headers.Contains(h)) <= 1;
        }

        #endregion Methods
    }
}
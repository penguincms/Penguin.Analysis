using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis.Constraints
{
    /// <summary>
    /// Do not allow this exact combination to appear as a route. Useful if one property is derivative of another
    /// </summary>
    [Serializable]
    public class Exclusive : IRouteConstraint
    {
        #region Fields

        private readonly List<string> Headers = new List<string>();

        #endregion Fields

        #region Constructors

        public Exclusive(params string[] headers)
        {
            this.Headers = headers.ToList();
        }

        #endregion Constructors

        #region Methods

        public bool Evaluate(params string[] headers)
        {
            return !(this.Headers.Count() >= headers.Count() && headers.All(h => headers.Contains(h)));
        }

        #endregion Methods
    }
}
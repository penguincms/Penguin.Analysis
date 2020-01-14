using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis.Constraints
{
    /// <summary>
    /// Do not allow this exact combination to appear as a route. Useful if one property is derivative of another. Also, if only one parameter then force the property to be evaluated alone
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
            if (headers.Length > 0)
            {
                this.Headers = headers.ToList();
            }
            else
            {
                throw new ArgumentException("At least one header must be specified");
            }
        }

        #endregion Constructors

        #region Methods

        public bool Evaluate(params string[] headers)
        {
            if (this.Headers.Count > 1)
            {
                return !(this.Headers.Count >= headers.Length && this.Headers.All(h => headers.Contains(h)));
            }
            else
            {
                return headers.Length == 1 || !headers.Contains(this.Headers.First());
            }
        }

        #endregion Methods
    }
}
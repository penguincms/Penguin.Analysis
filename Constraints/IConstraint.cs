using System;

namespace Penguin.Analysis.Constraints
{
    public interface IRouteConstraint : IDisposable
    {
        #region Methods

        /// <summary>
        /// Checks to make sure the headers pass this constraint
        /// </summary>
        /// <param name="headers"></param>
        /// <returns></returns>
        bool Evaluate(params string[] headers);

        #endregion Methods
    }
}
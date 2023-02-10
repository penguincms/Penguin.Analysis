using System;

namespace Penguin.Analysis.Constraints
{
    public interface IRouteConstraint : IDisposable
    {
        #region Methods

        public LongByte Key { get; }

        /// <summary>
        /// Checks to make sure the headers pass this constraint
        /// </summary>
        /// <returns></returns>
        bool Evaluate(LongByte key);

        /// <summary>
        /// Uses the column registrations to convert the header string values into a key
        /// </summary>
        /// <param name="columns">The registered columns</param>
        void SetKey(ColumnRegistration[] columns);

        #endregion Methods
    }
}
using System;
using System.Collections.Generic;

namespace Penguin.Analysis.DataColumns
{
    public interface IDataColumn : IDisposable
    {
        #region Methods

        /// <summary>
        /// A count of all possible options returned
        /// </summary>
        int OptionCount { get; }

        /// <summary>
        /// If false, dont bother seeding this column data
        /// </summary>
        bool SeedMe { get; }

        /// <summary>
        /// Allows for converting the value back to a human understandable type
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        string Display(int Value);

        /// <summary>
        /// When all possible values have been passed in
        /// </summary>
        void EndSeed();

        /// <summary>
        /// Accepts each option one at a time
        /// </summary>
        /// <param name="input">The option</param>
        void Seed(string input, bool PositiveIndicator);

        /// <summary>
        /// Method to "prewarm" all the values in the row so that they match the expected format
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        int Transform(string input);

        #endregion Methods
    }
}
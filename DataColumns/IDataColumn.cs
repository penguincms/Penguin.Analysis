using System;
using System.Collections.Generic;

namespace Penguin.Analysis.DataColumns
{
    public interface IDataColumn : IDisposable
    {
        #region Methods

        /// <summary>
        /// Allows for converting the value back to a human understandable type
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        string Display(int Value);

        /// <summary>
        /// Returns all possible matching and nonmatching values based on this columns logic.
        /// </summary>
        /// <param name="tableValues"></param>
        /// <param name="Result"></param>
        /// <returns></returns>
        IEnumerable<int> GetOptions();

        /// <summary>
        /// Optional method to "prewarm" all the values in the row so that they match the expected format
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        int Transform(string input, bool PositiveIndicator);

        #endregion Methods
    }
}
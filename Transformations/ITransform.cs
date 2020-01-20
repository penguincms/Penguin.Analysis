using System;
using System.Collections.Generic;
using System.Data;

namespace Penguin.Analysis.Transformations
{
    public interface ITransform : IDisposable
    {
        #region Properties

        List<string> ResultColumns { get; }

        string TargetColumn { get; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Run any post row alteration cleanup, like removing unneeded columns
        /// </summary>
        /// <param name="table"></param>
        void Cleanup(DataTable table);

        /// <summary>
        /// Make the necessary run-time transformations on the data row
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        void TransformRow(DataRow source);

        /// <summary>
        /// Make the post transform header changes to the datatable
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        DataTable TransformTable(DataTable table);

        #endregion Methods
    }
}
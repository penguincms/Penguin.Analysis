﻿using System;
using System.Collections.Generic;

namespace Penguin.Analysis.DataColumns
{
    [Serializable]
    public abstract class BaseColumn : IDataColumn
    {
        #region Methods

        public virtual string Display(int Value) => Value.ToString();

        public abstract IEnumerable<int> GetOptions();

        public abstract int Transform(string input, bool PositiveIndicator);

        #endregion Methods
    }
}
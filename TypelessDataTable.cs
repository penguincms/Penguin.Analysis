using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Penguin.Analysis
{
    [Serializable]
    public class TypelessDataRow : IEnumerable<int>
    {
        #region Properties

        private readonly int[] _Items;
        public bool MatchesOutput { get; set; }

        public TypelessDataTable Table { get; set; }

        #endregion Properties

        #region Constructors

        public TypelessDataRow(params int[] items)
        {
            _Items = items;
        }

        #endregion Constructors

        #region Indexers

        public int this[int i]
        {
            get => _Items[i];
            set => _Items[i] = value;
        }

        #endregion Indexers

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(int index, int Value)
        {
            return _Items[index] == Value;
        }

        #endregion Methods

        public IEnumerator<int> GetEnumerator()
        {
            return ((IEnumerable<int>)_Items).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<int>)_Items).GetEnumerator();
        }

        public override string ToString()
        {
            return string.Join(", ", _Items);
        }
    }

    [Serializable]
    public class TypelessDataTable
    {
        #region Properties

        private readonly List<TypelessDataRow> _rows;
        public ushort RowCount { get; internal set; }

        public IEnumerable<TypelessDataRow> Rows
        {
            get
            {
                foreach (TypelessDataRow row in _rows)
                {
                    yield return row;
                }
            }
        }

        #endregion Properties

        #region Constructors

        public TypelessDataTable()
        {
            _rows = new List<TypelessDataRow>();
        }

        public TypelessDataTable(int rows)
        {
            _rows = new List<TypelessDataRow>(rows);
        }

        #endregion Constructors

        #region Indexers

        public TypelessDataRow this[int index] => _rows[index];

        #endregion Indexers

        #region Methods

        public void AddRow(TypelessDataRow row)
        {
            if (row is null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            row.Table = this;
            _rows.Add(row);
            RowCount++;
        }

        public void AddRow(params int[] items)
        {
            TypelessDataRow row = new(items)
            {
                Table = this
            };
            _rows.Add(row);
            RowCount++;
        }

        #endregion Methods
    }
}
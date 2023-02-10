namespace Penguin.Analysis
{
    public class NodeSet
    {
        #region Properties

        public sbyte ColumnIndex { get; set; }

        public long Key => (long)1 << ColumnIndex;
        public int Values { get; set; }

        #endregion Properties

        #region Constructors

        internal NodeSet((sbyte columnIndex, int values) r) : this(r.columnIndex, r.values)
        {
        }

        internal NodeSet(sbyte columnIndex, int values)
        {
            ColumnIndex = columnIndex;
            Values = values;
        }

        // this is second one '!='
        public static bool operator !=(NodeSet obj1, NodeSet obj2)
        {
            return !(obj1 == obj2);
        }

        public static bool operator ==(NodeSet obj1, NodeSet obj2)
        {
            return ReferenceEquals(obj1, obj2) || obj1 is not null && obj2 is not null && obj1.ColumnIndex == obj2.ColumnIndex;
        }

        public bool Equals(NodeSet other)
        {
            return other is not null && (ReferenceEquals(this, other) || ColumnIndex == other.ColumnIndex);
        }

        public override bool Equals(object obj)
        {
            return obj is not null && (ReferenceEquals(this, obj) || (obj is NodeSet n && Equals(n)));
        }

        public override int GetHashCode()
        {
            return ColumnIndex;
        }

        #endregion Constructors
    }
}
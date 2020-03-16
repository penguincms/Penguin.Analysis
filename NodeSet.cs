using System.Linq;

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
            this.ColumnIndex = columnIndex;
            this.Values = values;
        }

        // this is second one '!='
        public static bool operator !=(NodeSet obj1, NodeSet obj2)
        {
            return !(obj1 == obj2);
        }

        public static bool operator ==(NodeSet obj1, NodeSet obj2)
        {
            if (ReferenceEquals(obj1, obj2))
            {
                return true;
            }

            if (obj1 is null || obj2 is null)
            {
                return false;
            }

            return obj1.ColumnIndex == obj2.ColumnIndex;
        }

        public bool Equals(NodeSet other)
        {
            if (other is null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.ColumnIndex == other.ColumnIndex;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is NodeSet n && this.Equals(n);
        }

        public override int GetHashCode()
        {
            return this.ColumnIndex;
        }

        #endregion Constructors
    }
}
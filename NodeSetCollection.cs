using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis
{
    public class NodeSetCollection : IList<NodeSet>
    {
        internal static readonly NodeSet[] NodeSetCache = new NodeSet[256];

        public int Count => Key.Count;
        public bool IsReadOnly => false;
        public LongByte Key { get; private set; }

        internal NodeSetCollection(IEnumerable<NodeSet> set) : this(new LongByte(set.Select(s => s.ColumnIndex)).Value)
        {
            foreach(NodeSet thisSet in set)
            {
                if(NodeSetCache[thisSet.ColumnIndex] is null)
                {
                    NodeSetCache[thisSet.ColumnIndex] = thisSet;
                }
            }
        }

        internal NodeSetCollection(IEnumerable<int> set) : this(new LongByte(set))
        {

        }

        internal NodeSetCollection(LongByte key)
        {
            Key = key;
        }

        internal NodeSetCollection(IEnumerable<(sbyte columnIndex, int[] values)> set)
        {
            List<NodeSet> localSets = new List<NodeSet>();

            foreach ((sbyte columnIndex, int[] values) x in set)
            {
                if (NodeSetCache[x.columnIndex] is null)
                {
                    NodeSetCache[x.columnIndex] = new NodeSet(x);
                }

                localSets.Add(NodeSetCache[x.columnIndex]);
            }

            SetKey(localSets);
        }

        internal NodeSetCollection()
        {
        }

        public NodeSet this[int index]
        {
            get => NodeSetCache[Key.ElementAt(index)];
            set
            {
                if (NodeSetCache[value.ColumnIndex] is null)
                {
                    NodeSetCache[value.ColumnIndex] = value;
                }

                Key = LongByte.SetBit(Key, index, value != null);
            }
        }

        public static implicit operator NodeSetCollection(LongByte b) => new NodeSetCollection(b.Value);

        public static implicit operator NodeSetCollection(long b) => new NodeSetCollection(b);

        public static implicit operator NodeSetCollection(List<NodeSet> n)
        {
            return new NodeSetCollection(n);
        }

        // this is second one '!='
        public static bool operator !=(NodeSetCollection obj1, NodeSetCollection obj2)
        {
            return !(obj1 == obj2);
        }

        public static bool operator ==(NodeSetCollection obj1, NodeSetCollection obj2)
        {
            if (ReferenceEquals(obj1, obj2))
            {
                return true;
            }

            if (obj1 is null || obj2 is null)
            {
                return false;
            }

            return obj1.Key == obj2.Key;
        }

        public void Add(NodeSet item)
        {
            this.Key |= item.Key;
        }

        public void Clear()
        {
            Key = 0;
        }

        public bool Contains(NodeSet item)
        {
            return (this.Key & item.Key) != 0;
        }

        public void CopyTo(NodeSet[] array, int arrayIndex)
        {
            List<NodeSet> localSets = GetSets().ToList();

            localSets.CopyTo(array, arrayIndex);
        }

        public bool Equals(NodeSetCollection other)
        {
            if (other is null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this == other;
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

            return obj is NodeSetCollection n && this.Equals(n);
        }

        public IEnumerator<NodeSet> GetEnumerator() => this.GetSets().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetSets().GetEnumerator();

        public override int GetHashCode() => unchecked((int)Key);

        public int IndexOf(NodeSet item) => (this.Key & item.Key) != 0 ? item.ColumnIndex : -1;

        public void Insert(int index, NodeSet item)
        {
            this.Key &= item.Key;
        }

        public bool Remove(long key)
        {
            bool v = (this.Key & key) != 0;

            if (v)
            {
                this.Key ^= key;
            }

            return v;
        }

        public bool Remove(sbyte header) => Remove((long)1 << header);

        public bool Remove(NodeSet item) => Remove(item.Key);

        public void RemoveAt(int index) => Remove((sbyte)index);

        private IEnumerable<NodeSet> GetSets() => Key.Select(i => NodeSetCache[i]);

        private void SetKey(IEnumerable<NodeSet> set)
        {
            this.Key = 0;

            foreach (NodeSet n in set)
            {
                this.Key |= n.Key;
            }
        }
    }
}
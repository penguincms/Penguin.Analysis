using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis
{
    public class NodeSetCollection : IList<NodeSet>
    {
        public NodeSet this[int index]
        {
            get => NodeSetCache[Key.ElementAt(index)];
            set
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (NodeSetCache[value.ColumnIndex] is null)
                {
                    NodeSetCache[value.ColumnIndex] = value;
                }

                Key = LongByte.SetBit(Key, index, value != null);
            }
        }

        internal static readonly NodeSet[] NodeSetCache = new NodeSet[256];

        public int Count => Key.Count;

        public bool IsReadOnly => false;

        public LongByte Key { get; private set; }

        internal NodeSetCollection(IEnumerable<NodeSet> set) : this(new LongByte(set.Select(s => s.ColumnIndex)).Value)
        {
            foreach (NodeSet thisSet in set)
            {
                if (NodeSetCache[thisSet.ColumnIndex] is null)
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

        internal NodeSetCollection(IEnumerable<(sbyte columnIndex, int values)> set)
        {
            List<NodeSet> localSets = new();

            foreach ((sbyte columnIndex, int values) x in set)
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

        public static implicit operator NodeSetCollection(LongByte b)
        {
            return new NodeSetCollection(b.Value);
        }

        public static implicit operator NodeSetCollection(long b)
        {
            return new NodeSetCollection(b);
        }

        public static implicit operator NodeSetCollection(List<NodeSet> n)
        {
            return n is null ? throw new ArgumentNullException(nameof(n)) : new NodeSetCollection(n);
        }

        // this is second one '!='
        public static bool operator !=(NodeSetCollection obj1, NodeSetCollection obj2)
        {
            return !(obj1 == obj2);
        }

        public static bool operator ==(NodeSetCollection obj1, NodeSetCollection obj2)
        {
            return ReferenceEquals(obj1, obj2) || obj1 is not null && obj2 is not null && obj1.Key == obj2.Key;
        }

        public void Add(NodeSet item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            Key |= item.Key;
        }

        public void Clear()
        {
            Key = 0;
        }

        public bool Contains(NodeSet item)
        {
            return item is null ? throw new ArgumentNullException(nameof(item)) : (Key & item.Key) != 0;
        }

        public void CopyTo(NodeSet[] array, int arrayIndex)
        {
            List<NodeSet> localSets = GetSets().ToList();

            localSets.CopyTo(array, arrayIndex);
        }

        public bool Equals(NodeSetCollection other)
        {
            return other is not null && (ReferenceEquals(this, other) || this == other);
        }

        public override bool Equals(object obj)
        {
            return obj is not null && (ReferenceEquals(this, obj) || (obj is NodeSetCollection n && Equals(n)));
        }

        public IEnumerator<NodeSet> GetEnumerator()
        {
            return GetSets().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetSets().GetEnumerator();
        }

        public override int GetHashCode()
        {
            return unchecked((int)Key);
        }

        public int IndexOf(NodeSet item)
        {
            return item is null ? throw new ArgumentNullException(nameof(item)) : (Key & item.Key) != 0 ? item.ColumnIndex : -1;
        }

        public void Insert(int index, NodeSet item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            Key &= item.Key;
        }

        public bool Remove(long key)
        {
            bool v = (Key & key) != 0;

            if (v)
            {
                Key ^= key;
            }

            return v;
        }

        public bool Remove(sbyte header)
        {
            return Remove((long)1 << header);
        }

        public bool Remove(NodeSet item)
        {
            return Remove(item?.Key ?? throw new ArgumentNullException(nameof(item)));
        }

        public void RemoveAt(int index)
        {
            _ = Remove((sbyte)index);
        }

        private IEnumerable<NodeSet> GetSets()
        {
            return Key.Select(i => NodeSetCache[i]);
        }

        private void SetKey(IEnumerable<NodeSet> set)
        {
            Key = 0;

            foreach (NodeSet n in set)
            {
                Key |= n.Key;
            }
        }

        public NodeSetCollection ToNodeSetCollection()
        {
            throw new NotImplementedException();
        }
    }
}
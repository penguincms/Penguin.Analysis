using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Penguin.Analysis
{
    class SerializationResults : IEnumerable<NodeMeta>
    {
        public SerializationResults()
        {

        }

        public SerializationResults(INodeFileStream stream, INode node, long parentOffset)
        {
            Meta.Add(new NodeMeta(stream, node, parentOffset));
        }

        public ConcurrentBag<NodeMeta> Meta = new ConcurrentBag<NodeMeta>();

        public IEnumerator<NodeMeta> GetEnumerator()
        {
            return ((IEnumerable<NodeMeta>)this.Meta).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<NodeMeta>)this.Meta).GetEnumerator();
        }

        public void AddRange(IEnumerable<NodeMeta> meta)
        {
            foreach(NodeMeta m in meta)
            {
                Meta.Add(m);
            }
        }
    }

    internal struct NodeMeta
    {
        public NodeMeta(INodeFileStream stream, INode node, long parentOffset)
        {
            Offset = stream.Offset;
            Root = parentOffset == DiskNode.HeaderBytes;
            Matches = node.GetMatched();

            if (node.Header == -1)
            {
                if (node.Next != null && node.Next.Any())
                {
                    Header = node.Next.Select(nc => nc.Header).Distinct().SingleOrDefault();
                } else
                {
                    Header = -1;
                }
            }
            else
            {
                Header = node.Header;
            }
        }

        public long Offset;
        public sbyte Header;
        public int Matches;
        public bool Root;

        public override string ToString()
        {
            return $"@{Offset}: {Header}x{Matches}";
        }
    }
}

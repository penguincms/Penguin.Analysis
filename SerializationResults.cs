using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis
{
    internal class SerializationResults : IEnumerable<NodeMeta>
    {
        public ConcurrentBag<NodeMeta> Meta = new ConcurrentBag<NodeMeta>();

        public SerializationResults()
        {
        }

        public SerializationResults(INodeFileStream stream, INode node, long parentOffset)
        {
            this.Meta.Add(new NodeMeta(stream, node, parentOffset));
        }

        public void AddRange(IEnumerable<NodeMeta> meta)
        {
            foreach (NodeMeta m in meta)
            {
                this.Meta.Add(m);
            }
        }

        public IEnumerator<NodeMeta> GetEnumerator()
        {
            return ((IEnumerable<NodeMeta>)this.Meta).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<NodeMeta>)this.Meta).GetEnumerator();
        }
    }

    internal struct NodeMeta
    {
        public sbyte Header;

        public int Matches;

        public long Offset;

        public bool Root;

        public NodeMeta(INodeFileStream stream, INode node, long parentOffset)
        {
            this.Offset = stream.Offset;
            this.Root = parentOffset == DiskNode.HEADER_BYTES;
            this.Matches = node.GetMatched();

            if (node.Header == -1)
            {
                if (node.Next != null && node.Next.Any())
                {
                    this.Header = node.Next.Select(nc => nc.Header).Distinct().SingleOrDefault();
                }
                else
                {
                    this.Header = -1;
                }
            }
            else
            {
                this.Header = node.Header;
            }
        }

        public override string ToString()
        {
            return $"@{this.Offset}: {this.Header}x{this.Matches}";
        }
    }
}
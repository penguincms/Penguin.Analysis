using Penguin.Analysis.Interfaces;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis
{
    internal struct NodeMeta
    {
        public sbyte Header;

        public double Accuracy;

        public long Offset;

        public bool Root;

        public NodeMeta(INodeFileStream stream, INode node, long parentOffset)
        {
            Offset = stream.Offset;
            Root = parentOffset == DiskNode.HEADER_BYTES;
            Accuracy = node.Accuracy.Next;

            Header = node.Header == -1
                ? node.Next != null && node.Next.Any(n => n != null)
                    ? node.Next.Where(n => n != null).Select(nc => nc.Header).Distinct().SingleOrDefault()
                    : (sbyte)-1
                : node.Header;
        }

        public override string ToString()
        {
            return $"@{Offset}: {Header}x{Accuracy}";
        }
    }

    internal class SerializationResults : IEnumerable<NodeMeta>
    {
        public ConcurrentBag<NodeMeta> Meta = new();

        public SerializationResults()
        {
        }

        public SerializationResults(INodeFileStream stream, INode node, long parentOffset)
        {
            Meta.Add(new NodeMeta(stream, node, parentOffset));
        }

        public void AddRange(IEnumerable<NodeMeta> meta)
        {
            foreach (NodeMeta m in meta)
            {
                Meta.Add(m);
            }
        }

        public IEnumerator<NodeMeta> GetEnumerator()
        {
            return ((IEnumerable<NodeMeta>)Meta).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<NodeMeta>)Meta).GetEnumerator();
        }
    }
}
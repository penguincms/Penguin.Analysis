using Newtonsoft.Json;
using Penguin.Analysis.Extensions;
using Penguin.Analysis.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Analysis
{
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class DiskNode : INode
    {
        public const int NodeSize = 30;

        private LockedNodeFileStream _backingStream { get; set; }
        private long ParentOffset { get; set; }
        private long[] NextOffsets { get; set; }

        [JsonProperty("N", Order = 5)]
        public List<DiskNode> DeleteMe => (this as INode).Next.Cast<DiskNode>().ToList();

        public DiskNode(LockedNodeFileStream fileStream, long offset)
        {
            //refs.Add(offset, this);

            byte[] data = fileStream.ReadBlock(offset);

            int pointer = 0;

            byte[] Read(int length)
            {
                byte[] toreturn = data.Skip(pointer).Take(length).ToArray();

                pointer += length;

                return toreturn;
            }

            this.ParentOffset = BitConverter.ToInt64(Read(8), 0);

            for (int i = 0; i < 4; i++)
            {
                this.Results[i] = BitConverter.ToInt32(Read(4), 0);
            }

            this.Header = unchecked((sbyte)data[pointer++]);

            this.Value = BitConverter.ToInt32(Read(4), 0);

            this.LastNode = data[pointer++] != 0;

            List<long> childOffsets = new List<long>();

            while (pointer < data.Length)
            {
                childOffsets.Add(BitConverter.ToInt64(Read(8), 0));
            }

            this.NextOffsets = childOffsets.ToArray();

            this._backingStream = fileStream;
        }

        public float Accuracy => this.GetAccuracy();
        public int Depth => this.GetDepth();

        [JsonProperty("H", Order = 2)]
        public sbyte Header { get; set; }

        [JsonProperty("L", Order = 4)]
        public bool LastNode { get; set; }

        public int Matched => this.GetMatched();
        public IList<TypelessDataRow> MatchingRows { get; set; }

        [JsonProperty("R", Order = 1)]
        public int[] Results { get; set; } = new int[4];

        public float Score => this.GetScore();

        [JsonProperty("V", Order = 3)]
        public int Value { get; set; }

        [JsonProperty("P", Order = 0)]
        public DiskNode ParentNode
        {
            get
            {
                if(ParentOffset == 0)
                {
                    return null;
                }

                return new DiskNode(_backingStream, ParentOffset);
            }
        }

        //static Dictionary<long, DiskNode> refs = new Dictionary<long, DiskNode>();

        //private DiskNode GetRef(long offset)
        //{
        //    if (refs.TryGetValue(offset, out DiskNode parent))
        //    {
        //        return parent;
        //    }

        //    parent = new DiskNode(_backingStream, offset);

        //    return parent;
        //}
        INode INode.ParentNode => this.ParentNode;

        IEnumerable<INode> INode.Next
        {
            get
            {
                foreach (long childOffset in this.NextOffsets)
                {

                    yield return new DiskNode(_backingStream, childOffset);
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Penguin.Analysis
{
    internal class ComplexTree
    {
        private (sbyte ColumnIndex, int Values)[] ColumnData;

        private long Hc;
        private long Hi;

        public ComplexTree(IEnumerable<(sbyte ColumnIndex, int Values)> columnData)
        {
            ColumnData = columnData.ToArray();

            Hc = (long)Math.Pow(2, ColumnData.Length) - 1;
            Hi = Hc;
        }

        public IEnumerable<List<(sbyte ColumnIndex, int Values)>> Build()
        {
            for (; Hi >= 1; Hi -= 2)
            {
                byte sbits = 0;

                long Hb = Hi;

                while (Hb != 0)
                {
                    if ((Hb & 1) != 0)
                    {
                        sbits++;
                    }

                    Hb >>= 1;
                }

                List<(sbyte ColumnIndex, int Values)> thisGraph = new List<(sbyte ColumnIndex, int Values)>(sbits);

                for (sbyte Wi = (sbyte)(ColumnData.Length - 1); Wi >= 0; Wi--)
                {
                    if (((Hi >> Wi) & 1) != 0)
                    {
                        thisGraph.Add((Wi, ColumnData[Wi].Values));
                    }
                }

                yield return thisGraph;
            }
        }

        public void Jump(long address)
        {
            Hi = address;
        }

        public void JumpToIndex(long index)
        {
            Hi = Hc - (index * 2);
        }
    }
}
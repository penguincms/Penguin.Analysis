using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Penguin.Analysis
{
    public static class Cpu
    {
        #region Methods

        public static void For(int fromInclusive, int toExclusive, Action<(int Cpu, List<int> Indexes)> body)
        {
            //Break apart each sized group into bins that equal the number of CPU's so we can maximize speed.
            (int Cpu, List<int> Indexes)[] threads = new (int Cpu, List<int> Indexes)[Environment.ProcessorCount];

            int cpui;
            for (cpui = 0; cpui < threads.Length; cpui++)
            {
                threads[cpui].Cpu = cpui;
                threads[cpui].Indexes = new List<int>();
            }

            cpui = 0;

            for (int i = fromInclusive; i < toExclusive; i++)
            {
                threads[cpui++].Indexes.Add(i);

                if (cpui == threads.Length)
                {
                    cpui = 0;
                }
            }

            _ = Parallel.ForEach(threads.ToList(), body);
        }

        public static void ForEach<TSource>(IEnumerable<TSource> source, Action<(int Cpu, List<TSource> Bag)> body)
        {
            List<TSource> sList = source.ToList();

            //Break apart each sized group into bins that equal the number of CPU's so we can maximize speed.
            (int Cpu, List<TSource> Bag)[] threads = new (int Cpu, List<TSource> Bag)[Environment.ProcessorCount];
            int BagSize = (sList.Count / Environment.ProcessorCount) + 1;

            int cpui;
            for (cpui = 0; cpui < threads.Length; cpui++)
            {
                threads[cpui].Cpu = cpui;
                threads[cpui].Bag = new List<TSource>(BagSize);
            }

            cpui = 0;

            foreach (TSource dr in sList)
            {
                threads[cpui++].Bag.Add(dr);

                if (cpui == threads.Length)
                {
                    cpui = 0;
                }
            }

            _ = Parallel.ForEach(threads.ToList() as IEnumerable<(int Index, List<TSource> Bag)>, body);
        }

        #endregion Methods
    }
}
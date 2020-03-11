using System;
using System.Collections.Generic;
using System.Text;

namespace Penguin.Analysis
{
    public struct Accuracy
    {
        public double Current { get; set; }

        public double Next { get; set; }

        public Accuracy(int pool, int current)
        {
            Current = current == 0 ? 0 : (float)current / (float)pool;
            Next = (float)(current + 1) / (float)(pool + 1);
        }
    }
}
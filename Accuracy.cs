using System;

namespace Penguin.Analysis
{
    public struct Accuracy : IEquatable<Accuracy>
    {
        public double Current { get; set; }

        public double Next { get; set; }

        public Accuracy(int pool, int current)
        {
            Current = current == 0 ? 0 : current / (float)pool;
            Next = (current + 1) / (float)(pool + 1);
        }

        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public static bool operator ==(Accuracy left, Accuracy right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Accuracy left, Accuracy right)
        {
            return !(left == right);
        }

        public bool Equals(Accuracy other)
        {
            throw new NotImplementedException();
        }
    }
}
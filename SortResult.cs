namespace Penguin.Analysis
{
    public struct SortResult : System.IEquatable<SortResult>
    {
        public sbyte Header;
        public int Matches;
        public long Offset;

        public override bool Equals(object obj)
        {
            throw new System.NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new System.NotImplementedException();
        }

        public static bool operator ==(SortResult left, SortResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SortResult left, SortResult right)
        {
            return !(left == right);
        }

        public bool Equals(SortResult other)
        {
            throw new System.NotImplementedException();
        }
    }
}
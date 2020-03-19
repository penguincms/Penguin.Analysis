namespace Penguin.Analysis
{
    public struct OffsetValue
    {
        public long Offset { get; set; }
        public ushort Value { get; set; }

        public override string ToString() => $"{Value}@{Offset}";
    }
}
namespace Penguin.Analysis
{
    public interface IResultsCollection
    {
        ref int this[MatchResult resultKind] { get; }
    }
}
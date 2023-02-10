namespace Penguin.Analysis.Interfaces
{
    public interface INodeBlock
    {
        long Offset { get; }
        long NextOffset { get; }
    }
}
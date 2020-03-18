using System;

namespace Penguin.Analysis
{
    [Flags]
    public enum MemoryManagementStyle
    {
        NoCache = 0,
        Standard = 1,
        Preload = 2,
        MemoryFlush = 4,
        PreloadAndFlush = Preload | MemoryFlush
    }
}
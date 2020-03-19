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
        Unmanaged = 8,
        PreloadAndFlush = Preload | MemoryFlush
    }
}
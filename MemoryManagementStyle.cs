using System;
using System.Collections.Generic;
using System.Text;

namespace Penguin.Analysis
{
    [Flags]
    public enum MemoryManagementStyle
    {
        None = 0,
        Preload = 1,
        MemoryFlush = 2,
        PreloadAndFlush = Preload | MemoryFlush
    }
}

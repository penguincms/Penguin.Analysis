using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Penguin.Analysis.SystemInterop
{
    public static class Memory
    {
        public static MEMORYSTATUSEX Status
        {
            get
            {
                MEMORYSTATUSEX memoryStatus = new MEMORYSTATUSEX
                {
                    dwLength = Marshal.SizeOf(typeof(MEMORYSTATUSEX))
                };
                if (!GlobalMemoryStatusEx(ref memoryStatus))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return memoryStatus;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public int dwLength;
            public int dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx([In, Out] ref MEMORYSTATUSEX lpBuffer);
    }
}
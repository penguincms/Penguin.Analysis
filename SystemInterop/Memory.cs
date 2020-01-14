using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Penguin.Analysis.SystemInterop
{
    public static class Memory
    {
        public static MEMORYSTATUSEX Status { 
            get
            {
                MEMORYSTATUSEX memoryStatus = new MEMORYSTATUSEX();
                memoryStatus.dwLength = (int)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (!GlobalMemoryStatusEx(ref memoryStatus))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return memoryStatus;
            } 
        }

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx([In, Out] ref MEMORYSTATUSEX lpBuffer);

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
    }
}

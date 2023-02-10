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
                MEMORYSTATUSEX memoryStatus = new()
                {
                    dwLength = Marshal.SizeOf(typeof(MEMORYSTATUSEX))
                };
                return !GlobalMemoryStatusEx(ref memoryStatus) ? throw new Win32Exception(Marshal.GetLastWin32Error()) : memoryStatus;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX : System.IEquatable<MEMORYSTATUSEX>
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

            public override bool Equals(object obj)
            {
                throw new System.NotImplementedException();
            }

            public override int GetHashCode()
            {
                throw new System.NotImplementedException();
            }

            public static bool operator ==(MEMORYSTATUSEX left, MEMORYSTATUSEX right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(MEMORYSTATUSEX left, MEMORYSTATUSEX right)
            {
                return !(left == right);
            }

            public bool Equals(MEMORYSTATUSEX other)
            {
                throw new System.NotImplementedException();
            }
        }

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx([In, Out] ref MEMORYSTATUSEX lpBuffer);
    }
}
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Penguin.Analysis
{
    public static class ScreenBuffer
    {
        #region Properties

        public static bool AutoFlush { get; set; } = true;

        /// <summary>
        /// Caps the frequency with which the screen can be flushed to help
        /// prevent multithread performance degredation
        /// </summary>
        public static int FlushDelayMs { get; set; } = 100;

        private static char[] Data { get; set; } = new char[Console.WindowWidth * Console.WindowHeight];
        private static int Height => Console.WindowHeight;
        private static char[] Last { get; set; } = new char[Console.WindowWidth * Console.WindowHeight];
        private static DateTime LastFlush { get; set; }
        private static int Pointer { get; set; } = 0;
        private static object ScreenBufferLock { get; set; } = new object();
        private static int Width { get; set; } = Console.WindowWidth;


        #endregion Properties

        #region Methods

        public static void CarriageReturn()
        {
            lock (ScreenBufferLock)
            {
                while (Pointer % Width != 0)
                {
                    Pointer--;
                }
            }
        }

        public static void Clear()
        {
            lock (ScreenBufferLock)
            {
                for (int i = 0; i < Data.Length; i++)
                {
                    Data[i] = '\0';
                }

                Flush(true);
            }
        }

        public static void ClearLine()
        {
            lock (ScreenBufferLock)
            {
                CarriageReturn();

                for (int i = 0; i < Width; i++)
                {
                    Data[i + Pointer] = '\0';
                }

                TryAutoFlush();
            }
        }

        public static void CRLF()
        {
            lock (ScreenBufferLock)
            {
                CarriageReturn();
                LineFeed();
            }
        }

        public static void Flush(bool Force = false)
        {
            if (Monitor.TryEnter(ScreenBufferLock) || Force)
            {
                if (Force)
                {
                    Monitor.Enter(ScreenBufferLock);
                }

                try
                {
                    if ((DateTime.Now - LastFlush).TotalMilliseconds > FlushDelayMs)
                    {
                        LastFlush = DateTime.Now;

                        Console.BufferHeight = Height;
                        Console.BufferWidth = Width;

                        for (int i = 0; i < Data.Length - 1; i++)
                        {
                            if (Last[i] != Data[i] || Force)
                            {
                                Console.SetCursorPosition(i % Width, i / Width);

                                Console.Write(Data[i]);

                                Last[i] = Data[i];
                            }
                        }

                        Console.SetCursorPosition(0, 0);

                        Console.SetCursorPosition(Pointer % Width, Pointer / Width);
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    Monitor.Exit(ScreenBufferLock);
                }
            }
        }

        public static void GotoLine(int LineNumber)
        {
            lock (ScreenBufferLock)
            {
                Pointer = Width * LineNumber;
            }
        }

        public static void LineFeed()
        {
            lock (ScreenBufferLock)
            {
                Pointer += Width;
            }
        }

        public static void ReplaceLine(string text)
        {
            lock (ScreenBufferLock)
            {
                CarriageReturn();

                string output = text;
                string Padding = new string('\0', (Width - output.Length) - 1);

                Write(output + Padding);

                Pointer -= Padding.Length;

                TryAutoFlush();
            }
        }

        public static void ReplaceLine(string text, int LineNumber)
        {
            int tpointer = Width * LineNumber;

            string output = text;
            output += new string('\0', (Width - output.Length) - 1);

            for (int i = 0; i < output.Length; i++)
            {
                Data[tpointer + i] = output[i];
            }

            TryAutoFlush();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TryAutoFlush()
        {
            if (AutoFlush)
            {
                Flush();
            }
        }

        public static void Write(string text)
        {
            lock (ScreenBufferLock)
            {
                foreach (char c in text)
                {
                    WriteChar(c);
                }

                TryAutoFlush();
            }
        }

        public static void WriteChar(char c)
        {
            lock (ScreenBufferLock)
            {
                if (Pointer >= Data.Length - 1)
                {
                    Pointer = Data.Length - Width;

                    for (int i = 0; i < Data.Length - Width; i++)
                    {
                        Data[i] = Data[i + Width];
                    }
                }

                Data[Pointer++] = c;
            }
        }

        public static void WriteLine(string text)
        {
            lock (ScreenBufferLock)
            {
                foreach (char c in text)
                {
                    WriteChar(c);
                }

                CRLF();

                TryAutoFlush();
            }
        }

        public static void WriteLine(string text, int LineNumber)
        {
            lock (ScreenBufferLock)
            {
                Pointer = Width * LineNumber;

                foreach (char c in text)
                {
                    WriteChar(c);
                }

                CRLF();

                TryAutoFlush();
            }
        }

        #endregion Methods
    }
}
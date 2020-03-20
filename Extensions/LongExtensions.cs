using System;
using System.Collections.Generic;
using System.Text;

namespace Penguin.Analysis.Extensions
{
    public static class LongExtensions
    {
        public static byte[] ToInt40Array(this long l)
        {
            byte[] toReturn = new byte[5];

            for(int i = 0; i < 5; i++)
            {
                toReturn[i] = (byte)((l >> (i * 8)) & 0xFF);
            }

            return toReturn;
        }
    }
}

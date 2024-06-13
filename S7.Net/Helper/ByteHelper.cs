using System;

namespace S7.Net.Helper
{
    internal static class ByteHelper
    {
        internal static void IncrementToEven(ref double numBytes)
        {
            numBytes = Math.Ceiling(numBytes);
            if (numBytes % 2 > 0) numBytes++;
        }
    }
}

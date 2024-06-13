using System;

namespace S7.Net.Types
{
    /// <summary>
    /// Contains the conversion methods to convert DInt from S7 plc to C# int (Int64 / long).
    /// </summary>
    public static class LInt
    {
        /// <summary>
        /// Converts a S7 DInt (4 bytes) to int (Int64)
        /// </summary>
        public static Int64 FromByteArray(byte[] bytes)
        {
            if (bytes.Length != 8)
                throw new ArgumentException("Wrong number of bytes. Bytes array must contain 8 bytes.");

            // bytes[0] -> HighByte
            // bytes[1] -> LowByte
            //return (short)((int)(bytes[1]) | ((int)(bytes[0]) << 8));
            return (Int64)(bytes[0] << 56 |
                   bytes[1] << 48 |
                   bytes[2] << 40 |
                   bytes[3] << 32 |
                   bytes[4] << 24 |
                   bytes[5] << 16 |
                   bytes[6] << 8 |
                   bytes[7]);
        }


        /// <summary>
        /// Converts an int (Int64) to S7 DInt (4 bytes)
        /// </summary>
        public static byte[] ToByteArray(Int64 value)
        {
            byte[] bytes = new byte[8];

            bytes[0] = (byte)((value >> 56) & 0xFF);
            bytes[1] = (byte)((value >> 48) & 0xFF);
            bytes[2] = (byte)((value >> 40) & 0xFF);
            bytes[3] = (byte)((value >> 32) & 0xFF);
            bytes[4] = (byte)((value >> 24) & 0xFF);
            bytes[5] = (byte)((value >> 16) & 0xFF);
            bytes[6] = (byte)((value >> 8) & 0xFF);
            bytes[7] = (byte)((value) & 0xFF);

            return bytes;
        }

        /// <summary>
        /// Converts an array of int (Int64) to an array of bytes
        /// </summary>
        public static byte[] ToByteArray(Int64[] value)
        {
            byte[] bytes = new byte[value.Length * 2];
            int bytesPos = 0;

            for(int i=0; i< value.Length; i++)
            {
                bytes[bytesPos++] = (byte)((value[i] >> 8) & 0xFF);
                bytes[bytesPos++] = (byte) (value[i] & 0xFF);
            }

            return bytes;
        }

        /// <summary>
        /// Converts an array of S7 LInt to an array of int (Int64)
        /// </summary>
        public static Int64[] ToArray(byte[] bytes)
        {
            int shortsCount = bytes.Length / 8;

            Int64[] values = new Int64[shortsCount];

            int counter = 0;
            for (int cnt = 0; cnt < shortsCount; cnt++)
                values[cnt] = FromByteArray(new byte[] { bytes[counter++], bytes[counter++], bytes[counter++], bytes[counter++], bytes[counter++], bytes[counter++], bytes[counter++], bytes[counter++] });

            return values;
        }
    }
}

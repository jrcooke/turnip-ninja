using System;

namespace AdfReader.NewFolder
{
    internal class MyBitConverter
    {
        private static byte[] buffer = new byte[8];

        internal static double ToDouble(byte[] value, int startIndex)
        {
            for (int i = 0; i < 8; i++)
            {
                buffer[8 - i - 1] = value[startIndex + i];
            }

            return BitConverter.ToDouble(buffer, 0);
        }

        internal static uint ToUInt32(byte[] value, int startIndex)
        {
            for (int i = 0; i < 4; i++)
            {
                buffer[4 - i - 1] = value[startIndex + i];
            }

            return BitConverter.ToUInt32(buffer, 0);
        }

        internal static int ToInt32(byte[] value, int startIndex)
        {
            for (int i = 0; i < 4; i++)
            {
                buffer[4 - i - 1] = value[startIndex + i];
            }

            return BitConverter.ToInt32(buffer, 0);
        }

        internal static float ToSingle(byte[] value, int startIndex)
        {
            for (int i = 0; i < 4; i++)
            {
                buffer[4 - i - 1] = value[startIndex + i];
            }

            return BitConverter.ToSingle(buffer, 0);
        }
    }
}
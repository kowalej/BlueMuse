using System;
using System.Collections;

namespace BlueMuse.Helpers
{
    public static class PacketConversion
    {
        const int BITS_UINT12 = 12;
        const int BITS_UINT16 = 16;

        public static UInt16 ToFakeUInt12(ref string binary, int offset = 0)
        {
            UInt16 value = 0;
            for (int i = 0; i < BITS_UINT12; i++)
            {
                if (binary[i + offset] == '1')
                    value += Convert.ToUInt16(Math.Pow(2, (BITS_UINT12 - 1) - i));
            }
            return value;
        }

        public static UInt16 ToUInt16(ref string binary, int offset = 0)
        {
            UInt16 value = 0;
            for (int i = 0; i < BITS_UINT16; i++)
            {
                if (binary[i + offset] == '1')
                    value += Convert.ToUInt16(Math.Pow(2, (BITS_UINT16 - 1) - i));
            }
            return value;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlueMuse.Helpers
{
    public static class PacketConversion
    {
        public static UInt16 ToUInt12(this BitArray binary, int offset = 0)
        {
            UInt16 value = 0;
            for (int i = 0; i < 12; i++)
            {
                if (binary[i + offset])
                    value += Convert.ToUInt16(Math.Pow(2, i));
            }
            return value;
        }

        public static UInt16 ToUInt16(this BitArray binary, int offset = 0)
        {
            UInt16 value = 0;
            for (int i = 0; i < 16; i++)
            {
                if (binary[i + offset])
                    value += Convert.ToUInt16(Math.Pow(2, i));
            }
            return value;
        }
    }
}

using System;
using TCPTCPGecko;

namespace GeckoApp
{
    public enum AddressType
    {
        Rw,
        Ro,
        Ex,
        Hardware,
        Unknown
    }

    public class AddressRange
    {

        public AddressType description { get; private set; }
        public Byte id { get; private set; }
        public UInt32 low { get; private set; }
        public UInt32 high { get; private set; }

        public AddressRange(AddressType desc, Byte id, UInt32 low, UInt32 high)
        {
            this.id = id;
            description = desc;
            this.low = low;
            this.high = high;
        }

        public AddressRange(AddressType desc, UInt32 low, UInt32 high) :
            this(desc, (Byte)(low >> 24), low, high)
        { }
    }

    public static class ValidMemory
    {
        public static bool addressDebug = false;

        public static readonly AddressRange[] ValidAreas = new AddressRange[] {
             new AddressRange(AddressType.Ex,  0x01000000,0x01800000),
             new AddressRange(AddressType.Ex,  0x0e300000,0x10000000),
             new AddressRange(AddressType.Rw,  0x10000000,0x45000000),
             new AddressRange(AddressType.Ro,  0xe0000000,0xe4000000),
             new AddressRange(AddressType.Ro,  0xe8000000,0xea000000),
             new AddressRange(AddressType.Ro,  0xf4000000,0xf6000000),
             new AddressRange(AddressType.Ro,  0xf6000000,0xf6800000),
             new AddressRange(AddressType.Ro,  0xf8000000,0xfb000000),
             new AddressRange(AddressType.Ro,  0xfb000000,0xfb800000),
             new AddressRange(AddressType.Rw,  0xfffe0000,0xffffffff)
        };

        public static AddressType rangeCheck(UInt32 address)
        {
            int id = rangeCheckId(address);
            if (id == -1)
                return AddressType.Unknown;
            else
                return ValidAreas[id].description;
        }

        public static int rangeCheckId(UInt32 address)
        {
            for (int i = 0; i < ValidAreas.Length; i++)
            {
                AddressRange range = ValidAreas[i];
                if (address >= range.low && address < range.high)
                    return i;
            }
            return -1;
        }

        public static bool validAddress(UInt32 address, bool debug)
        {
            if (debug)
                return true;
            return (rangeCheckId(address) >= 0);
        }

        public static bool validAddress(UInt32 address)
        {
            return validAddress(address, addressDebug);
        }

        public static bool validRange(UInt32 low, UInt32 high, bool debug)
        {
            if (debug)
                return true;
            return (rangeCheckId(low) == rangeCheckId(high - 1));
        }

        public static bool validRange(UInt32 low, UInt32 high)
        {
            return validRange(low, high, addressDebug);
        }

        public static void setDataUpper(TCPGecko upper)
        {
            UInt32 mem;
            switch (upper.OsVersionRequest())
            {
                case 400:
                case 410:
                    mem = upper.peek_kern(0xFFEB902C);
                    break;
                case 500:
                case 510:
                    mem = upper.peek_kern(0xFFEA9E4C);
                    break;
                case 532:
                case 540:
                    mem = upper.peek_kern(0xFFEAAA1C);
                    break;
                case 550:
                case 551:
                    mem = upper.peek_kern(0xFFEAB7AC);
                    return;
                default:
                    return;
            }
            UInt32 tbl = upper.peek_kern(mem + 4);
            UInt32 lst = upper.peek_kern(tbl + 20);

            UInt32 init_start = upper.peek_kern(lst + 0 + 0x00);
            UInt32 init_len = upper.peek_kern(lst + 4 + 0x00);
            UInt32 code_start = upper.peek_kern(lst + 0 + 0x10);
            UInt32 code_len = upper.peek_kern(lst + 4 + 0x10);
            UInt32 data_start = upper.peek_kern(lst + 0 + 0x20);
            UInt32 data_len = upper.peek_kern(lst + 4 + 0x20);
            ValidAreas[0] = new AddressRange(AddressType.Ex, init_start, init_start + init_len);
            ValidAreas[1] = new AddressRange(AddressType.Ex, code_start, code_start + code_len);
            ValidAreas[2] = new AddressRange(AddressType.Rw, data_start, data_start + data_len);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UsbNewDaq
{
    class CheckStringFormat
    {


        private const string rxHex = @"\b[0-9a-fA-F]{1,}\b";// Match 8 bytes Hex
        private const string rxByte = @"\b[0-9a-fA-F]{2}\b";// Match 8 bit Hex
        private const string rxInteger = @"^\d+$";   // Match positive integer
        private const string rxDouble = @"^-?\d+(\.\d+)?$";// Match double
        public static bool CheckHex(string HexInString)
        {
            Regex RegexHex = new Regex(rxHex);
            return RegexHex.IsMatch(HexInString);
        }
        public static bool CheckHexByte(string Hex8Bits)
        {
            Regex RegexHexByte = new Regex(rxByte);
            return RegexHexByte.IsMatch(Hex8Bits);
        }
        public static bool CheckInteger(string IntegerInString)
        {
            Regex rxInt = new Regex(rxInteger);
            return rxInt.IsMatch(IntegerInString);
        }
        public static bool CheckDouble(string DoubleInString)
        {
            Regex RegexDouble = new Regex(rxDouble);
            return RegexDouble.IsMatch(DoubleInString);
        }

    }
}

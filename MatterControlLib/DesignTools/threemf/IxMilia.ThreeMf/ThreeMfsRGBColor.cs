using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IxMilia.ThreeMf
{
    public struct ThreeMfsRGBColor
    {
        private static Regex ColorPattern = new Regex("^#([0-9A-F]{2}){3,4}$", RegexOptions.IgnoreCase);

        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; }

        public ThreeMfsRGBColor(byte r, byte g, byte b)
            : this(r, g, b, 255)
        {
        }

        public ThreeMfsRGBColor(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static bool operator==(ThreeMfsRGBColor a, ThreeMfsRGBColor b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
        }

        public static bool operator!=(ThreeMfsRGBColor a, ThreeMfsRGBColor b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj is ThreeMfsRGBColor)
            {
                return this == (ThreeMfsRGBColor)obj;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return R.GetHashCode() ^ G.GetHashCode() ^ B.GetHashCode() ^ A.GetHashCode();
        }

        public override string ToString()
        {
            return $"#{R:X2}{G:X2}{B:X2}{A:X2}";
        }

        public static ThreeMfsRGBColor Parse(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!ColorPattern.IsMatch(value))
            {
                throw new ThreeMfParseException("Invalid color string.");
            }

            var u = uint.Parse(value.Substring(1), NumberStyles.HexNumber);
            if (value.Length == 7)
            {
                // if no alpha specified, assume 0xFF
                u <<= 8;
                u |= 0x000000FF;
            }

            // at this point `u` should be of the format '#RRGGBBAA'
            var r = (u & 0xFF000000) >> 24;
            var g = (u & 0x00FF0000) >> 16;
            var b = (u & 0x0000FF00) >> 8;
            var a = (u & 0x000000FF);

            return new ThreeMfsRGBColor((byte)r, (byte)g, (byte)b, (byte)a);
        }
    }
}

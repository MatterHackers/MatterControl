using System;

namespace IxMilia.ThreeMf
{
    public enum ThreeMfTileStyle
    {
        Wrap,
        Mirror,
        Clamp
    }

    internal static class ThreeMfTileStyleExtensions
    {
        private const string WrapStyle = "wrap";
        private const string MirrorStyle = "mirror";
        private const string ClampStyle = "clamp";

        public static string ToTileStyleString(this ThreeMfTileStyle tileStyle)
        {
            switch (tileStyle)
            {
                case ThreeMfTileStyle.Wrap:
                    return WrapStyle;
                case ThreeMfTileStyle.Mirror:
                    return MirrorStyle;
                case ThreeMfTileStyle.Clamp:
                    return ClampStyle;
                default:
                    throw new InvalidOperationException();
            }
        }

        public static ThreeMfTileStyle ParseTileStyle(string tileStyle)
        {
            switch (tileStyle)
            {
                case WrapStyle:
                case null:
                    return ThreeMfTileStyle.Wrap;
                case MirrorStyle:
                    return ThreeMfTileStyle.Mirror;
                case ClampStyle:
                    return ThreeMfTileStyle.Clamp;
                default:
                    throw new ThreeMfParseException($"Invalid tile style '{tileStyle}'.");
            }
        }
    }
}

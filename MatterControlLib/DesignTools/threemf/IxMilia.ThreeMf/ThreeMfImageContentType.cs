using System;

namespace IxMilia.ThreeMf
{
    public enum ThreeMfImageContentType
    {
        Jpeg,
        Png
    }

    internal static class ThreeMfImageContentTypeExtensions
    {
        private const string JpegContentType = "image/jpeg";
        private const string PngContentType = "image/png";

        public static string ToContentTypeString(this ThreeMfImageContentType contentType)
        {
            switch (contentType)
            {
                case ThreeMfImageContentType.Jpeg:
                    return JpegContentType;
                case ThreeMfImageContentType.Png:
                    return PngContentType;
                default:
                    throw new InvalidOperationException();
            }
        }

        public static string ToExtensionString(this ThreeMfImageContentType contentType)
        {
            switch (contentType)
            {
                case ThreeMfImageContentType.Jpeg:
                    return ".jpg";
                case ThreeMfImageContentType.Png:
                    return ".png";
                default:
                    throw new InvalidOperationException();
            }
        }

        public static ThreeMfImageContentType ParseContentType(string contentType)
        {
            switch (contentType)
            {
                case JpegContentType:
                    return ThreeMfImageContentType.Jpeg;
                case PngContentType:
                    return ThreeMfImageContentType.Png;
                default:
                    throw new ThreeMfParseException($"Invalid image content type '{contentType}'.");
            }
        }
    }
}

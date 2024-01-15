using System;
using System.Xml.Linq;

namespace IxMilia.ThreeMf
{
    public struct ThreeMfBoundingBox
    {
        internal const string BoundingBoxAttributeName = "box";

        public double U { get; set; }
        public double V { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public bool IsDefault => U == 0.0 && V == 0.0 && Width == 1.0 && Height == 1.0;

        public ThreeMfBoundingBox(double u, double v, double width, double height)
        {
            U = u;
            V = v;
            Width = width;
            Height = height;
        }

        public static ThreeMfBoundingBox Default => new ThreeMfBoundingBox(0.0, 0.0, 1.0, 1.0);

        internal XAttribute ToXAttribute()
        {
            if (IsDefault)
            {
                // default
                return null;
            }

            return new XAttribute(BoundingBoxAttributeName, $"{U} {V} {Width} {Height}");
        }

        internal static ThreeMfBoundingBox ParseBoundingBox(string value)
        {
            if (value == null)
            {
                return ThreeMfBoundingBox.Default;
            }

            var parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4)
            {
                throw new ThreeMfParseException($"Bounding box requires 4 values.");
            }

            if (!double.TryParse(parts[0], out var u) ||
                !double.TryParse(parts[1], out var v) ||
                !double.TryParse(parts[2], out var width) ||
                !double.TryParse(parts[3], out var height))
            {
                throw new ThreeMfParseException("Invalid value in bounding box.");
            }

            return new ThreeMfBoundingBox(u, v, width, height);
        }
    }
}

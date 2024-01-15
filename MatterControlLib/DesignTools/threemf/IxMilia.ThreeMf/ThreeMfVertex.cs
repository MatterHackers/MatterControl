using System.Xml.Linq;
using IxMilia.ThreeMf.Extensions;

namespace IxMilia.ThreeMf
{
    public struct ThreeMfVertex
    {
        private const string XAttributeName = "x";
        private const string YAttributeName = "y";
        private const string ZAttributeName = "z";

        internal static XName VertexName = XName.Get("vertex", ThreeMfModel.ModelNamespace);

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public ThreeMfVertex(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        internal XElement ToXElement()
        {
            return new XElement(VertexName,
                new XAttribute(XAttributeName, X),
                new XAttribute(YAttributeName, Y),
                new XAttribute(ZAttributeName, Z));
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }

        public static bool operator ==(ThreeMfVertex a, ThreeMfVertex b)
        {
            if (ReferenceEquals(a, b))
                return true;
            return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        }

        public static bool operator !=(ThreeMfVertex a, ThreeMfVertex b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is ThreeMfVertex)
                return this == (ThreeMfVertex)obj;
            return false;
        }

        internal static ThreeMfVertex ParseVertex(XElement element)
        {
            var x = element.AttributeDoubleValueOrThrow(XAttributeName);
            var y = element.AttributeDoubleValueOrThrow(YAttributeName);
            var z = element.AttributeDoubleValueOrThrow(ZAttributeName);
            return new ThreeMfVertex(x, y, z);
        }
    }
}

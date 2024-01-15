using System.Collections.Generic;
using System.Xml.Linq;
using IxMilia.ThreeMf.Extensions;

namespace IxMilia.ThreeMf
{
    public struct ThreeMfTriangle
    {
        private const string V1AttributeName = "v1";
        private const string V2AttributeName = "v2";
        private const string V3AttributeName = "v3";
        private const string PropertyIndexAttributeName = "pid";
        private const string V1PropertyAttributeName = "p1";
        private const string V2PropertyAttributeName = "p2";
        private const string V3PropertyAttributeName = "p3";

        internal static XName TriangleName = XName.Get("triangle", ThreeMfModel.ModelNamespace);

        public ThreeMfVertex V1 { get; set; }
        public ThreeMfVertex V2 { get; set; }
        public ThreeMfVertex V3 { get; set; }

        public IThreeMfPropertyResource PropertyResource { get; set; }
        public int? V1PropertyIndex { get; set; }
        public int? V2PropertyIndex { get; set; }
        public int? V3PropertyIndex { get; set; }

        public ThreeMfTriangle(ThreeMfVertex v1, ThreeMfVertex v2, ThreeMfVertex v3)
        {
            V1 = v1;
            V2 = v2;
            V3 = v3;
            PropertyResource = null;
            V1PropertyIndex = null;
            V2PropertyIndex = null;
            V3PropertyIndex = null;
        }

        internal XElement ToXElement(Dictionary<ThreeMfVertex, int> vertices, Dictionary<ThreeMfResource, int> resourceMap)
        {
            return new XElement(TriangleName,
                new XAttribute(V1AttributeName, vertices[V1]),
                new XAttribute(V2AttributeName, vertices[V2]),
                new XAttribute(V3AttributeName, vertices[V3]),
                PropertyResource == null
                    ? null
                    : new[]
                    {
                        new XAttribute(PropertyIndexAttributeName, resourceMap[(ThreeMfResource)PropertyResource]),
                        V1PropertyIndex == null ? null : new XAttribute(V1PropertyAttributeName, V1PropertyIndex.GetValueOrDefault()),
                        V2PropertyIndex == null ? null : new XAttribute(V2PropertyAttributeName, V2PropertyIndex.GetValueOrDefault()),
                        V3PropertyIndex == null ? null : new XAttribute(V3PropertyAttributeName, V3PropertyIndex.GetValueOrDefault())
                    });
        }

        internal static ThreeMfTriangle ParseTriangle(XElement triangleElement, IList<ThreeMfVertex> vertices, Dictionary<int, ThreeMfResource> resourceMap)
        {
            var v1Index = triangleElement.AttributeIntValueOrThrow(V1AttributeName);
            var v2Index = triangleElement.AttributeIntValueOrThrow(V2AttributeName);
            var v3Index = triangleElement.AttributeIntValueOrThrow(V3AttributeName);

            if (v1Index == v2Index || v1Index == v3Index || v2Index == v3Index)
            {
                throw new ThreeMfParseException("Triangle must specify distinct indices.");
            }

            if (v1Index < 0 || v1Index >= vertices.Count ||
                v2Index < 0 || v2Index >= vertices.Count ||
                v3Index < 0 || v3Index >= vertices.Count)
            {
                throw new ThreeMfParseException("Triangle vertex index does not exist.");
            }

            var triangle = new ThreeMfTriangle(vertices[v1Index], vertices[v2Index], vertices[v3Index]);
            if (resourceMap.TryGetPropertyResource(triangleElement, PropertyIndexAttributeName, out var propertyResource))
            {
                triangle.PropertyResource = propertyResource;
                triangle.V1PropertyIndex = propertyResource.ParseAndValidateOptionalResourceIndex(triangleElement, V1PropertyAttributeName);
                triangle.V2PropertyIndex = propertyResource.ParseAndValidateOptionalResourceIndex(triangleElement, V2PropertyAttributeName);
                triangle.V3PropertyIndex = propertyResource.ParseAndValidateOptionalResourceIndex(triangleElement, V3PropertyAttributeName);
            }

            return triangle;
        }
    }
}

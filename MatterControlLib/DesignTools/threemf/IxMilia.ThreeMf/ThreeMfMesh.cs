using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace IxMilia.ThreeMf
{
    public class ThreeMfMesh
    {
        private static XName VerticesName = XName.Get("vertices", ThreeMfModel.ModelNamespace);
        private static XName TrianglesName = XName.Get("triangles", ThreeMfModel.ModelNamespace);

        public IList<ThreeMfTriangle> Triangles { get; } = new List<ThreeMfTriangle>();

        internal XElement ToXElement(Dictionary<ThreeMfResource, int> resourceMap)
        {
            var vertices = Triangles.SelectMany(t => new[] { t.V1, t.V2, t.V3 }).Distinct().Select((v, i) => new { v, i }).ToDictionary(x => x.v, x => x.i);
            var verticesXml = vertices.Select(v => v.Key.ToXElement());
            var trianglesXml = Triangles.Select(t => t.ToXElement(vertices, resourceMap));
            return new XElement(ThreeMfObject.MeshName,
                new XElement(VerticesName, verticesXml),
                new XElement(TrianglesName, trianglesXml));
        }

        internal static ThreeMfMesh ParseMesh(XElement element, Dictionary<int, ThreeMfResource> resourceMap)
        {
            if (element == null)
            {
                throw new ThreeMfParseException("Missing element <mesh>.");
            }

            var vertices = new List<ThreeMfVertex>();
            foreach (var vertexElement in element.Element(VerticesName).Elements(ThreeMfVertex.VertexName))
            {
                var vertex = ThreeMfVertex.ParseVertex(vertexElement);
                vertices.Add(vertex);
            }

            var mesh = new ThreeMfMesh();
            foreach (var triangleElement in element.Element(TrianglesName).Elements(ThreeMfTriangle.TriangleName))
            {
                var triangle = ThreeMfTriangle.ParseTriangle(triangleElement, vertices, resourceMap);
                mesh.Triangles.Add(triangle);
            }

            return mesh;
        }
    }
}

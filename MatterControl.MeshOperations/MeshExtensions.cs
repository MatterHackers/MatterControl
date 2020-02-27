using System;
using g3;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public static class MeshExtensions
	{
		public static DMesh3 ToDMesh3(this Mesh inMesh)
		{
			var outMesh = new DMesh3();
			foreach (var vertex in inMesh.Vertices)
			{
				outMesh.AppendVertex(new Vector3d(vertex.X, vertex.Y, vertex.Z));
			}

			foreach (var face in inMesh.Faces)
			{
				outMesh.AppendTriangle(face.v0, face.v1, face.v2);
			}

			return outMesh;
		}

		public static Mesh ToMesh(this DMesh3 mesh)
		{
			var outMesh = new Mesh();
			int[] mapV = new int[mesh.MaxVertexID];
			int nAccumCountV = 0;
			foreach (int vi in mesh.VertexIndices())
			{
				mapV[vi] = nAccumCountV++;
				Vector3d v = mesh.GetVertex(vi);
				outMesh.Vertices.Add(new Vector3(v[0], v[1], v[2]));
			}

			foreach (int ti in mesh.TriangleIndices())
			{
				Index3i t = mesh.GetTriangle(ti);
				t[0] = mapV[t[0]];
				t[1] = mapV[t[1]];
				t[2] = mapV[t[2]];
				outMesh.Faces.Add(t[0], t[1], t[2], outMesh.Vertices);
			}

			return outMesh;
		}
	}
}

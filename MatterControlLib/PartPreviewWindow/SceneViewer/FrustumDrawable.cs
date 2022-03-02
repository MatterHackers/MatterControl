using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class FrustumDrawable : IDrawable
	{
		public FrustumDrawable()
		{
		}

		string IDrawable.Title => "Frustum visualization";

		string IDrawable.Description => "When enabled, captures the current frustum and visualizes it.";

		bool _enabled = false;

		bool IDrawable.Enabled
		{
			get => _enabled;
			set
			{
				_enabled = value;
				meshes.Clear();
			}
		}

		DrawStage IDrawable.DrawStage => DrawStage.TransparentContent;

		readonly List<(Mesh mesh, Color color)> meshes = new List<(Mesh, Color)>();

		static Mesh GetMesh(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
		{
			Mesh mesh = new Mesh();
			mesh.Vertices.Add(a);
			mesh.Vertices.Add(b);
			mesh.Vertices.Add(c);
			mesh.Vertices.Add(d);
			mesh.Faces.Add(0, 1, 2, mesh.Vertices);
			mesh.Faces.Add(0, 2, 3, mesh.Vertices);
			return mesh;
		}

		void IDrawable.Draw(GuiWidget sender, DrawEventArgs e, Matrix4X4 itemMaxtrix, WorldView world)
		{
			if (meshes.Count == 0)
			{
				Vector3[] ndcCoords = new Vector3[] {
					new Vector3(+1, +1, +1), // 0: far top right
					new Vector3(-1, +1, +1), // 1: far top left
					new Vector3(+1, -1, +1), // 2: far bottom right
					new Vector3(-1, -1, +1), // 3: far bottom left
					new Vector3(+1, +1, -1), // 4: near top right
					new Vector3(-1, +1, -1), // 5: near top left
					new Vector3(+1, -1, -1), // 6: near bottom right
					new Vector3(-1, -1, -1), // 7: near bottom left
				};

				Vector3[] worldspaceCoords = ndcCoords.Select(p => world.NDCToViewspace(p).TransformPosition(world.InverseModelviewMatrix)).ToArray();

				// X
				meshes.Add((GetMesh(worldspaceCoords[1], worldspaceCoords[5], worldspaceCoords[7], worldspaceCoords[3]), Color.Red.WithAlpha(0.5)));
				meshes.Add((GetMesh(worldspaceCoords[0], worldspaceCoords[2], worldspaceCoords[6], worldspaceCoords[4]), Color.Red.WithAlpha(0.5)));

				// Y
				meshes.Add((GetMesh(worldspaceCoords[3], worldspaceCoords[7], worldspaceCoords[6], worldspaceCoords[2]), Color.Green.WithAlpha(0.5)));
				meshes.Add((GetMesh(worldspaceCoords[1], worldspaceCoords[0], worldspaceCoords[4], worldspaceCoords[5]), Color.Green.WithAlpha(0.5)));

				// Z
				meshes.Add((GetMesh(worldspaceCoords[0], worldspaceCoords[1], worldspaceCoords[3], worldspaceCoords[2]), Color.Blue.WithAlpha(0.5)));
				meshes.Add((GetMesh(worldspaceCoords[4], worldspaceCoords[5], worldspaceCoords[7], worldspaceCoords[6]), Color.Blue.WithAlpha(0.5)));
			}

			GL.Disable(EnableCap.Lighting);

			foreach (var mesh in meshes)
			{
				GLHelper.Render(mesh.mesh, mesh.color, forceCullBackFaces: false);
			}

			GL.Enable(EnableCap.Lighting);
		}

		AxisAlignedBoundingBox IDrawable.GetWorldspaceAABB()
		{
			var box = AxisAlignedBoundingBox.Empty();

			foreach (var mesh in meshes)
			{
				box = AxisAlignedBoundingBox.Union(box, mesh.mesh.GetAxisAlignedBoundingBox());
			}

			return box;
		}
	}
}
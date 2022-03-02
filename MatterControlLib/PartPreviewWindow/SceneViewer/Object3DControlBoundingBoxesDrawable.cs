using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class Object3DControlBoundingBoxesDrawable : IDrawable
	{
		string IDrawable.Title => "Object3DControlsLayer bounding boxes";

		string IDrawable.Description => "When enabled, show all the bounding boxes reported by Object3DControlsLayer.";

		bool IDrawable.Enabled { get; set; }

		DrawStage IDrawable.DrawStage => DrawStage.TransparentContent;

		void IDrawable.Draw(GuiWidget sender, DrawEventArgs e, Matrix4X4 itemMaxtrix, WorldView world)
		{
			if (!(sender is Object3DControlsLayer layer))
				return;

			GLHelper.PrepareFor3DLineRender(false);

			var frustum = world.GetClippingFrustum();

			Color color = Color.White;

			List<AxisAlignedBoundingBox> aabbs = layer.MakeListOfObjectControlBoundingBoxes();
			aabbs.Add(layer.GetPrinterNozzleAABB());

			foreach (var box in aabbs)
			{
				if (box.XSize < 0)
					continue;

				Vector3[] v = box.GetCorners();

				Tuple<Vector3, Vector3>[] lines = new Tuple<Vector3, Vector3>[]{
					new Tuple<Vector3, Vector3>(v[0], v[1]),
					new Tuple<Vector3, Vector3>(v[2], v[3]),
					new Tuple<Vector3, Vector3>(v[0], v[3]),
					new Tuple<Vector3, Vector3>(v[1], v[2]),
					new Tuple<Vector3, Vector3>(v[4 + 0], v[4 + 1]),
					new Tuple<Vector3, Vector3>(v[4 + 2], v[4 + 3]),
					new Tuple<Vector3, Vector3>(v[4 + 0], v[4 + 3]),
					new Tuple<Vector3, Vector3>(v[4 + 1], v[4 + 2]),
					new Tuple<Vector3, Vector3>(v[0], v[4 + 0]),
					new Tuple<Vector3, Vector3>(v[1], v[4 + 1]),
					new Tuple<Vector3, Vector3>(v[2], v[4 + 2]),
					new Tuple<Vector3, Vector3>(v[3], v[4 + 3]),
				};

				foreach (var (start, end) in lines)
				{
					world.Render3DLineNoPrep(frustum, start, end, color);
					//e.Graphics2D.DrawLine(color, world.GetScreenPosition(start), world.GetScreenPosition(end));
				}
			}

			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.DepthTest);
		}

		AxisAlignedBoundingBox IDrawable.GetWorldspaceAABB()
		{
			// Let's not recurse on this...
			return AxisAlignedBoundingBox.Empty();
		}
	}
}

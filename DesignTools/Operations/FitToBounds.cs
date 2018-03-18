/*
Copyright (c) 2018, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Drawing;
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public enum MaintainRatio { None, X_Y, X_Y_Z }

	public class FitToBounds : Object3D, IRebuildable, IEditorDraw
	{
		public double Width { get; set; }
		public double Depth { get; set; }
		public double Height { get; set; }

		public MaintainRatio MaintainRatio { get; set; } = MaintainRatio.X_Y;
		public bool StretchX { get; set; } = true;
		public bool StretchY { get; set; } = true;
		public bool StretchZ { get; set; } = true;

		IObject3D ScaleItem => Children.First();
		IObject3D ItemToScale => Children.First().Children.First();

		public FitToBounds()
		{
		}

		protected override void OnInvalidate()
		{
			// If the child bounds changed than adjust the scale control
			base.OnInvalidate();
		}

		public static FitToBounds Create(IObject3D itemToFit)
		{
			FitToBounds fitToBounds = new FitToBounds();
			var aabb = itemToFit.GetAxisAlignedBoundingBox();

			fitToBounds.Width = aabb.XSize;
			fitToBounds.Depth = aabb.YSize;
			fitToBounds.Height = aabb.ZSize;

			var scaleItem = new Object3D();
			fitToBounds.Children.Add(scaleItem);
			scaleItem.Children.Add(itemToFit);

			return fitToBounds;
		}

		public void Rebuild(UndoBuffer undoBuffer)
		{
			var aabb = this.GetAxisAlignedBoundingBox();

			AdjustChildSize(null, null);

			if (aabb.ZSize > 0)
			{
				// If the part was already created and at a height, maintain the height.
				PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
			}
		}

		private void AdjustChildSize(object sender, EventArgs e)
		{
			var aabb = ItemToScale.GetAxisAlignedBoundingBox();
			ScaleItem.Matrix = Matrix4X4.Identity;
			var scale = Vector3.One;
			if (StretchX)
			{
				scale.X = Width / aabb.XSize;
			}
			if (StretchY)
			{
				scale.Y = Depth / aabb.YSize;
			}
			if (StretchZ)
			{
				scale.Z = Height / aabb.ZSize;
			}

			switch (MaintainRatio)
			{
				case MaintainRatio.None:
					break;
				case MaintainRatio.X_Y:
					var minXy = Math.Min(scale.X, scale.Y);
					scale.X = minXy;
					scale.Y = minXy;
					break;
				case MaintainRatio.X_Y_Z:
					var minXyz = Math.Min(Math.Min(scale.X, scale.Y), scale.Z);
					scale.X = minXyz;
					scale.Y = minXyz;
					scale.Z = minXyz;
					break;
			}
			ScaleItem.Matrix = Object3DExtensions.ApplyAtPosition(ScaleItem.Matrix, Matrix4X4.CreateScale(scale), aabb.Center);
		}

		public void DrawEditor(object sender, DrawEventArgs e)
		{
			if (sender is InteractionLayer layer
				&& layer.Scene.SelectedItem == this)
			{
				var aabb = ItemToScale.GetAxisAlignedBoundingBox();
				var center = aabb.Center;
				var worldMatrix = this.WorldMatrix();
				var minXyz = center - new Vector3(Width / 2, Depth / 2, Height / 2);
				var maxXyz = center + new Vector3(Width / 2, Depth / 2, Height / 2);
				var bounds = new AxisAlignedBoundingBox(minXyz, maxXyz);
				//var leftW = Vector3.Transform(, worldMatrix);
				var right = Vector3.Transform(center + new Vector3(Width / 2, 0, 0), worldMatrix);
				// GLHelper.Render3DLine(layer.World, left, right, Agg.Color.Red);
				layer.World.RenderAabb(bounds, worldMatrix, Agg.Color.Red, 1);
				// turn the lighting back on
				GL.Enable(EnableCap.Lighting);
			}
		}
	}
}
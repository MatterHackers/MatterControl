﻿/*
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

/*********************************************************************/
/**************************** OBSOLETE! ******************************/
/************************ USE NEWER VERSION **************************/
/*********************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
    [Obsolete("Use CurveObject3D_3 instead", false)]
	public class CurveObject3D : MeshWrapperObject3D, IEditorDraw
	{
		// holds where we rotate the object
		private Vector2 rotationCenter;

		public CurveObject3D()
		{
			Name = "Curve".Localize();
		}

		[DisplayName("Bend Up")]
		public bool BendCcw { get; set; } = true;

		[Slider(1, 400, Easing.EaseType.Quadratic)]
		public double Diameter { get; set; } = double.MinValue;

		[Slider(3, 360, Easing.EaseType.Cubic, snapDistance: 1)]
		[Description("Ensures the rotated part has a minimum number of sides per complete rotation")]
		public double MinSidesPerRotation { get; set; } = 3;

		[Slider(0, 100, snapDistance: 1)]
		[Description("Where to start the bend as a percent of the width of the part")]
		public double StartPercent { get; set; } = 50;

		public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
		{
			if (layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem.DescendantsAndSelf().Where((i) => i == this).Any())
			{
				// we want to measure the
				var currentMatrixInv = Matrix.Inverted;
				var aabb = this.GetAxisAlignedBoundingBox(currentMatrixInv);

				layer.World.RenderCylinderOutline(this.WorldMatrix(), new Vector3(rotationCenter, aabb.Center.Z), Diameter, aabb.ZSize, 30, Color.Red);
			}

			// turn the lighting back on
			GL.Enable(EnableCap.Lighting);
		}

		public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
		{
			if (layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem.DescendantsAndSelf().Where((i) => i == this).Any())
			{
				var currentMatrixInv = Matrix.Inverted;
				var aabb = this.GetAxisAlignedBoundingBox(currentMatrixInv);
				return AxisAlignedBoundingBox.CenteredBox(new Vector3(Diameter, Diameter, aabb.ZSize), new Vector3(rotationCenter, aabb.Center.Z)).NewTransformed(this.WorldMatrix());
			}
			
			return AxisAlignedBoundingBox.Empty();
		}

		public override void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Mesh))
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				Rebuild();
			}
			else if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				Rebuild();
			}
			else if (Expressions.NeedRebuild(this, invalidateArgs))
			{
				Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			bool valuesChanged = Diameter == double.MinValue;
			if (StartPercent < 0
				|| StartPercent > 100)
			{
				StartPercent = Math.Min(100, Math.Max(0, StartPercent));
				valuesChanged = true;
			}

			using (RebuildLock())
			{
				ResetMeshWrapperMeshes(Object3DPropertyFlags.All, CancellationToken.None);

				// remember the current matrix then clear it so the parts will rotate at the original wrapped position
				var currentMatrix = Matrix;
				Matrix = Matrix4X4.Identity;

				var meshWrapperEnumerator = WrappedObjects();

				var aabb = this.GetAxisAlignedBoundingBox();

				if (Diameter == double.MinValue)
				{
					// uninitialized set to a reasonable value
					Diameter = (int)aabb.XSize;
					// TODO: ensure that the editor display value is updated
				}

				if (Diameter > 0)
				{
					var radius = Diameter / 2;
					var circumference = MathHelper.Tau * radius;
					rotationCenter = new Vector2(aabb.MinXYZ.X + (aabb.MaxXYZ.X - aabb.MinXYZ.X) * (StartPercent / 100), aabb.MaxXYZ.Y + radius);
					foreach (var object3Ds in meshWrapperEnumerator)
					{
						var matrix = object3Ds.original.WorldMatrix(this);
						if (!BendCcw)
						{
							// rotate around so it will bend correctly
							matrix *= Matrix4X4.CreateTranslation(0, -aabb.MaxXYZ.Y, 0);
							matrix *= Matrix4X4.CreateRotationX(MathHelper.Tau / 2);
							matrix *= Matrix4X4.CreateTranslation(0, aabb.MaxXYZ.Y - aabb.YSize, 0);
						}

						var matrixInv = matrix.Inverted;

						var curvedMesh = object3Ds.meshCopy.Mesh;

						for (int i = 0; i < curvedMesh.Vertices.Count; i++)
						{
							var worldPosition = curvedMesh.Vertices[i].Transform((Matrix4X4)matrix);

							var angleToRotate = ((worldPosition.X - rotationCenter.X) / circumference) * MathHelper.Tau - MathHelper.Tau / 4;
							var distanceFromCenter = rotationCenter.Y - worldPosition.Y;

							var rotatePosition = new Vector3Float(Math.Cos(angleToRotate), Math.Sin(angleToRotate), 0) * distanceFromCenter;
							rotatePosition.Z = worldPosition.Z;
							var worldWithBend = rotatePosition + new Vector3Float(rotationCenter.X, radius + aabb.MaxXYZ.Y, 0);
							curvedMesh.Vertices[i] = worldWithBend.Transform(matrixInv);
						}

						curvedMesh.CalculateNormals();
					}

					if (!BendCcw)
					{
						// fix the stored center so we draw correctly
						rotationCenter = new Vector2(rotationCenter.X, aabb.MinXYZ.Y - radius);
					}
				}

				// set the matrix back
				Matrix = currentMatrix;
			}

			this.CancelAllParentBuilding();
			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			Invalidate(InvalidateType.DisplayValues);

			return base.Rebuild();
		}
	}
}
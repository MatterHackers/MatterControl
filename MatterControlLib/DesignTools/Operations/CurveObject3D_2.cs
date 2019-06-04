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
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools
{
	public class CurveObject3D_2 : OperationSourceContainerObject3D, IEditorDraw
	{
		public CurveObject3D_2()
		{
			Name = "Curve".Localize();
		}

		[DisplayName("Bend Up")]
		public bool BendCcw { get; set; } = true;

		public double Diameter { get; set; } = double.MaxValue;

		[Range(3, 360, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
		[Description("Ensures the rotated part has a minimum number of sides per complete rotation")]
		public double MinSidesPerRotation { get; set; } = 30;

		[Range(0, 100, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
		[Description("Where to start the bend as a percent of the width of the part")]
		public double StartPercent { get; set; } = 50;

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			var sourceAabb = this.SourceContainer.GetAxisAlignedBoundingBox();
			var distance = Diameter / 2 + sourceAabb.YSize / 2;
			var center = sourceAabb.Center + new Vector3(0, BendCcw ? distance : -distance, 0);
			center.X -= sourceAabb.XSize / 2 - (StartPercent / 100.0) * sourceAabb.XSize;

			// render the top and bottom rings
			layer.World.RenderCylinderOutline(this.WorldMatrix(), center, Diameter, sourceAabb.ZSize, 100, Color.Red, Color.Transparent);

			// render the split lines
			var radius = Diameter / 2;
			var circumference = MathHelper.Tau * radius;
			var xxx = sourceAabb.XSize * (StartPercent / 100.0);
			var startAngle = MathHelper.Tau * 3 / 4 - xxx / circumference * MathHelper.Tau;
			layer.World.RenderCylinderOutline(this.WorldMatrix(), center, Diameter, sourceAabb.ZSize, (int)Math.Max(0, Math.Min(100, this.MinSidesPerRotation)), Color.Transparent, Color.Red, phase: startAngle);

			// turn the lighting back on
			GL.Enable(EnableCap.Lighting);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			bool valuesChanged = false;

			// ensure we have good values
			if (StartPercent < 0 || StartPercent > 100)
			{
				StartPercent = Math.Min(100, Math.Max(0, StartPercent));
				valuesChanged = true;
			}

			if (Diameter < 1 || Diameter > 100000)
			{
				if (Diameter == double.MaxValue)
				{
					var aabb = this.GetAxisAlignedBoundingBox();
					// uninitialized set to a reasonable value
					Diameter = (int)aabb.XSize;
				}

				Diameter = Math.Min(100000, Math.Max(1, Diameter));
				valuesChanged = true;
			}

			if (MinSidesPerRotation < 3 || MinSidesPerRotation > 360)
			{
				MinSidesPerRotation = Math.Min(360, Math.Max(3, MinSidesPerRotation));
				valuesChanged = true;
			}

			var rebuildLocks = this.RebuilLockAll();

			return ApplicationController.Instance.Tasks.Execute(
				"Curve".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					var sourceAabb = this.SourceContainer.GetAxisAlignedBoundingBox();

					var radius = Diameter / 2;
					var circumference = MathHelper.Tau * radius;
					double numRotations = sourceAabb.XSize / circumference;
					double numberOfCuts = numRotations * MinSidesPerRotation;
					double cutSize = sourceAabb.XSize / numberOfCuts;
					double cutPosition = sourceAabb.MinXYZ.X + cutSize;
					var cuts = new List<double>();
					for (int i = 0; i < numberOfCuts; i++)
					{
						cuts.Add(cutPosition);
						cutPosition += cutSize;
					}

					var rotationCenter = new Vector3(sourceAabb.MinXYZ.X + (sourceAabb.MaxXYZ.X - sourceAabb.MinXYZ.X) * (StartPercent / 100),
						BendCcw ? sourceAabb.MaxXYZ.Y + radius : sourceAabb.MinXYZ.Y - radius,
						sourceAabb.Center.Z);

					var curvedChildren = new List<IObject3D>();

					var status = new ProgressStatus();

					foreach (var sourceItem in SourceContainer.VisibleMeshes())
					{
						var originalMesh = sourceItem.Mesh;
						status.Status = "Copy Mesh".Localize();
						reporter.Report(status);
						var transformedMesh = originalMesh.Copy(CancellationToken.None);
						var itemMatrix = sourceItem.WorldMatrix(SourceContainer);

						// transform into this space
						transformedMesh.Transform(itemMatrix);

						status.Status = "Split Mesh".Localize();
						reporter.Report(status);

						// split the mesh along the x axis
						transformedMesh.SplitOnPlanes(Vector3.UnitX, cuts, cutSize / 8);

						for (int i = 0; i < transformedMesh.Vertices.Count; i++)
						{
							var position = transformedMesh.Vertices[i];

							var angleToRotate = ((position.X - rotationCenter.X) / circumference) * MathHelper.Tau - MathHelper.Tau / 4;
							var distanceFromCenter = rotationCenter.Y - position.Y;
							if (!BendCcw)
							{
								angleToRotate = -angleToRotate;
								distanceFromCenter = -distanceFromCenter;
							}

							var rotatePosition = new Vector3Float(Math.Cos(angleToRotate), Math.Sin(angleToRotate), 0) * distanceFromCenter;
							rotatePosition.Z = position.Z;
							transformedMesh.Vertices[i] = rotatePosition + new Vector3Float(rotationCenter.X, radius + sourceAabb.MaxXYZ.Y, 0);
						}

						// transform back into item local space
						transformedMesh.Transform(Matrix4X4.CreateTranslation(-rotationCenter) * itemMatrix.Inverted);

						status.Status = "Merge Vertices".Localize();
						reporter.Report(status);

						transformedMesh.MergeVertices(.1);
						transformedMesh.CalculateNormals();

						var curvedChild = new Object3D()
						{
							Mesh = transformedMesh
						};
						curvedChild.CopyWorldProperties(sourceItem, SourceContainer, Object3DPropertyFlags.All);
						curvedChild.Visible = true;
						curvedChild.Translate(new Vector3(rotationCenter));
						if (!BendCcw)
						{
							curvedChild.Translate(0, -sourceAabb.YSize - Diameter, 0);
						}

						curvedChildren.Add(curvedChild);
					}

					RemoveAllButSource();
					this.SourceContainer.Visible = false;

					this.Children.Modify((list) =>
					{
						list.AddRange(curvedChildren);
					});

					rebuildLocks.Dispose();

					if (valuesChanged)
					{
						Invalidate(InvalidateType.DisplayValues);
					}

					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));

					return Task.CompletedTask;
				});
		}
	}
}
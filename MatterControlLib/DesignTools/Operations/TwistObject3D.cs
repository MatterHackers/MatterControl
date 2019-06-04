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
	public class TwistObject3D : OperationSourceContainerObject3D, IPropertyGridModifier, IEditorDraw
	{
		public TwistObject3D()
		{
			Name = "Twist".Localize();
		}

		[Description("The angle to rotate the top of the part")]
		public double Angle { get; set; } = 135;

		[Range(3, 360, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
		[Description("Ensures the rotated part has a minimum number of sides per complete rotation")]
		public double MinCutsPerRotation { get; set; } = 60;

		[DisplayName("Twist Right")]
		public bool TwistCw { get; set; } = true;

		[Description("Allows for the repositioning of the rotation origin")]
		public Vector2 RotationOffset { get; set; }

		public bool Advanced { get; set; }

		public Easing.EaseType EasingType { get; set; } = Easing.EaseType.Linear;

		public Easing.EaseOption EasingOption { get; set; } = Easing.EaseOption.InOut;

		public double EndHeightPercent { get; set; } = 100;

		public double StartHeightPercent { get; set; } = 0;

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			var sourceAabb = this.SourceContainer.GetAxisAlignedBoundingBox();
			var center = sourceAabb.Center + new Vector3(RotationOffset);

			// render the top and bottom rings
			layer.World.RenderCylinderOutline(this.WorldMatrix(), center, 1, sourceAabb.ZSize, 15, Color.Red, Color.Red, 5);

			// turn the lighting back on
			GL.Enable(EnableCap.Lighting);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			bool valuesChanged = false;

			if (Angle < 1 || Angle > 100000)
			{
				Angle = Math.Min(100000, Math.Max(1, Angle));
				valuesChanged = true;
			}

			if (MinCutsPerRotation < 3 || MinCutsPerRotation > 360)
			{
				MinCutsPerRotation = Math.Min(360, Math.Max(3, MinCutsPerRotation));
				valuesChanged = true;
			}

			if (EndHeightPercent < 1 || EndHeightPercent > 100)
			{
				EndHeightPercent = Math.Min(100, Math.Max(1, EndHeightPercent));
				valuesChanged = true;
			}

			if (StartHeightPercent < 0 || StartHeightPercent > EndHeightPercent - 1)
			{
				StartHeightPercent = Math.Min(EndHeightPercent - 1, Math.Max(0, StartHeightPercent));
				valuesChanged = true;
			}

			var rebuildLocks = this.RebuilLockAll();

			return ApplicationController.Instance.Tasks.Execute(
				"Twist".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					var sourceAabb = this.SourceContainer.GetAxisAlignedBoundingBox();

					var bottom = sourceAabb.MinXYZ.Z;
					var top = sourceAabb.ZSize * EndHeightPercent / 100.0;
					var size = sourceAabb.ZSize;
					if (Advanced)
					{
						bottom += sourceAabb.ZSize * StartHeightPercent / 100.0;
						size = top - bottom;
					}

					double numberOfCuts = MinCutsPerRotation * (Angle / 360.0);
					double cutSize = size / numberOfCuts;
					var cuts = new List<double>();
					for (int i = 0; i < numberOfCuts + 1; i++)
					{
						var ratio = i / numberOfCuts;
						if (Advanced)
						{
							var goal = ratio;
							var current = .5;
							var next = .25;
							// look for an x value that equals the goal
							for (int j = 0; j < 64; j++)
							{
								var xAtY = Easing.Specify(EasingType, EasingOption, current);
								if (xAtY < goal)
								{
									current += next;
								}
								else if (xAtY > goal)
								{
									current -= next;
								}

								next *= .5;
							}

							ratio = current;
						}

						cuts.Add(bottom - cutSize + (size * ratio));
					}

					var rotationCenter = new Vector2(sourceAabb.Center) + RotationOffset;

					var twistedChildren = new List<IObject3D>();

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

						// split the mesh along the z axis
						transformedMesh.SplitOnPlanes(Vector3.UnitZ, cuts, cutSize / 8);

						for (int i = 0; i < transformedMesh.Vertices.Count; i++)
						{
							var position = transformedMesh.Vertices[i];

							var ratio = (position.Z - bottom) / size;

							if (Advanced)
							{
								if (position.Z < bottom)
								{
									ratio = 0;
								}
								else if (position.Z > top)
								{
									ratio = 1;
								}
								else
								{
									ratio = (position.Z - bottom) / size;
									ratio = Easing.Specify(EasingType, EasingOption, ratio);
								}
							}

							var angleToRotate = ratio * Angle / 360.0 * MathHelper.Tau;

							if (!TwistCw)
							{
								angleToRotate = -angleToRotate;
							}

							var positionXy = new Vector2(position) - rotationCenter;
							positionXy.Rotate(angleToRotate);
							positionXy += rotationCenter;
							transformedMesh.Vertices[i] = new Vector3Float(positionXy.X, positionXy.Y, position.Z);
						}

						// transform back into item local space
						transformedMesh.Transform(itemMatrix.Inverted);

						//transformedMesh.MergeVertices(.1);
						transformedMesh.CalculateNormals();

						var twistedChild = new Object3D()
						{
							Mesh = transformedMesh
						};
						twistedChild.CopyWorldProperties(sourceItem, SourceContainer, Object3DPropertyFlags.All);
						twistedChild.Visible = true;

						twistedChildren.Add(twistedChild);
					}

					RemoveAllButSource();
					this.SourceContainer.Visible = false;

					this.Children.Modify((list) =>
					{
						list.AddRange(twistedChildren);
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

		public void UpdateControls(PublicPropertyChange change)
		{
			if (change.Context.GetEditRow(nameof(EndHeightPercent)) is GuiWidget widget)
			{
				widget.Visible = Advanced;
			}

			if (change.Context.GetEditRow(nameof(StartHeightPercent)) is GuiWidget widget2)
			{
				widget2.Visible = Advanced;
			}

			if (change.Context.GetEditRow(nameof(EasingOption)) is GuiWidget widget3)
			{
				widget3.Visible = Advanced && EasingType != Easing.EaseType.Linear;
			}

			if (change.Context.GetEditRow(nameof(EasingType)) is GuiWidget widget4)
			{
				widget4.Visible = Advanced;
			}
		}
	}
}
/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class RevolveObject3D : Object3D, IEditorDraw
	{
		[MaxDecimalPlaces(2)]
		[Slider(0, 360, snapDistance: 1)]
		public DoubleOrExpression Rotation { get; set; } = 0;
        
		[MaxDecimalPlaces(2)]
		[Slider(-30, 30, snapDistance: 1)]
		public DoubleOrExpression AxisPosition { get; set; } = 0;

		[MaxDecimalPlaces(2)]
		[Slider(0, 360, snapDistance: 1)]
		public DoubleOrExpression StartingAngle { get; set; } = 0;

		[MaxDecimalPlaces(2)]
		[Slider(3, 360, snapDistance: 1)]
		public DoubleOrExpression EndingAngle { get; set; } = 45;

		[Slider(3, 360, Easing.EaseType.Quadratic, snapDistance: 1)]
		public IntOrExpression Sides { get; set; } = 30;

		public override bool CanApply => true;

		public override void Apply(UndoBuffer undoBuffer)
		{
			if (Mesh == null)
			{
				Cancel(undoBuffer);
			}
			else
			{
				// only keep the mesh and get rid of everything else
				using (RebuildLock())
				{
					var meshOnlyItem = new Object3D()
					{
						Mesh = this.Mesh.Copy(CancellationToken.None)
					};

					meshOnlyItem.CopyProperties(this, Object3DPropertyFlags.All);

					// and replace us with the children
					undoBuffer.AddAndDo(new ReplaceCommand(new[] { this }, new[] { meshOnlyItem }));
				}

				Invalidate(InvalidateType.Children);
			}
		}

		public RevolveObject3D()
		{
			Name = "Revolve".Localize();
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Path)
					|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children))
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateArgs.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

		(Vector3, Vector3) GetStartEnd(IObject3D pathObject, IVertexSource path)
		{
			// draw the line that is the rotation point
			var aabb = this.GetAxisAlignedBoundingBox();
			var vertexSource = path.Transform(Matrix);
			var bounds = vertexSource.GetBounds();
			var lineX = bounds.Left + AxisPosition.Value(this);

			var start = new Vector3(lineX, aabb.MinXYZ.Y, aabb.MinXYZ.Z);
			var end = new Vector3(lineX, aabb.MaxXYZ.Y, aabb.MinXYZ.Z);
			return (start, end);
		}

		public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
		{
			var path = this.CombinedVisibleChildrenPaths();
			if (path != null)
			{
				var (start, end) = GetStartEnd(this, path);
				layer.World.Render3DLine(start, end, Color.Red, true);
				layer.World.Render3DLine(start, end, Color.Red.WithAlpha(20), false);
			}
		}

		public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
		{
			return this.GetWorldspaceAabbOfDrawPath();
		}

		private CancellationTokenSource cancellationToken;

		public bool IsBuilding => this.cancellationToken != null;

		public void CancelBuild()
		{
			var threadSafe = this.cancellationToken;
			if (threadSafe != null)
			{
				threadSafe.Cancel();
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			bool valuesChanged = false;

            var rotation = MathHelper.DegreesToRadians(Rotation.ClampIfNotCalculated(this, 0, 360, ref valuesChanged));
            var startingAngle = StartingAngle.ClampIfNotCalculated(this, 0, 360 - .01, ref valuesChanged);
			var endingAngle = EndingAngle.ClampIfNotCalculated(this, startingAngle + .01, 360, ref valuesChanged);
			var sides = Sides.Value(this);
			var axisPosition = AxisPosition.Value(this);

			if (startingAngle > 0 || endingAngle < 360)
			{
				Sides = Util.Clamp(sides, 1, 360, ref valuesChanged);
			}
			else
			{
				Sides = Util.Clamp(sides, 3, 360, ref valuesChanged);
			}

			Invalidate(InvalidateType.DisplayValues);

			var rebuildLock = RebuildLock();
			// now create a long running task to process the image
			return ApplicationController.Instance.Tasks.Execute(
				"Revolve".Localize(),
				null,
				(reporter, cancellationTokenSource) =>
				{
					this.cancellationToken = cancellationTokenSource as CancellationTokenSource;
					var vertexSource = this.CombinedVisibleChildrenPaths();
                    vertexSource = vertexSource.Rotate(rotation);
                    var pathBounds = vertexSource.GetBounds();
					vertexSource = vertexSource.Translate(-pathBounds.Left - axisPosition, 0);
					Mesh mesh = VertexSourceToMesh.Revolve(vertexSource,
						sides,
						MathHelper.DegreesToRadians(360 - endingAngle),
						MathHelper.DegreesToRadians(360 - startingAngle),
						false);

					var transform = Matrix4X4.CreateTranslation(pathBounds.Left + axisPosition, 0, 0) * Matrix4X4.CreateRotationZ(-rotation);
					// take the axis offset out
					mesh.Transform(transform);

					if (mesh.Vertices.Count == 0)
					{
						mesh = null;
					}

					Mesh = mesh;

					this.cancellationToken = null;
					UiThread.RunOnIdle(() =>
					{
						rebuildLock.Dispose();
						this.CancelAllParentBuilding();
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
					});

					return Task.CompletedTask;
				});
		}
	}
}
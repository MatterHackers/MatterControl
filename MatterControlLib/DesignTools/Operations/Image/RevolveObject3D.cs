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

using System.Collections.Generic;
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
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools
{

	public class RevolveObject3D : Object3D, IEditorDraw
	{
		public double AxisPosition { get; set; } = 0;

		public int Sides { get; set; } = 30;

		public bool Advanced { get; set; } = false;

		public double StartingAngle { get; set; } = 0;

		public double EndingAngle { get; set; } = 360;

		public override bool CanFlatten => true;

		[JsonIgnore]
		private IVertexSource VertexSource
		{
			get
			{
				var item = this.Descendants().Where((d) => d is IPathObject).FirstOrDefault();
				if (item is IPathObject pathItem)
				{
					return pathItem.VertexSource;
				}

				return null;
			}
		}

		public override void Flatten(UndoBuffer undoBuffer)
		{
			if (Mesh == null)
			{
				Remove(undoBuffer);
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

		public override async void OnInvalidate(InvalidateArgs eventArgs)
		{
			if ((eventArgs.InvalidateType.HasFlag(InvalidateType.Path)
					|| eventArgs.InvalidateType.HasFlag(InvalidateType.Children))
				&& eventArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (eventArgs.InvalidateType.HasFlag(InvalidateType.Properties)
				&& eventArgs.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(eventArgs);
			}
		}

		public void DrawEditor(Object3DControlsLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e)
		{
			var child = this.Children.FirstOrDefault();
			if (child is IPathObject pathObject)
			{
				// draw the path
				child.DrawPath();

				// draw the line that is the rotation point
				var aabb = this.GetAxisAlignedBoundingBox();
				var vertexSource = this.VertexSource.Transform(Matrix);
				var bounds = vertexSource.GetBounds();
				var lineX = bounds.Left + AxisPosition;

				var start = new Vector3(lineX, aabb.MinXYZ.Y, aabb.MinXYZ.Z);
				var end = new Vector3(lineX, aabb.MaxXYZ.Y, aabb.MinXYZ.Z);

				layer.World.Render3DLine(start, end, Color.Red, true);
				layer.World.Render3DLine(start, end, Color.Red.WithAlpha(20), false);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");
			bool valuesChanged = false;

			if (Advanced && (StartingAngle > 0 || EndingAngle < 360))
			{
				Sides = agg_basics.Clamp(Sides, 1, 360, ref valuesChanged);
			}
			else
			{
				Sides = agg_basics.Clamp(Sides, 3, 360, ref valuesChanged);
			}

			StartingAngle = agg_basics.Clamp(StartingAngle, 0, 360 - .01, ref valuesChanged);
			EndingAngle = agg_basics.Clamp(EndingAngle, StartingAngle + .01, 360, ref valuesChanged);

			if (valuesChanged)
			{
				Invalidate(InvalidateType.DisplayValues);
			}

			var rebuildLock = RebuildLock();
			// now create a long running task to process the image
			return ApplicationController.Instance.Tasks.Execute(
				"Revolve".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					var vertexSource = this.VertexSource.Transform(Matrix);
					var bounds = vertexSource.GetBounds();
					vertexSource = vertexSource.Translate(-bounds.Left - AxisPosition, 0);
					Mesh mesh = null;
					if (!Advanced)
					{
						mesh = VertexSourceToMesh.Revolve(vertexSource, Sides);
					}
					else
					{
						mesh = VertexSourceToMesh.Revolve(vertexSource,
							Sides,
							MathHelper.DegreesToRadians(StartingAngle),
							MathHelper.DegreesToRadians(EndingAngle));
					}

					mesh.Transform(Matrix4X4.CreateTranslation(bounds.Left + AxisPosition, 0, -mesh.GetAxisAlignedBoundingBox().MinXYZ.Z));

					if (mesh.Vertices.Count == 0)
					{
						mesh = null;
					}

					Mesh = mesh;

					rebuildLock.Dispose();
					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
					return Task.CompletedTask;
				});
		}
	}
}
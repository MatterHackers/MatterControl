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
using System.Linq;
using System.Threading.Tasks;
using ClipperLib;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh.Processors;
using Polygon = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class SmoothPathObject3D : Object3D, IPathObject, IEditorDraw, IObject3DControlsProvider
	{
		public IVertexSource VertexSource { get; set; } = new VertexStorage();

		public SmoothPathObject3D()
		{
			Name = "Smooth Path".Localize();
		}

		[Description("Max distance to move bumps to make lines")]
		[MaxDecimalPlaces(2)]
		[Slider(.01, 1, VectorMath.Easing.EaseType.Quadratic, snapDistance: .01)]
		public DoubleOrExpression SmoothDistance { get; set; } = .3;

		[Description("The number of smoothing passes")]
		public IntOrExpression Iterations { get; set; } = 3;

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddControls(ControlTypes.Standard2D);
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Matrix)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Path))
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				await Rebuild();
			}
			else if (SheetObject3D.NeedsRebuild(this, invalidateArgs))
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLock = RebuildLock();
			return ApplicationController.Instance.Tasks.Execute(
				"Smooth Path".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					DoSmoothing((long)(SmoothDistance.Value(this) * 1000), Iterations.Value(this));

					// set the mesh to show the path
					this.Mesh = this.VertexSource.Extrude(Constants.PathPolygonsHeight);

					UiThread.RunOnIdle(() =>
					{
						rebuildLock.Dispose();
						this.Invalidate(InvalidateType.DisplayValues);
						this.CancelAllParentBuilding();
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
					});
					return Task.CompletedTask;
				});
		}

		private void DoSmoothing(long maxDist, int interations)
		{
			bool closedPath = true;
			var path = this.Children.OfType<IPathObject>().FirstOrDefault();
			if (path == null)
			{
				// clear our existing data
				VertexSource = new VertexStorage();
				return;
			}

			var sourceVertices = path.VertexSource;

			var inputPolygons = sourceVertices.CreatePolygons();

			Polygons outputPolygons = new Polygons();
			foreach (Polygon inputPolygon in inputPolygons)
			{
				int numVerts = inputPolygon.Count;
				long maxDistSquared = maxDist * maxDist;

				var smoothedPositions = new Polygon(numVerts);
				foreach (IntPoint inputPosition in inputPolygon)
				{
					smoothedPositions.Add(inputPosition);
				}

				for (int iteration = 0; iteration < interations; iteration++)
				{
					var positionsThisPass = new Polygon(numVerts);
					foreach (IntPoint inputPosition in smoothedPositions)
					{
						positionsThisPass.Add(inputPosition);
					}

					int startIndex = closedPath ? 0 : 1;
					int endIndex = closedPath ? numVerts : numVerts - 1;

					for (int i = startIndex; i < endIndex; i++)
					{
						// wrap back to the previous index
						IntPoint prev = positionsThisPass[(i + numVerts - 1) % numVerts];
						IntPoint cur = positionsThisPass[i];
						IntPoint next = positionsThisPass[(i + 1) % numVerts];

						IntPoint newPos = (prev + cur + next) / 3;
						IntPoint delta = newPos - inputPolygon[i];
						if (delta.LengthSquared() > maxDistSquared)
						{
							delta = delta.GetLength(maxDist);
							newPos = inputPolygon[i] + delta;
						}

						smoothedPositions[i] = newPos;
					}
				}

				outputPolygons.Add(smoothedPositions);

				outputPolygons = ClipperLib.Clipper.CleanPolygons(outputPolygons, Math.Max(maxDist / 10, 1.415));
			}

			VertexSource = outputPolygons.CreateVertexStorage();
		}

		public void DrawEditor(Object3DControlsLayer layer, DrawEventArgs e)
		{
			this.DrawPath();
		}
	}
}
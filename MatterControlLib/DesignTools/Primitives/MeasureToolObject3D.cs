/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	[MarkDownDescription("Drag the spheres to the locations you would like to measure the distance between.")]
	[HideMeterialAndColor]
	public class MeasureToolObject3D : Object3D, IObject3DControlsProvider, IAlwaysEditorDraw, IEditorButtonProvider
	{
		private static Mesh shape = null;
		private List<IObject3DControl> editorControls = null;

		public MeasureToolObject3D()
		{
			Name = "Measure Tool".Localize();
			Color = Color.FromHSL(.11, .98, .76);

			if (shape == null)
			{
				using (Stream measureAmfStream = StaticData.Instance.OpenStream(Path.Combine("Stls", "measure_tool.stl")))
				{
					shape = StlProcessing.Load(measureAmfStream, CancellationToken.None);
				}
			}

			Mesh = shape;
		}

		public static async Task<MeasureToolObject3D> Create()
		{
			var item = new MeasureToolObject3D();
			await item.Rebuild();
			return item;
		}

		[HideFromEditor]
		public Vector3 StartPosition { get; set; } = new Vector3(-10, 5, 3);

		[HideFromEditor]
		public Vector3 EndPosition { get; set; } = new Vector3(10, 5, 3);

		[ReadOnly(true)]
		public double Distance { get; set; } = 0;

		public bool AlwaysVisible { get; set; } = false;

		[HideFromEditor]
		public bool PositionsHaveBeenSet { get; set; } = false;

		public override bool Persistable => false;

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			if (editorControls == null)
			{
				editorControls = new List<IObject3DControl>
				{
					new TracedPositionObject3DControl(object3DControlsLayer,
					this,
					() =>
					{
						return PositionsHaveBeenSet ? StartPosition : StartPosition.Transform(Matrix);
					},
					(position) =>
					{
						if (!PositionsHaveBeenSet)
						{
							PositionsHaveBeenSet = true;
							EndPosition = EndPosition.Transform(this.Matrix);
						}

						StartPosition = position;
						Distance = (StartPosition - EndPosition).Length;
						UiThread.RunOnIdle(() => Invalidate(InvalidateType.DisplayValues));
					}),
					new TracedPositionObject3DControl(object3DControlsLayer,
					this,
					() =>
					{
						return PositionsHaveBeenSet ? EndPosition : EndPosition.Transform(Matrix);
					},
					(position) =>
					{
						if (!PositionsHaveBeenSet)
						{
							PositionsHaveBeenSet = true;
							StartPosition = StartPosition.Transform(this.Matrix);
						}

						EndPosition = position;
						Distance = (StartPosition - EndPosition).Length;
						UiThread.RunOnIdle(() => Invalidate(InvalidateType.DisplayValues));
					}),
				};
			}

			object3DControlsLayer.Object3DControls.Modify((list) =>
			{
				list.AddRange(editorControls);
			});
		}

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
			return Task.CompletedTask;
		}

		public void DrawEditor(Object3DControlsLayer controlLayer, List<Object3DView> transparentMeshes, DrawEventArgs e)
		{
			if (controlLayer.Scene.SelectedItem != this
				&& !AlwaysVisible)
			{
				return;
			}

			var start = PositionsHaveBeenSet ? StartPosition : StartPosition.Transform(Matrix);
			var end = PositionsHaveBeenSet ? EndPosition : EndPosition.Transform(Matrix);

			var world = controlLayer.World;
			// draw on top of anything that is already drawn
			world.Render3DLine(start,
				end,
				Color.Black.WithAlpha(Constants.LineAlpha),
				false,
				GuiWidget.DeviceScale,
				true,
				true);

			// Restore DepthTest
			world.Render3DLine(start, end, Color.Black, true, width: GuiWidget.DeviceScale);

			var screenStart = world.GetScreenPosition(start);
			var screenEnd = world.GetScreenPosition(end);

			var center = (screenStart + screenEnd) / 2;

			if (PositionsHaveBeenSet)
			{
				controlLayer.DrawBeforeGui((graphics) =>
				{
					var number = new TypeFacePrinter(Distance.ToString("0.##"),
						10,
						center,
						Justification.Center,
						Baseline.BoundsCenter);

					var theme = ApplicationController.Instance.MenuTheme;

					var bounds = number.LocalBounds;
					bounds.Inflate(3 * GuiWidget.DeviceScale);

					graphics.Render(new RoundedRectShape(bounds, 3 * GuiWidget.DeviceScale), theme.BackgroundColor);

					graphics.Render(number, theme.TextColor);
				});
			}
		}

		public IEnumerable<EditorButtonData> GetEditorButtonsData()
		{
			yield return new EditorButtonData()
			{
				Action = () =>
				{
					StartPosition = new Vector3(-10, 5, 3);
					EndPosition = new Vector3(10, 5, 3);
					Distance = 0;
					PositionsHaveBeenSet = false;
					UiThread.RunOnIdle(() => Invalidate(InvalidateType.DisplayValues));
				},
				HelpText = "Reset the line ends back to their starting positions".Localize(),
				Name = "Reset".Localize(),
			};
		}
	}
}
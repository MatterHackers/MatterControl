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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
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
		private GuiWidget containerWidget;
		private GuiWidget textWidget;
		private Object3DControlsLayer controlLayer;

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
		private Vector3 worldStartPosition
		{
			get
			{
				return LocalStartPosition.Transform(this.WorldMatrix());
			}

			set
			{
				LocalStartPosition = value.Transform(this.WorldMatrix().Inverted);
			}
		}

		[HideFromEditor]
		private Vector3 worldEndPosition
		{
			get
			{
				return LocalEndPosition.Transform(this.WorldMatrix());
			}

			set
			{
				LocalEndPosition = value.Transform(this.WorldMatrix().Inverted);
			}
		}


		[HideFromEditor]
		public Vector3 LocalStartPosition { get; set; }

		[HideFromEditor]
		public Vector3 LocalEndPosition { get; set; }


		[ReadOnly(true)]
		public double Distance { get; set; } = 0;

		[HideFromEditor]
		public bool PositionsHaveBeenSet { get; set; } = false;

		public override bool Persistable => false;

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			if (editorControls == null)
			{
				editorControls = new List<IObject3DControl>
				{
					// Start Position Object
					new TracedPositionObject3DControl(object3DControlsLayer,
					this,
					// get position function
					() => worldStartPosition,
					// set position function
					(position) =>
						{
							if (!PositionsHaveBeenSet)
							{
								PositionsHaveBeenSet = true;
							}

						worldStartPosition = position;
							Distance = (worldStartPosition - worldEndPosition).Length;
							UiThread.RunOnIdle(() => Invalidate(InvalidateType.DisplayValues));
						},
					// edit complete function
					(undoPosition) => SetUndoData(undoPosition, worldEndPosition)
					),
					// End Position Object
					new TracedPositionObject3DControl(object3DControlsLayer,
					this,
					// get position function
					() => worldEndPosition,
					// set position function
					(position) =>
						{
							if (!PositionsHaveBeenSet)
							{
								PositionsHaveBeenSet = true;
							}

						worldEndPosition = position;
							Distance = (worldStartPosition - worldEndPosition).Length;
							UiThread.RunOnIdle(() => Invalidate(InvalidateType.DisplayValues));
						},
					// edit complete function
					(undoPosition) => SetUndoData(worldStartPosition, undoPosition)
					),
				};
			}

			object3DControlsLayer.Object3DControls.Modify((list) =>
			{
				list.AddRange(editorControls);
			});
		}

		private void SetUndoData(Vector3 undoStartPosition, Vector3 undoEndPosition)
		{
			var doStartPosition = worldStartPosition;
			var doEndPosition = worldEndPosition;

			controlLayer.Scene.UndoBuffer.Add(new UndoRedoActions(() =>
			{
				worldStartPosition = undoStartPosition;
				worldEndPosition = undoEndPosition;
				this.Invalidate(InvalidateType.Matrix);
			},
			() =>
			{
				worldStartPosition = doStartPosition;
				worldEndPosition = doEndPosition;
				this.Invalidate(InvalidateType.Matrix);
			}));
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
			if (!PositionsHaveBeenSet)
			{
				var aabb = this.Mesh.GetAxisAlignedBoundingBox();
				LocalStartPosition = aabb.Center + new Vector3(-10, 5, 3);
				LocalEndPosition = aabb.Center + new Vector3(10, 5, 3);
			}

			var start = worldStartPosition;
			var end = worldEndPosition;

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
			world.Render3DLine(start,
				end,
				Color.Black.WithAlpha(Constants.LineAlpha),
				true,
				GuiWidget.DeviceScale,
				true,
				true);

			var screenStart = world.GetScreenPosition(start);
			var screenEnd = world.GetScreenPosition(end);

			var center = (screenStart + screenEnd) / 2;

			if (PositionsHaveBeenSet)
			{
				CreateWidgetIfRequired(controlLayer);
				// always keep the displayed distance the actual world distance
				var worldStartPosition = LocalStartPosition.Transform(this.WorldMatrix());
				var worldEndPosition = LocalEndPosition.Transform(this.WorldMatrix());
				Distance = (worldStartPosition - worldEndPosition).Length;
				textWidget.Text = Distance.ToString("0.##");
				containerWidget.Position = center - new Vector2(containerWidget.LocalBounds.Width / 2, containerWidget.LocalBounds.Height / 2);
				containerWidget.Visible = true;
			}
		}

		private void CreateWidgetIfRequired(Object3DControlsLayer controlLayer)
		{
			if (containerWidget == null
				|| containerWidget.Parents<SystemWindow>().Count() == 0)
			{
				this.controlLayer = controlLayer;
				var theme = ApplicationController.Instance.MenuTheme;
				containerWidget = new GuiWidget()
				{
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit,
					Padding = 5,
					BackgroundColor = theme.BackgroundColor,
					BackgroundRadius = new RadiusCorners(3 * GuiWidget.DeviceScale),
					BorderColor = theme.PrimaryAccentColor,
					BackgroundOutlineWidth = 1,
				};

				containerWidget.AddChild(textWidget = new TextWidget(Distance.ToString("0.##"))
				{
					TextColor = theme.TextColor,
					PointSize = 10,
					Selectable = true,
					AutoExpandBoundsToText = true,
				});

				controlLayer.GuiSurface.AddChild(containerWidget);

				controlLayer.GuiSurface.AfterDraw += GuiSurface_AfterDraw;

				void NumberWidget_MouseDown(object sender, MouseEventArgs e2)
				{
					controlLayer.Scene.SelectedItem = this;
				}

				containerWidget.MouseDown += NumberWidget_MouseDown;
			}
		}

		private void GuiSurface_AfterDraw(object sender, DrawEventArgs e)
		{
			if (!controlLayer.Scene.Contains(this))
			{
				containerWidget.Close();
				if (sender is GuiWidget guiWidget)
				{
					guiWidget.AfterDraw -= GuiSurface_AfterDraw;
				}
			}
		}

		public IEnumerable<EditorButtonData> GetEditorButtonsData()
		{
			yield return new EditorButtonData()
			{
				Action = () =>
				{
					Distance = 0;
					if (containerWidget != null)
					{
						containerWidget.Visible = false;
					}
					PositionsHaveBeenSet = false;
					UiThread.RunOnIdle(() => Invalidate(InvalidateType.DisplayValues));
				},
				HelpText = "Reset the line ends back to their starting positions".Localize(),
				Name = "Reset".Localize(),
			};
		}
	}
}
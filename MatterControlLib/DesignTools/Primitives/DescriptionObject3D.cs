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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Markdig.Agg;
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
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	[MarkDownDescription("Drag the sphere to the locations you would like to position the description.")]
	[HideMeterialAndColor]
	public class DescriptionObject3D : Object3D, IObject3DControlsProvider, IAlwaysEditorDraw, IEditorButtonProvider
	{
		private static Mesh shape = null;
		private List<IObject3DControl> editorControls = null;
		private MarkdownWidget markdownWidget;

		public DescriptionObject3D()
		{
			Name = "Description".Localize();
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

		public static async Task<DescriptionObject3D> Create()
		{
			var item = new DescriptionObject3D();
			await item.Rebuild();
			return item;
		}

		[HideFromEditor]
		public Vector3 StartPosition { get; set; } = new Vector3(-10, 5, 3);

		[HideFromEditor]
		public bool PositionHasBeenSet { get; set; } = false;

		[MarkdownString]
		public string Description { get; set; } = "Type a description in the properties panel";

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
						return PositionHasBeenSet ? StartPosition : StartPosition.Transform(Matrix);
					},
					(position) =>
					{
						if (!PositionHasBeenSet)
						{
							PositionHasBeenSet = true;
						}

						StartPosition = position;
						UiThread.RunOnIdle(() => Invalidate(InvalidateType.DisplayValues));
					}),
				};
			}

			object3DControlsLayer.Object3DControls.Modify((list) =>
			{
				list.AddRange(editorControls);
			});
		}

		public override void Remove(UndoBuffer undoBuffer)
		{
			if (markdownWidget != null)
			{
				markdownWidget.Close();
			}

			base.Remove(undoBuffer);
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
			var start = PositionHasBeenSet ? StartPosition : StartPosition.Transform(Matrix);

			var world = controlLayer.World;

			var screenStart = world.GetScreenPosition(start);

			if (PositionHasBeenSet)
			{
				if (markdownWidget == null)
				{
					var theme = ApplicationController.Instance.MenuTheme;
					markdownWidget = new MarkdownWidget(theme, true)
					{
						HAnchor = HAnchor.Absolute,
						VAnchor = VAnchor.Fit,
						Width = 200,
						Height = 100,
						BackgroundColor = theme.BackgroundColor,
						BackgroundRadius = new RadiusCorners(3 * GuiWidget.DeviceScale),
						Margin = 0,
						BorderColor = theme.PrimaryAccentColor,
						BackgroundOutlineWidth = 1,
						Padding = 5,
					};

					markdownWidget.Markdown = Description;
					markdownWidget.Width = 100 * GuiWidget.DeviceScale;

					controlLayer.GuiSurface.AddChild(markdownWidget);

					markdownWidget.AfterDraw += MarkdownWidget_AfterDraw;

					void MarkdownWidget_MouseDown(object sender, MouseEventArgs e2)
					{
						controlLayer.Scene.SelectedItem = this;
					}

					markdownWidget.MouseDown += MarkdownWidget_MouseDown;
				}

				var descrpition = Description.Replace("\\n", "\n");
				if (markdownWidget.Markdown != descrpition)
				{
					markdownWidget.Markdown = descrpition;
					markdownWidget.Width = 100 * GuiWidget.DeviceScale;
				}

				markdownWidget.Position = screenStart;
			}
		}

		private void MarkdownWidget_AfterDraw(object sender, DrawEventArgs e)
		{
			if (!this.Parent.Children.Where(c => c == this).Any())
			{
				markdownWidget.Close();
				markdownWidget.AfterDraw -= MarkdownWidget_AfterDraw;
			}
		}

		public IEnumerable<EditorButtonData> GetEditorButtonsData()
		{
			yield return new EditorButtonData()
			{
				Action = () =>
				{
					StartPosition = new Vector3(-10, 5, 3);
					PositionHasBeenSet = false;
					UiThread.RunOnIdle(() => Invalidate(InvalidateType.DisplayValues));
				},
				HelpText = "Reset the position".Localize(),
				Name = "Reset".Localize(),
			};
		}
	}
}
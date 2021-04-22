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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
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
	[HideChildrenFromTreeView]
	[MarkDownDescription("Drag the sphere to the location you would like to position the description.")]
	[HideMeterialAndColor]
	public class DescriptionObject3D : Object3D, IObject3DControlsProvider, IAlwaysEditorDraw, IEditorButtonProvider
	{
		private MarkdownWidget markdownWidget;
		private Object3DControlsLayer controlLayer;

		public DescriptionObject3D()
		{
			Name = "Description".Localize();

			using (Stream measureAmfStream = StaticData.Instance.OpenStream(Path.Combine("Stls", "description_tool.amf")))
			{
				Children.Add(AmfDocument.Load(measureAmfStream, CancellationToken.None));
			}
		}

		public static async Task<DescriptionObject3D> Create()
		{
			var item = new DescriptionObject3D();
			await item.Rebuild();
			return item;
		}

		[HideFromEditor]
		public Vector3 Position { get; set; } = new Vector3(-10, 5, 3);

		[HideFromEditor]
		public bool PositionHasBeenSet { get; set; } = false;

		[MarkdownString]
		public string Description { get; set; } = "Type a description in the properties panel";

		public enum Placements
		{
			Left_Top,
			Left_Bottom,
			Right_Top,
			Right_Bottom
		}

		[EnumDisplay(IconPaths = new string[] { "left_top.png", "left_bottom.png", "right_top.png", "right_bottom.png", }, InvertIcons = true)]
		public Placements Placement { get; set; }

		public enum Widths
		{
			Narrow,
			Normal,
			Wide,
		}

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public Widths Width { get; set; } = Widths.Normal;

		public override bool Persistable => false;

		private TracedPositionObject3DControl tracedPositionControl;
		private bool mouseDownOnWidget;
		private Vector3 mouseDownPosition;
		private Vector2 widgetDownPosition;

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			if (tracedPositionControl == null)
			{
				tracedPositionControl = new TracedPositionObject3DControl(object3DControlsLayer,
					this,
					() =>
					{
						return PositionHasBeenSet ? Position : Position.Transform(Matrix);
					},
					(position) =>
					{
						if (!PositionHasBeenSet)
						{
							PositionHasBeenSet = true;
						}

						Position = position;
						UiThread.RunOnIdle(() => Invalidate(InvalidateType.DisplayValues));
					});
			}

			object3DControlsLayer.Object3DControls.Modify((list) =>
			{
				list.Add(tracedPositionControl);
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

		private double width
		{
			get
			{
				switch (Width)
				{
					case Widths.Narrow:
						return 100 * GuiWidget.DeviceScale;
					case Widths.Normal:
						return 200 * GuiWidget.DeviceScale;
					case Widths.Wide:
						return 300 * GuiWidget.DeviceScale;
				}

				return 200 * GuiWidget.DeviceScale;
			}
		}

		public void DrawEditor(Object3DControlsLayer controlLayer, List<Object3DView> transparentMeshes, DrawEventArgs e)
		{
			var start = PositionHasBeenSet ? Position : Position.Transform(Matrix);

			var world = controlLayer.World;

			var screenStart = world.GetScreenPosition(start);

			CreateWidgetIfRequired(controlLayer);
			markdownWidget.Visible = true;

			var descrpition = Description.Replace("\\n", "\n");
			if (markdownWidget.Markdown != descrpition)
			{
				markdownWidget.Markdown = descrpition;
			}

			markdownWidget.Width = width;

			var pos = screenStart;
			switch (Placement)
			{
				case Placements.Left_Top:
					pos.X -= markdownWidget.Width;
					break;
				case Placements.Left_Bottom:
					pos.X -= markdownWidget.Width;
					pos.Y -= markdownWidget.Height;
					break;
				case Placements.Right_Top:
					break;
				case Placements.Right_Bottom:
					pos.Y -= markdownWidget.Height;
					break;
			}

			markdownWidget.Position = pos;

			var graphics2DOpenGL = new Graphics2DOpenGL(GuiWidget.DeviceScale);
			var distBetweenPixelsWorldSpace = controlLayer.World.GetWorldUnitsPerScreenPixelAtPosition(start);
			var transform = Matrix4X4.CreateScale(distBetweenPixelsWorldSpace) * world.RotationMatrix.Inverted * Matrix4X4.CreateTranslation(start);
			var theme = ApplicationController.Instance.MenuTheme;
			graphics2DOpenGL.RenderTransformedPath(transform, new Ellipse(0, 0, 5, 5), theme.PrimaryAccentColor, false);
		}

		private void CreateWidgetIfRequired(Object3DControlsLayer controlLayer)
		{
			if (markdownWidget == null
				|| markdownWidget.Parents<SystemWindow>().Count() == 0)
			{
				this.controlLayer = controlLayer;
				var theme = ApplicationController.Instance.MenuTheme;
				markdownWidget = new MarkdownWidget(theme, false)
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Fit,
					Width = width,
					Height = 100,
					BackgroundColor = theme.BackgroundColor,
					BackgroundRadius = new RadiusCorners(3 * GuiWidget.DeviceScale),
					Margin = 0,
					BorderColor = theme.PrimaryAccentColor,
					BackgroundOutlineWidth = 1,
					Padding = 5,
					Selectable = true
				};

				markdownWidget.Markdown = Description;
				markdownWidget.Width = width;
				markdownWidget.ScrollArea.VAnchor = VAnchor.Fit | VAnchor.Center;
				foreach (var child in markdownWidget.Children)
				{
					child.Selectable = false;
				}

				controlLayer.GuiSurface.AddChild(markdownWidget);
				controlLayer.GuiSurface.AfterDraw += GuiSurface_AfterDraw;
				markdownWidget.MouseDown += MarkdownWidget_MouseDown;
				markdownWidget.MouseMove += MarkdownWidget_MouseMove;
				markdownWidget.MouseUp += MarkdownWidget_MouseUp;
			}
		}

		void MarkdownWidget_MouseDown(object sender, MouseEventArgs e)
		{
			controlLayer.Scene.SelectedItem = this;

			if (tracedPositionControl != null && !tracedPositionControl.DownOnControl)
			{
				mouseDownPosition = Position;
				widgetDownPosition = e.Position;
				mouseDownOnWidget = true;
			}
		}

		void MarkdownWidget_MouseMove(object sender, MouseEventArgs e)
		{
			if (mouseDownOnWidget)
			{
				var screenStart = controlLayer.World.GetScreenPosition(mouseDownPosition);
				var delta = e.Position - widgetDownPosition;
				tracedPositionControl.MoveToScreenPosition(screenStart + delta);
				widgetDownPosition = e.Position;
			}
		}

		void MarkdownWidget_MouseUp(object sender, MouseEventArgs e)
		{
			if (mouseDownOnWidget)
			{
				mouseDownOnWidget = false;
			}
		}

		private void GuiSurface_AfterDraw(object sender, DrawEventArgs e)
		{
			if (!this.Parent.Children.Where(c => c == this).Any())
			{
				markdownWidget.Close();
				if (sender is GuiWidget guiWidget)
				{
					guiWidget.AfterDraw -= GuiSurface_AfterDraw;
					markdownWidget.MouseDown -= MarkdownWidget_MouseDown;
					markdownWidget.MouseMove -= MarkdownWidget_MouseMove;
					markdownWidget.MouseUp -= MarkdownWidget_MouseUp;
				}
			}
		}

		public IEnumerable<EditorButtonData> GetEditorButtonsData()
		{
			yield return new EditorButtonData()
			{
				Action = () =>
				{
					Position = new Vector3(-10, 5, 3);
					PositionHasBeenSet = false;
					if (markdownWidget != null)
					{
						markdownWidget.Visible = false;
					}
					UiThread.RunOnIdle(() => Invalidate(InvalidateType.DisplayValues));
				},
				HelpText = "Reset the position".Localize(),
				Name = "Reset".Localize(),
			};
		}
	}
}
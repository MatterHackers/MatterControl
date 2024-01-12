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
using Markdig.Agg;
using Markdig.Renderers.Agg.Inlines;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	[HideChildrenFromTreeView]
	[HideMeterialAndColor]
	[WebPageLink("Resources", "Markdown Help", "https://guides.github.com/features/mastering-markdown/")]
	[MarkDownDescription("Used to add description within then scene. The object on the bed will not print.")]
	public class DescriptionObject3D : Object3D, IObject3DControlsProvider, ICustomEditorDraw, IEditorButtonProvider
	{
		private MarkdownWidget markdownWidget;
		private Object3DControlsLayer controlLayer;

		public DescriptionObject3D()
		{
			Name = "Description".Localize();
		}

		public override Mesh Mesh
		{
			get
			{
				if (!this.Children.Where(i => i.VisibleMeshes().Count() > 0).Any())
				{
					// add the amf content
					using (Stream measureAmfStream = StaticData.Instance.OpenStream(Path.Combine("Stls", "description_tool.amf")))
					{
						Children.Modify((list) =>
						{
							list.Clear();
							list.Add(AmfDocument.Load(measureAmfStream, CancellationToken.None));
						});
					}
				}

				return base.Mesh;
			}

			set => base.Mesh = value;
		}

		public static async Task<DescriptionObject3D> Create()
		{
			var item = new DescriptionObject3D();
			await item.Rebuild();
			return item;
		}

		private Vector3 worldPosition
		{
			get
			{
				return LocalPosition.Transform(this.WorldMatrix());
			}

			set
			{
				LocalPosition = value.Transform(this.WorldMatrix().Inverted);
			}
		}

		public bool DoEditorDraw(bool isSelected) => true;


		[HideFromEditor]
		public Vector3 LocalPosition { get; set; }

		[HideFromEditor]
		public bool PositionHasBeenSet { get; set; } = false;

		[DisplayName("Description - Markdown Text")]
		[MultiLineEdit]
		[UpdateOnEveryKeystroke]
		public string Description { get; set; } = "You can edit this description in the properties panel";

		public enum Placements
		{
			Left_Top,
			Left_Bottom,
			Right_Top,
			Right_Bottom
		}

		[EnumDisplay(IconPaths = new string[] { "left_top.png", "left_bottom.png", "right_top.png", "right_bottom.png", }, InvertIcons = true)]
		public Placements Placement { get; set; } = Placements.Left_Top;

		public enum Widths
		{
			Narrow,
			Normal,
			Wide,
		}

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public Widths Width { get; set; } = Widths.Normal;

		public override bool Printable => false;

		private TracedPositionObject3DControl tracedPositionControl;
		private bool mouseDownOnWidget;
		private Vector3 mouseDownPosition;
		private Vector2 widgetDownPosition;

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			EnsureTracedPositionControl(object3DControlsLayer);

			object3DControlsLayer.Object3DControls.Modify((list) =>
			{
				list.Add(tracedPositionControl);
			});
		}

		private void EnsureTracedPositionControl(Object3DControlsLayer object3DControlsLayer)
		{
			if (tracedPositionControl == null)
			{
				tracedPositionControl = new TracedPositionObject3DControl(object3DControlsLayer,
					this,
					// get position function
					() => worldPosition,
					// set position function
					(position) =>
					{
						if (!PositionHasBeenSet)
						{
							PositionHasBeenSet = true;
						}

						if (worldPosition != position)
						{
							worldPosition = position;
							UiThread.RunOnIdle(() => Invalidate(InvalidateType.DisplayValues));
						}
					},
					// edit complete function
					(undoPosition) => SetUndoData(undoPosition)
					);
			}
		}

		public override void Cancel(UndoBuffer undoBuffer)
		{
			if (markdownWidget != null)
			{
				markdownWidget.Close();
			}

			base.Cancel(undoBuffer);
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				await Rebuild();
			}
			else if (Expressions.NeedRebuild(this, invalidateArgs))
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

			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
				}
			}

			this.CancelAllParentBuilding();
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

		public void AddEditorTransparents(Object3DControlsLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e) { }

		public void DrawEditor(Object3DControlsLayer controlLayer, DrawEventArgs e)
		{
			EnsureTracedPositionControl(controlLayer);

			var world = controlLayer.World;

			if (!PositionHasBeenSet)
			{
				var aabb = Children.FirstOrDefault().GetAxisAlignedBoundingBox();
				LocalPosition = new Vector3(aabb.MinXYZ.X, aabb.MaxXYZ.Y, aabb.MaxXYZ.Z);
			}

			var start = worldPosition;

			var screenStart = world.GetScreenPosition(start);

			CreateWidgetIfRequired(controlLayer);
			markdownWidget.Visible = true;

			var description = Description.Replace("\\n", "\n");
			if (markdownWidget.Markdown != description)
			{
				markdownWidget.Markdown = description;

				FixSelectableBasedOnLinks(markdownWidget);
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

        private void FixSelectableBasedOnLinks(MarkdownWidget markdownWidget)
        {
			if (markdownWidget.Descendants<TextLinkX>().Any())
			{
				foreach (var child in markdownWidget.Children)
				{
					child.Selectable = true;
				}
			}
			else
            {
				foreach (var child in markdownWidget.Children)
				{
					child.Selectable = false;
				}
			}
		}

		public AxisAlignedBoundingBox GetEditorWorldspaceAABB(Object3DControlsLayer layer)
		{
			return AxisAlignedBoundingBox.Empty();
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

				FixSelectableBasedOnLinks(markdownWidget);

                controlLayer.GuiSurface.AddChild(markdownWidget);
				controlLayer.GuiSurface.AfterDraw += GuiSurface_AfterDraw;
				markdownWidget.MouseDown += MarkdownWidget_MouseDown;
				markdownWidget.MouseMove += MarkdownWidget_MouseMove;
				markdownWidget.MouseUp += MarkdownWidget_MouseUp;
				markdownWidget.KeyDown += MarkdownWidget_KeyDown;
			}
		}

		private void MarkdownWidget_KeyDown(object sender, KeyEventArgs e)
		{
			if (mouseDownOnWidget
				&& e.KeyCode == Keys.Escape)
			{
				mouseDownOnWidget = false;
				worldPosition = mouseDownPosition;
			}
		}

		void MarkdownWidget_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				controlLayer.Scene.SelectedItem = this;

				if (tracedPositionControl != null && !tracedPositionControl.DownOnControl)
				{
					tracedPositionControl.ResetHitPlane();
					mouseDownPosition = worldPosition;
					var widget = (GuiWidget)sender;
					widgetDownPosition = widget.TransformToScreenSpace(e.Position);
					mouseDownOnWidget = true;
				}
			}
		}

		void MarkdownWidget_MouseMove(object sender, MouseEventArgs e)
		{
			if (mouseDownOnWidget)
			{
				var screenStart = controlLayer.World.GetScreenPosition(mouseDownPosition);
				var widget = (GuiWidget)sender;
				var ePosition = widget.TransformToScreenSpace(e.Position);
				var delta = ePosition - widgetDownPosition;
				if (delta.LengthSquared > 0)
				{
					tracedPositionControl.MoveToScreenPosition(screenStart + delta);
				}
			}
		}

		void MarkdownWidget_MouseUp(object sender, MouseEventArgs e)
		{
			if (mouseDownOnWidget)
			{
				mouseDownOnWidget = false;

				SetUndoData(mouseDownPosition);
			}
		}

		private void SetUndoData(Vector3 undoPosition)
		{
			var doPosition = worldPosition;

			controlLayer.Scene.UndoBuffer.Add(new UndoRedoActions(() =>
			{
				worldPosition = undoPosition;
				this.Invalidate(InvalidateType.Matrix);
			},
			() =>
			{
				worldPosition = doPosition;
				this.Invalidate(InvalidateType.Matrix);
			}));
		}

		private void GuiSurface_AfterDraw(object sender, DrawEventArgs e)
		{
			if (!controlLayer.Scene.Contains(this))
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
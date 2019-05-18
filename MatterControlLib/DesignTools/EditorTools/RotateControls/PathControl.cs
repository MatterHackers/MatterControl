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

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MeshVisualizer;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.Plugins.EditorTools
{
	public class PathControl : IGLInteractionElement
	{
		private IInteractionVolumeContext interactionContext;

		private ThemeConfig theme;

		private WorldView world;

		private IObject3D lastItem;

		private List<PointWidget> targets = new List<PointWidget>();

		private bool controlsRegistered = false;
		private IEnumerable<VertexData> activePoints;
		private bool m_visible;

		public PathControl(IInteractionVolumeContext context)
		{
			interactionContext = context;
			theme = MatterControl.AppContext.Theme;

			world = context.World;
		}

		public string Name => "Path Control";

		public bool Visible
		{
			get => m_visible;
			set
			{
				if (m_visible != value)
				{
					m_visible = value;

					foreach(var widget in targets)
					{
						widget.Visible = m_visible;
					}
				}
			}
		}

		public bool DrawOnTop => false;

		public void CancelOperation()
		{
		}

		public void DrawGlContent(DrawGlContentEventArgs e)
		{
			//// Draw grid background with active BedColor
			//GL.Disable(EnableCap.Texture2D);
			//GL.Disable(EnableCap.Blend);

			GL.Begin(BeginMode.Lines);
			{
				GL.Color4(theme.PrimaryAccentColor);

				bool isStart = true;

				Vector3 last = Vector3.Zero;
				Vector3 first = Vector3.Zero;

				foreach (var point in activePoints)
				{
					if (isStart
						|| point.IsMoveTo)
					{
						last = new Vector3(point.position);
						first = last;
						isStart = false;
					}
					else if (point.IsVertex)
					{
						GL.Vertex3(last);
						GL.Vertex3(last = new Vector3(point.position));
					}
					else if (point.IsClose)
					{
						GL.Vertex3(last);
						GL.Vertex3(first);
					}
				}
			}
			GL.End();
		}

		public void LostFocus()
		{
			this.Reset();
		}

		private void Reset()
		{
			// Clear and close selection targets
			foreach (var widget in targets)
			{
				widget.Close();
			}

			targets.Clear();

			lastItem = null;
		}

		public void SetPosition(IObject3D selectedItem)
		{
			if (selectedItem != lastItem)
			{
				this.Reset();

				lastItem = selectedItem;

				if (selectedItem is PathObject3D pathObject)
				{
					var vertexStorage = pathObject.VertexSource as VertexStorage;

					activePoints = vertexStorage.Vertices();

					//foreach (var v in activePoints.Where(p => p.command != ShapePath.FlagsAndCommand.FlagNone && !p.IsClose))

					for (var i = 0; i < vertexStorage.Count; i++)
					{
						var command = vertexStorage.vertex(i, out double x, out double y);

						if (ShapePath.is_vertex(command))
						{
							var widget = new VertexPointWidget(interactionContext, vertexStorage, new Vector3(x, y, 0), command, i);
							widget.Click += (s, e) =>
							{
								Console.WriteLine("Hello Worl2d!");
							};

							targets.Add(widget);

							interactionContext.GuiSurface.AddChild(widget);
						}
					}
				}
			}

			foreach (var item in targets)
			{
				item.UpdatePosition();
			}
		}

		private class VertexPointWidget : PointWidget
		{
			private ShapePath.FlagsAndCommand command;
			private int index;
			private VertexStorage vertexStorage;

			public VertexPointWidget(IInteractionVolumeContext interactionContext, VertexStorage vertexStorage, Vector3 point, ShapePath.FlagsAndCommand flagsandCommand, int index)
				: base(interactionContext, point)
			{
				this.command = flagsandCommand;
				this.index = index;
				this.vertexStorage = vertexStorage;
			}

			protected override void OnDragTo(IntersectInfo info)
			{
				this.Point = info.HitPosition;
				vertexStorage.modify_vertex(index, info.HitPosition.X, info.HitPosition.Y);
				this.Invalidate();
				base.OnDragTo(info);
			}
		}

		private class PointWidget : GuiWidget
		{
			private WorldView world;
			private GuiWidget guiSurface;
			private bool mouseInBounds;
			private GuiWidget systemWindow;
			private bool mouseDownOnWidget;
			private IInteractionVolumeContext interactionContext;
			private static PlaneShape bedPlane = new PlaneShape(Vector3.UnitZ, 0, null);

			public PointWidget(IInteractionVolumeContext interactionContext, Vector3 point)
			{
				this.interactionContext = interactionContext;
				this.HAnchor = HAnchor.Absolute;
				this.VAnchor = VAnchor.Absolute;
				this.Width = 12;
				this.Height = 12;
				this.Point = point;

				world = interactionContext.World;
				guiSurface = interactionContext.GuiSurface;
			}

			protected Vector3 Point { get; set; }

			public override void OnLoad(EventArgs args)
			{
				// Register listeners
				systemWindow = this.Parents<SystemWindow>().First();
				systemWindow.AfterDraw += this.Parent_AfterDraw;

				base.OnLoad(args);
			}

			public override void OnClosed(EventArgs e)
			{
				// Unregister listeners
				if (systemWindow != null)
				{
					systemWindow.AfterDraw -= this.Parent_AfterDraw;
				}

				base.OnClosed(e);
			}

			public override void OnMouseDown(MouseEventArgs mouseEvent)
			{
				mouseDownOnWidget = mouseEvent.Button == MouseButtons.Left && this.PositionWithinLocalBounds(mouseEvent.Position);
				base.OnMouseDown(mouseEvent);
			}

			public override void OnMouseUp(MouseEventArgs mouseEvent)
			{
				mouseDownOnWidget = false;
				base.OnMouseUp(mouseEvent);
			}

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				// Drag item
				if (mouseDownOnWidget)
				{
					var localMousePosition = mouseEvent.Position;

					Vector2 meshViewerWidgetScreenPosition = this.TransformToParentSpace(guiSurface, localMousePosition);
					Ray ray = world.GetRayForLocalBounds(meshViewerWidgetScreenPosition);

					if (bedPlane.GetClosestIntersection(ray) is IntersectInfo info)
					{
						this.OnDragTo(info);
					}
				}

				base.OnMouseMove(mouseEvent);
			}

			protected virtual void OnDragTo(IntersectInfo info)
			{
			}

			public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
			{
				mouseInBounds = true;
				base.OnMouseEnterBounds(mouseEvent);
				this.Invalidate();
			}

			public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
			{
				mouseInBounds = false;
				base.OnMouseLeaveBounds(mouseEvent);
				this.Invalidate();
			}

			private void Parent_AfterDraw(object sender, DrawEventArgs e)
			{
				// AfterDraw listener registered on parent to draw outside of bounds
				if (mouseInBounds)
				{
					var position = this.TransformToScreenSpace(LocalBounds.Center);
					e.Graphics2D.Circle(position, 9, Color.Blue.WithAlpha(80));
				}
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				graphics2D.Circle(6, 6, 3, Color.Black);
				base.OnDraw(graphics2D);
			}

			public void UpdatePosition()
			{
				this.Position = world.GetScreenPosition(Point) - new Vector2(this.LocalBounds.Width / 2, this.LocalBounds.Height / 2);
			}
		}
	}
}
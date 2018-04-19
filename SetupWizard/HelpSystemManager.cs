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

using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using System;
using System.Linq;

namespace MatterHackers.MatterControl.SetupWizard
{
	public class HelpOverlay : GuiWidget
	{
		private double animationRatio = 0;
		private bool DoneAnimating => animationRatio >= 1;
		private GuiWidget target;
		private string message;
		bool addedDescription = false;
		Animation showAnimation;

		public HelpOverlay(GuiWidget target, string message)
		{
			this.target = target;
			this.message = message;
			HAnchor = HAnchor.Stretch;
			VAnchor = VAnchor.Stretch;

			showAnimation = new Animation()
			{
				DrawTarget = this,
			};

			showAnimation.Update += (s, timePassed) =>
			{
				animationRatio += timePassed;
				if(animationRatio >= 1)
				{
					showAnimation?.Stop();
					showAnimation = null;
				}
			};
		}

		public override void OnClick(MouseEventArgs mouseEvent)
		{
			if (DoneAnimating)
			{
				CloseOnIdle();
			}
			base.OnClick(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if(showAnimation != null
				&& !showAnimation.IsRunning)
			{
				showAnimation.Start();
			}

			var backgroundColor = new Color(Color.Black, 100);

			BackgroundColor = Color.Transparent;

			var childBounds = target.TransformToParentSpace(this, target.LocalBounds);

			VertexStorage dimRegion = new VertexStorage();
			dimRegion.MoveTo(LocalBounds.Left, LocalBounds.Bottom);
			dimRegion.LineTo(LocalBounds.Right, LocalBounds.Bottom);
			dimRegion.LineTo(LocalBounds.Right, LocalBounds.Top);
			dimRegion.LineTo(LocalBounds.Left, LocalBounds.Top);

			var ratio = Easing.Quadratic.InOut(Math.Min(animationRatio, 1));

			double closingLeft = LocalBounds.Left + (childBounds.Left - LocalBounds.Left) * ratio;
			double closingRight = LocalBounds.Right - (LocalBounds.Right - childBounds.Right) * ratio;
			double closingBottom = LocalBounds.Bottom + (childBounds.Bottom - LocalBounds.Bottom) * ratio;
			double closingTop = LocalBounds.Top - (LocalBounds.Top - childBounds.Top) * ratio;

			dimRegion.MoveTo(closingRight, closingBottom);
			dimRegion.LineTo(closingLeft, closingBottom);
			dimRegion.LineTo(closingLeft, closingTop);
			dimRegion.LineTo(closingRight, closingTop);

			graphics2D.Render(dimRegion, backgroundColor);

			BorderDouble margin = new BorderDouble(10);

			if (ratio >= 1
				&& !addedDescription)
			{
				addedDescription = true;
				UiThread.RunOnIdle(() =>
				{
					CloseAllChildren();

					var text = new TextWidget(message)
					{
						HAnchor = HAnchor.Center,
						VAnchor = VAnchor.Center,
						TextColor = Color.White
					};

					var child = new GuiWidget(text.Width + margin.Width, text.Height + margin.Height);

					child.AddChild(text);

					child.Position = new VectorMath.Vector2(childBounds.Right - child.Width,
						childBounds.Bottom - child.Height - margin.Top);

					child.BeforeDraw += (s, e) =>
					{
						e.Graphics2D.Render(new RoundedRect(child.LocalBounds, 5), Color.Black);
					};

					AddChild(child);
				});
			}

			base.OnDraw(graphics2D);
		}
	}

	public class HelpSystemManager
	{
		private SystemWindow SystemWindow { get; set; }

		private string message;
		private GuiWidget target;
		private static HelpSystemManager _instance;

		public static HelpSystemManager Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new HelpSystemManager();
				}
				return _instance;
			}
		}

		private HelpSystemManager()
		{
		}

		public void ShowTip(SystemWindow systemWindow, string widgetName, string message)
		{
			this.SystemWindow = systemWindow;
			this.message = message;
			target = systemWindow.Descendants().Where((w) => w.Name == widgetName).FirstOrDefault();

			if (target != null)
			{
				target.AfterDraw -= DoShowTip;
			}
			// hook the widget draw and wait for it to draw so that we know it is visible
			target.AfterDraw += DoShowTip;
			target.Invalidate();
		}

		private void DoShowTip(object sender, DrawEventArgs drawEvent)
		{
			if (target != null)
			{
				target.AfterDraw -= DoShowTip;

				SystemWindow.AddChild(new HelpOverlay(target, message));
			}
		}
	}
}
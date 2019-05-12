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
using System.Linq;
using MatterHackers.Agg.Platform;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PartPreviewWindow;

namespace MatterHackers.Agg.UI
{
	public class SingleWindowProvider : ISystemWindowProvider
	{
		protected List<SystemWindow> _openWindows = new List<SystemWindow>();
		protected IPlatformWindow platformWindow;

		SystemWindow _topWindow;
		public SystemWindow TopWindow
		{
			get => _topWindow;

			private set
			{
				void MaintainSizes(object s, EventArgs e)
				{
					foreach (var window in _openWindows)
					{
						if (_topWindow != window)
						{
							window.LocalBounds = new RectangleDouble(0, 0, _topWindow.Width, _topWindow.Height);
						}
					}
				}

				if (_topWindow != null)
				{
					_topWindow.SizeChanged -= MaintainSizes;
				}

				_topWindow = value;

				if (_topWindow != null)
				{
					_topWindow.SizeChanged += MaintainSizes;
				}
			}
		}

		public IReadOnlyList<SystemWindow> OpenWindows => _openWindows;

		/// <summary>
		/// Creates or connects a PlatformWindow to the given SystemWindow
		/// </summary>
		public virtual void ShowSystemWindow(SystemWindow systemWindow)
		{
			if (_openWindows.Count == 0)
			{
				this._openWindows.Add(systemWindow);
			}
			else
			{
				if (systemWindow.PlatformWindow != null)
				{
					return;
				}

				var overlayWindow = new SystemWindow(_openWindows.FirstOrDefault().Width, _openWindows.FirstOrDefault().Height)
				{
					PlatformWindow = platformWindow
				};

				_openWindows.FirstOrDefault().Unfocus();

				systemWindow.HAnchor = HAnchor.Stretch;
				systemWindow.VAnchor = VAnchor.Stretch;

				var theme = ApplicationController.Instance.Theme;

				var movable = new WindowWidget(systemWindow)
				{
					WindowBorderColor = new Color(theme.PrimaryAccentColor, 175)
				};

				movable.Width = Math.Min(overlayWindow.Width, movable.Width);
				movable.Height = Math.Min(overlayWindow.Height, movable.Height);

				overlayWindow.AddChild(movable);

				var closeButton = theme.CreateSmallResetButton();
				closeButton.HAnchor = HAnchor.Right;
				closeButton.ToolTipText = "Close".Localize();
				closeButton.Click += (s, e) =>
				{
					systemWindow.Close();
				};

				var titleBarRow = new Toolbar(theme, closeButton)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit | VAnchor.Center,
				};

				titleBarRow.AddChild(new ImageWidget(AggContext.StaticData.LoadIcon("mh.png", 16, 16, theme.InvertIcons))
				{
					Margin = new BorderDouble(4, 0, 6, 0),
					VAnchor = VAnchor.Center
				});

				titleBarRow.ActionArea.AddChild(new TextWidget(systemWindow.Title ?? "", pointSize: theme.DefaultFontSize - 1, textColor: theme.TextColor)
				{
					VAnchor = VAnchor.Center,
				});

				movable.TitleBar.BackgroundColor = theme.TabBarBackground;

				movable.TitleBar.AddChild(titleBarRow);

				void SystemWindow_VisibleChanged(object sender, EventArgs e)
				{
					if (systemWindow.Visible)
					{
						_openWindows.Add(overlayWindow);
						this.TopWindow = overlayWindow;

						overlayWindow.Visible = true;
					}
					else
					{
						_openWindows.Remove(overlayWindow);
						this.TopWindow = _openWindows.LastOrDefault();

						overlayWindow.Visible = false;
					}

					platformWindow.ShowSystemWindow(TopWindow);
				};

				void SystemWindow_BoundsChanged(object sender, EventArgs e)
				{
					var position = movable.Position;

					// Adjust Y
					if (position.Y + movable.Height > overlayWindow.Height)
					{
						position.Y = overlayWindow.Height - movable.Height;
					}

					// Adjust X
					if (position.X + movable.Width > overlayWindow.Width)
					{
						position.X = Math.Max(0, overlayWindow.Width - movable.Width);
					}

					movable.Position = position;
				}

				overlayWindow.BoundsChanged += SystemWindow_BoundsChanged;

				systemWindow.VisibleChanged += SystemWindow_VisibleChanged;

				systemWindow.Closed += (s, e) =>
				{
					systemWindow.VisibleChanged -= SystemWindow_VisibleChanged;
					overlayWindow.BoundsChanged -= SystemWindow_BoundsChanged;

					overlayWindow.Close();
				};

				movable.Width += 1;

				movable.Position = new VectorMath.Vector2((overlayWindow.Width - movable.Width) / 2, (overlayWindow.Height - movable.Height) / 2);

				this._openWindows.Add(overlayWindow);
			}

			TopWindow = _openWindows.LastOrDefault();

			platformWindow.ShowSystemWindow(TopWindow);

			// Ensure focus is set to the new window
			systemWindow.Focus();
		}

		public virtual void CloseSystemWindow(SystemWindow systemWindow)
		{
			if (_openWindows.Count > 1)
			{
				if (systemWindow == _openWindows.FirstOrDefault())
				{
					foreach (var openWindow in _openWindows.Reverse<SystemWindow>())
					{
						openWindow.Close();
					}

					_openWindows.Clear();
				}

				// Find and remove the WindowContainer from the openWindows list
				_openWindows.Remove(systemWindow);
			}

			TopWindow = _openWindows.LastOrDefault();

			platformWindow.CloseSystemWindow(systemWindow);
		}
	}
}
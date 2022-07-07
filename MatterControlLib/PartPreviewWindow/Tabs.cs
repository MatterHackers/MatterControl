﻿/*
Copyright (c) 2022, Lars Brubaker, John Lewin
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
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public interface ITab
	{
		GuiWidget TabContent { get; }

		string Key { get; }

		string Text { get; }
	}

	/// <summary>
	/// A toolbar and associated tab body
	/// </summary>
	public class SimpleTabs : FlowLayoutWidget
	{
		public SimpleTabs(ThemeConfig theme, string overflowText, GuiWidget rightAnchorItem = null)
			: base(FlowDirection.TopToBottom)
		{
			this.TabContainer = this;

			if (rightAnchorItem == null)
			{
				TabBar = new OverflowBar(null, theme, overflowText)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit
				};
			}
			else
			{
				TabBar = new Toolbar(theme.TabbarPadding, rightAnchorItem)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit
				};
			}

			this.AddChild(this.TabBar);
		}

		public Toolbar TabBar { get; }

		public GuiWidget TabContainer { get; protected set; }

		public event EventHandler ActiveTabChanged;

		public IEnumerable<ITab> AllTabs
		{
			get
			{
				foreach (var child in this.TabBar.ActionArea.Children)
				{
					if (child is ITab iTab)
					{
						yield return iTab;
					}
				}
			}
		}

		public int TabCount => AllTabs.Count();

		public virtual void AddTab(GuiWidget tabWidget, int position = -1)
		{
			var iTab = tabWidget as ITab;

			tabWidget.Click += TabWidget_Click;

			this.TabBar.ActionArea.AddChild(tabWidget, position);

			this.TabContainer.AddChild(iTab.TabContent);
		}

		public virtual void AddTab(GuiWidget tabWidget, int tabPosition, int widgetPosition)
		{
			var iTab = tabWidget as ITab;

			tabWidget.Click += TabWidget_Click;

			this.TabBar.ActionArea.AddChild(tabWidget, widgetPosition);

			this.TabContainer.AddChild(iTab.TabContent);
		}

		private void TabWidget_Click(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				var tab = sender as ITab;
				this.ActiveTab = tab;

				// Push focus to tab content on tab pill selection
				tab.TabContent.Focus();
			}
		}

		public string SelectedTabKey
		{
			get
			{
				return this.ActiveTab?.Key;
			}
			set
			{
				var foundTab = AllTabs.First();
				foreach (var tab in AllTabs)
				{
					if (tab.Key == value)
					{
						foundTab = tab;
					}
				}

				this.ActiveTab = foundTab;
			}
		}

		public int SelectedTabIndex
		{
			get => AllTabs.IndexOf(this.ActiveTab);
			set
			{
				int index = 0;
				foreach (var tab in AllTabs)
				{
					if (index == value)
					{
						this.ActiveTab = tab;
					}
				}
			}
		}

		internal virtual void CloseTab(ITab tab)
		{
			// Close Tab and TabContent widgets
			tab.TabContent.Close();
			(tab as GuiWidget)?.Close();

			if (tab is ChromeTab chromeTab)
			{
				// Activate next or last tab
				ActiveTab = chromeTab.NextTab ?? AllTabs.LastOrDefault();
			}
			else
			{
				// Activate last tab
				ActiveTab = AllTabs.LastOrDefault();
			}
		}

		private ITab _activeTab;

		public ITab ActiveTab
		{
			get => _activeTab;
			set
			{
				if (_activeTab != value)
				{
					_activeTab = value;

					var clickedWidget = value as GuiWidget;

					foreach (var tab in AllTabs)
					{
						tab.TabContent.Visible = tab == clickedWidget;
					}

					this.OnActiveTabChanged();
				}
			}
		}

		// default to no movable tabs
		public int FirstMovableTab { get; set; } = int.MaxValue;

		public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			if (this.TabContainer == this)
			{
				return base.AddChild(childToAdd, indexInChildrenList);
			}
			else
			{
				return this.TabContainer.AddChild(childToAdd, indexInChildrenList);
			}
		}

		protected virtual void OnActiveTabChanged()
		{
			this.ActiveTabChanged?.Invoke(this, null);
		}

		internal int GetTabIndex(SimpleTab tab)
		{
			return AllTabs.IndexOf(tab);
		}
	}

	public class ChromeTabs : SimpleTabs
	{
		private TabTrailer tabTrailer;

		private GuiWidget leadingTabAdornment;

		public event EventHandler PlusClicked;

		public ChromeTabs(GuiWidget rightAnchorItem, ThemeConfig theme)
			: base(theme, null, rightAnchorItem)
		{
			leadingTabAdornment = new GuiWidget()
			{
				MinimumSize = new Vector2(16 * GuiWidget.DeviceScale, theme.TabButtonHeight),
				VAnchor = VAnchor.Bottom
			};
			leadingTabAdornment.AfterDraw += (s, e) =>
			{
				var firstItem = this.AllTabs.OfType<ChromeTab>().FirstOrDefault();
				ChromeTab.DrawTabLowerRight(e.Graphics2D, leadingTabAdornment.LocalBounds, (firstItem == this.ActiveTab) ? theme.BackgroundColor : theme.InactiveTabColor);
			};
			this.TabBar.ActionArea.AddChild(leadingTabAdornment);

			this.TabBar.MouseMove += (s, e) =>
			{
				try
				{
					if (e?.DragFiles?.Count > 0
						&& e.DragFiles.Where(f => ApplicationController.ShellFileExtensions.Contains(Path.GetExtension(f).ToLower())).Any())
					{
						e.AcceptDrop = true;
					}
				}
				catch
                {
                }
			};

			TabBar.MouseEnterBounds += (s, e) =>
			{
				ApplicationController.Instance.UiHint = "You can drag and drop .mcx files here to open them";
			};

			TabBar.MouseLeaveBounds += (s, e) =>
			{
				ApplicationController.Instance.UiHint = "";
			};

			this.TabBar.MouseUp += (s, e) =>
			{
				if (e?.DragFiles?.Count > 0
					&& e.DragFiles.Where(f => ApplicationController.ShellFileExtensions.Contains(Path.GetExtension(f).ToLower())).Any())
				{
					foreach (var file in e.DragFiles)
					{
						ApplicationController.Instance.MainView.OpenFile(file);
					}
				}
			};

			tabTrailer = new TabTrailer(this, theme)
			{
				VAnchor = VAnchor.Bottom,
				MinimumSize = new Vector2(8 * GuiWidget.DeviceScale, theme.TabButtonHeight),
			};

			this.TabBar.ActionArea.AddChild(tabTrailer);

			var plusTabButton = new NewTabButton(StaticData.Instance.LoadIcon("fa-plus_12.png", 12, 12).SetToColor(theme.TextColor), theme)
			{
				Height = 20 * GuiWidget.DeviceScale,
			};

			plusTabButton.IconButton.Click += (s, e) =>
			{
				this.PlusClicked?.Invoke(this, null);
			};

			this.TabBar.ActionArea.AddChild(plusTabButton);
		}

		public override void AddTab(GuiWidget tabWidget, int tabIndex = -1)
		{
			// Default position if tabIndex == -1 is just before the tabTrailer
			var widgetPosition = this.TabBar.ActionArea.Children.IndexOf(tabTrailer);
			var firstTabPosition = this.TabBar.ActionArea.Children.IndexOf(leadingTabAdornment) + 1;

			if (tabIndex != -1)
			{
				// Adjust position to be the head of the list + the tabIndex offset
				widgetPosition = firstTabPosition + tabIndex;
			}

			if (tabWidget is ChromeTab newTab)
			{
				newTab.TabContent.Visible = false;

				// Call AddTab(widget, int) in base explicitly
				base.AddTab(tabWidget, widgetPosition - firstTabPosition, widgetPosition);
			}
		}

		public void MoveTabRight(ITab tab)
		{
			var index = AllTabs.IndexOf(tab);
			var tabWidget = tab as GuiWidget;

			if (index >= FirstMovableTab
				&& index < AllTabs.Count() - 1)
			{
				TabBar.ActionArea.Children.Modify(list =>
				{
					var tabIndex = list.IndexOf(tabWidget);
					list.Remove(tabWidget);
					list.Insert(tabIndex + 1, tabWidget);
				});

				var savedIndex = index - 3;
				var moving = ApplicationController.Instance.Workspaces[savedIndex];
				ApplicationController.Instance.Workspaces.RemoveAt(savedIndex);
				ApplicationController.Instance.Workspaces.Insert(savedIndex + 1, moving);

				TabBar.ActionArea.PerformLayout();

				ActiveTab = tab;
			}
		}

		public void MoveTabLeft(ITab tab)
		{
			var index = AllTabs.IndexOf(tab);
			var tabWidget = tab as GuiWidget;

			if (index >= FirstMovableTab + 1
				&& index < AllTabs.Count())
			{
				TabBar.ActionArea.Children.Modify(list =>
				{
					var tabIndex = list.IndexOf(tabWidget);
					list.Remove(tabWidget);
					list.Insert(tabIndex - 1, tabWidget);
				});

				var savedIndex = index - 3;
				var moving = ApplicationController.Instance.Workspaces[savedIndex];
				ApplicationController.Instance.Workspaces.RemoveAt(savedIndex);
				ApplicationController.Instance.Workspaces.Insert(savedIndex - 1, moving);

				TabBar.ActionArea.PerformLayout();

				ActiveTab = tab;
			}
		}

		public Func<GuiWidget> NewTabPage { get; set; }
	}

	public class SimpleTab : GuiWidget, ITab
	{
		public event EventHandler CloseClicked;

		protected SimpleTabs parentTabControl;

		protected ThemeConfig theme;

		protected TabPill tabPill;

		public GuiWidget TabContent { get; protected set; }

		public string Key { get; set; }

		private bool hasClose = false;

		public SimpleTab(string tabKey, string tabLabel, SimpleTabs parentTabControl, GuiWidget tabContent, ThemeConfig theme, string tabImageUrl = null, bool hasClose = true, double pointSize = 12, ImageBuffer iconImage = null)
		{
			this.Key = tabKey;
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit | VAnchor.Bottom;
			this.Padding = 0;
			this.Margin = 0;
			this.theme = theme;
			this.hasClose = hasClose;

			this.TabContent = tabContent;
			this.parentTabControl = parentTabControl;

			if (iconImage != null)
			{
				tabPill = new TabPill(tabLabel, theme.TextColor, iconImage, pointSize);
			}
			else
			{
				tabPill = new TabPill(tabLabel, theme.TextColor, tabImageUrl, pointSize);
			}

			tabPill.Margin = hasClose ? new BorderDouble(right: 16) : 0;

			this.AddChild(tabPill);

			if (hasClose)
			{
				// var fadeRegion = new LeftClipFlowLayoutWidget();
				// fadeRegion.HAnchor |= HAnchor.Right;
				// this.AddChild(fadeRegion);

				var closeButton = theme.CreateSmallResetButton();
				closeButton.Margin = new BorderDouble(right: 7, top: 1);
				closeButton.Name = "Close Tab Button";
				closeButton.ToolTipText = "Close".Localize();
				closeButton.Click += (s, e) => ConditionallyCloseTab();
				closeButton.HAnchor |= HAnchor.Right;
				closeButton.VAnchor = VAnchor.Center;
				// fadeRegion.AddChild(closeButton);
				this.AddChild(closeButton);
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if(mouseEvent.Button == MouseButtons.Middle
				&& hasClose)
			{
				ConditionallyCloseTab();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);

			// Clear all listeners
			this.CloseClicked = null;
		}

		private void ConditionallyCloseTab()
		{
			UiThread.RunOnIdle(() =>
			{
				if (this.TabContent is PrinterTabPage printerTab
					&& printerTab.Printer.Connection.Printing)
				{
					StyledMessageBox.ShowMessageBox(
						(bool response) =>
						{
							if (response)
							{
								UiThread.RunOnIdle(() =>
								{
									this.parentTabControl.CloseTab(this);
									this.CloseClicked?.Invoke(this, null);
								});
							}
						},
						"Cancel the current print?".Localize(),
						"Cancel Print?".Localize(),
						StyledMessageBox.MessageType.YES_NO,
						"Cancel Print".Localize(),
						"Continue Printing".Localize());
				}
				else if (this.TabContent is DesignTabPage partTab
					&& partTab?.Workspace?.SceneContext?.Scene is InteractiveScene scene
					&& scene.HasUnsavedChanges)
				{
					StyledMessageBox.ShowYNCMessageBox(
						(response) =>
						{
							switch (response)
							{
								case StyledMessageBox.ResponseType.YES:
									UiThread.RunOnIdle(async () =>
									{
										var sceneContext = partTab.Workspace.SceneContext;
										if (sceneContext.EditContext.ContentStore == null)
										{
											// If we are about to close a tab that has never been saved it will need a name before it can actually save
											// Open up the save as dialog rather than continue with saving and closing
											DialogWindow.Show(
												new SaveAsPage(
													(container, newName) =>
													{
														sceneContext.SaveAs(container, newName);
														// If we succeed at saveing the file go ahead and finish closing this tab
														this.CloseClicked?.Invoke(this, null);
														// Must be called after CloseClicked otherwise listeners are cleared before event is invoked
														this.parentTabControl.CloseTab(this);
													}));
										}
										else
										{
											await ApplicationController.Instance.Tasks.Execute("Saving Changes".Localize(), this, partTab.Workspace.SceneContext.SaveChanges);

											this.CloseClicked?.Invoke(this, null);
											// Must be called after CloseClicked otherwise listeners are cleared before event is invoked
											this.parentTabControl.CloseTab(this);
										}
									});
									break;

								case StyledMessageBox.ResponseType.NO:
									UiThread.RunOnIdle(() =>
									{
										this.CloseClicked?.Invoke(this, null);
										// Must be called after CloseClicked otherwise listeners are cleared before event is invoked
										this.parentTabControl?.CloseTab(this);
									});
									break;
							}
						},
						"Wolud you like to save changes before closing?".Localize(),
						"Save Changes?".Localize(),
						"Save Changes".Localize(),
						"Discard Changes".Localize(),
						"Cancel".Localize());
				}
				else
				{
					this.CloseClicked?.Invoke(this, null);
					// Must be called after CloseClicked otherwise listeners are cleared before event is invoked
					this.parentTabControl?.CloseTab(this);
				}
			});
		}

		public class TabPill : FlowLayoutWidget
		{
			private TextWidget label;
			private ImageWidget imageWidget;

			public TabPill(string tabTitle, Color textColor, string imageUrl = null, double pointSize = 12)
				: this(tabTitle, textColor, string.IsNullOrEmpty(imageUrl) ? null : new ImageBuffer(16, 16).CreateScaledImage(GuiWidget.DeviceScale), pointSize)
			{
				if (imageWidget != null
					&& !string.IsNullOrEmpty(imageUrl))
				{
					try
					{
						// TODO: Use caching
						// Attempt to load image
						WebCache.RetrieveImageAsync(imageWidget.Image, imageUrl, true);
					}
					catch { }
				}
			}

			public TabPill(string tabTitle, Color textColor, ImageBuffer imageBuffer = null, double pointSize = 12)
			{
				this.Selectable = false;
				this.Padding = new BorderDouble(10, 5, 10, 4);

				if (imageBuffer != null)
				{
					imageWidget = new ImageWidget(imageBuffer)
					{
						Margin = new BorderDouble(right: 6, bottom: 2),
						VAnchor = VAnchor.Center
					};
					this.AddChild(imageWidget);
				}

				label = new TextWidget(tabTitle, pointSize: pointSize)
				{
					TextColor = textColor,
					VAnchor = VAnchor.Center,
					AutoExpandBoundsToText = true
				};
				this.AddChild(label);
			}

			public Color TextColor
			{
				get => label.TextColor;
				set => label.TextColor = value;
			}

			public override string Text
			{
				get => label.Text;
				set => label.Text = value;
			}
		}
	}

	public class ToolTab : SimpleTab
	{
		public Color InactiveTabColor { get; set; }

		public Color ActiveTabColor { get; set; }

		public override Color BorderColor
		{
			get =>  (this.IsActiveTab) ? theme.PrimaryAccentColor : base.BorderColor;
			set => base.BorderColor = value;
		}

		public ToolTab(string tabKey, string tabLabel, SimpleTabs parentTabControl, GuiWidget tabContent, ThemeConfig theme, string tabImageUrl = null, bool hasClose = true, int pointSize = -1)
			: base(tabKey, tabLabel, parentTabControl, tabContent, theme, tabImageUrl, hasClose, pointSize: (pointSize == -1) ? theme.FontSize10 : pointSize)
		{
			this.Border = new BorderDouble(top: 1);
			this.InactiveTabColor = Color.Transparent;
			this.ActiveTabColor = theme.BackgroundColor;

			tabPill.Padding = tabPill.Padding.Clone(top: 10, bottom: 10);
		}

		private bool IsActiveTab => this == parentTabControl.ActiveTab;

		public override string Text { get => tabPill.Text; set => tabPill.Text = value; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.Render(
				new RoundedRect(this.LocalBounds, 0),
				(this.IsActiveTab) ? this.ActiveTabColor : this.InactiveTabColor);

			base.OnDraw(graphics2D);
		}
	}

	public class ChromeTab : SimpleTab
	{
		public ChromeTab(string tabKey, string tabLabel, SimpleTabs parentTabControl, GuiWidget tabContent, ThemeConfig theme, string tabImageUrl = null, bool hasClose = true)
			: base(tabKey, tabLabel, parentTabControl, tabContent, theme, tabImageUrl, hasClose)
		{
		}

		public ChromeTab(string tabKey, string tabLabel, SimpleTabs parentTabControl, GuiWidget tabContent, ThemeConfig theme, ImageBuffer imageBuffer, bool hasClose = true)
			: base(tabKey, tabLabel, parentTabControl, tabContent, theme, iconImage: imageBuffer, hasClose: hasClose)
		{
			this.Text = tabLabel;
		}

		private static int tabInsetDistance = 14 / 2;

        internal ChromeTab NextTab
		{
			get
			{
				var owner = this.Parent;
				if (owner != null)
				{
					var found = false;
					foreach(var item in owner.Children)
					{
						if (item == this)
						{
							found = true;
						}
						else if (found && item is ChromeTab chromeTab)
						{
							return chromeTab;
						}
					}

				}

				return null;
			}
		}

		internal ChromeTab PreviousTab
		{
			get
			{
				var owner = this.Parent;
				if (owner != null)
				{
					ChromeTab last = null;
					foreach (var item in owner.Children)
					{
						if (item == this)
						{
							return last;
						}
						else if (item is ChromeTab chromeTab)
						{
							last = chromeTab;
						}
					}
				}

				return null;
			}
		}


		public override void OnDraw(Graphics2D graphics2D)
		{
			var rect = LocalBounds;
			var centerY = rect.YCenter;

			if (this.Parent == null)
			{
				return;
			}

			var siblings = this.Parent.Children.OfType<ChromeTab>().ToList();

			int position = siblings.IndexOf(this);

			// MainTab leftSibling = (position > 0) ? siblings[position - 1] : null;
			// MainTab rightSibling = (position < siblings.Count - 1) ? siblings[position + 1] : null;

			var activeTab = parentTabControl.ActiveTab;

			bool isFirstTab = position == 0;
			bool rightSiblingSelected = this.NextTab == activeTab;
			bool leftSiblingSelected = this.PreviousTab == activeTab;

			bool drawLeftTabOverlap = this != activeTab && !isFirstTab;

			// Tab - core
			var tabShape = new VertexStorage();
			tabShape.MoveTo(rect.Left, centerY);
			tabShape.LineTo(rect.Left + tabInsetDistance, rect.Top);
			tabShape.LineTo(rect.Right - tabInsetDistance, rect.Top);
			tabShape.LineTo(rect.Right, centerY);
			if (!rightSiblingSelected)
			{
				tabShape.LineTo(rect.Right, rect.Bottom);
			}

			tabShape.LineTo(rect.Right - tabInsetDistance, rect.Bottom);
			tabShape.LineTo(rect.Left + tabInsetDistance, rect.Bottom);
			if (!drawLeftTabOverlap)
			{
				tabShape.LineTo(rect.Left, rect.Bottom);
			}

			graphics2D.Render(
				tabShape,
				(this == activeTab) ? theme.BackgroundColor : theme.InactiveTabColor);

			if (drawLeftTabOverlap)
			{
				DrawTabLowerLeft(
					graphics2D,
					rect,
					(leftSiblingSelected || this == activeTab) ? theme.BackgroundColor : theme.InactiveTabColor);
			}

			if (rightSiblingSelected)
			{
				DrawTabLowerRight(graphics2D, rect, theme.BackgroundColor);
			}

			base.OnDraw(graphics2D);
		}

        public void HookupNameChange(PartWorkspace workspace)
        {
            throw new NotImplementedException();
        }

        public override void OnClosed(EventArgs e)
		{
			this.parentTabControl = null;
			this.TabContent = null;

			base.OnClosed(e);
		}

        public override string Text
		{
			get => tabPill?.Text;
			set
			{
				if (tabPill != null)
				{
					tabPill.Text = value;
				}
			}
		}

        public override string ToolTipText
		{
			get => tabPill?.ToolTipText;
			set
			{
				if (tabPill != null)
				{
					tabPill.ToolTipText = value;
				}
			}
		}

		public static void DrawTabLowerRight(Graphics2D graphics2D, RectangleDouble rect, Color color)
		{
			// Tab - right nub
			var tabRight = new VertexStorage();
			tabRight.MoveTo(rect.Right, rect.YCenter);
			tabRight.LineTo(rect.Right, rect.Bottom);
			tabRight.LineTo(rect.Right - tabInsetDistance, rect.Bottom);

			graphics2D.Render(tabRight, color);
		}

		public static void DrawTabLowerLeft(Graphics2D graphics2D, RectangleDouble rect, Color color)
		{
			// Tab - left nub
			var tabLeft = new VertexStorage();
			tabLeft.MoveTo(rect.Left, rect.YCenter);
			tabLeft.LineTo(rect.Left + tabInsetDistance, rect.Bottom);
			tabLeft.LineTo(rect.Left, rect.Bottom);

			graphics2D.Line(rect.Left, rect.YCenter, rect.Left + tabInsetDistance, rect.Bottom, AppContext.Theme.MinimalShade, 1.3);

			graphics2D.Render(tabLeft, color);
		}
	}

	public static class TabsExtensions
	{
		public static int IndexOf<ITab>(this IEnumerable<ITab> source, ITab value)
		{
			int index = 0;
			var comparer = EqualityComparer<ITab>.Default; // or pass in as a parameter
			foreach (ITab item in source)
			{
				if (comparer.Equals(item, value)) return index;
				index++;
			}
			return -1;
		}
	}
}

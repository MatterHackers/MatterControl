/*
Copyright (c) 2017, Lars Brubaker
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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	/// <summary>
	/// A toolbar with an optional right anchored element and an ActionBar child to add  actions to the bar
	/// </summary>
	public class Toolbar : Bar
	{
		public FlowLayoutWidget ActionBar { get; }

		public HorizontalLine SeparatorLine { get; }

		public Toolbar(GuiWidget rightAnchorItem, ThemeConfig theme, bool bottomBorder = true)
			: base(rightAnchorItem, theme)
		{
			GuiWidget context = this;

			this.ActionBar = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch
			};

			if (bottomBorder)
			{
				var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit
				};
				this.AddChild(column, 0);

				column.AddChild(this.ActionBar);
				column.AddChild(this.SeparatorLine = new HorizontalLine(40));
			}
			else
			{
				this.AddChild(this.ActionBar, 0);
			}
		}
	}

	public interface ITab
	{
		GuiWidget TabContent { get; }
	}

	/// <summary>
	/// A toolbar like item with an optional right anchored element
	/// </summary>
	public class Bar : GuiWidget
	{
		public Bar(GuiWidget rightAnchorItem, ThemeConfig theme)
		{
			if (rightAnchorItem != null)
			{
				rightAnchorItem.HAnchor |= HAnchor.Right;
				this.AddChild(rightAnchorItem);
			}
		}
	}

	/// <summary>
	/// A toolbar and associated tab body
	/// </summary>
	public class SimpleTabs : FlowLayoutWidget
	{
		public Toolbar TabBar { get; }

		private GuiWidget body;

		public SimpleTabs(GuiWidget rightAnchorItem, ThemeConfig theme, bool bottomBorder = true)
			: base(FlowDirection.TopToBottom)
		{
			this.AddChild(TabBar = new Toolbar(rightAnchorItem, theme, bottomBorder)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			});

			this.AddChild(body = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			});
		}

		public event EventHandler ActiveTabChanged;

		private List<ITab> _allTabs = new List<ITab>();

		public IEnumerable<ITab> AllTabs => _allTabs;

		public void AddTab(GuiWidget tabWidget, int position)
		{
			var iTab = tabWidget as ITab;

			_allTabs.Add(tabWidget as ITab);

			tabWidget.Click += TabWidget_Click;

			this.TabBar.ActionBar.AddChild(tabWidget, position);

			this.body.AddChild(iTab.TabContent);
		}

		private void TabWidget_Click(object sender, MouseEventArgs e)
		{
			this.ActiveTab = sender as ITab;
		}

		internal void RemoveTab(ITab tab)
		{
			_allTabs.Remove(tab);

			TabBar.ActionBar.RemoveChild(tab as GuiWidget);
			body.RemoveChild(tab.TabContent);

			ActiveTab = _allTabs.LastOrDefault();
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

					foreach (var tab in _allTabs)
					{
						tab.TabContent.Visible = (tab == clickedWidget);
					}

					this.OnActiveTabChanged();
				}
			}
		}

		protected virtual void OnActiveTabChanged()
		{
			this.ActiveTabChanged?.Invoke(this, null);
		}
	}

	public class ChromeTabs : SimpleTabs
	{
		private NewTabButton plusTabButton;

		public ChromeTabs(GuiWidget rightAnchorItem, ThemeConfig theme)
			: base(rightAnchorItem, theme)
		{
			// TODO: add in the printers and designs that are currently open (or were open last run).
			var leadingTabAdornment = new GuiWidget()
			{
				MinimumSize = new VectorMath.Vector2(16, theme.shortButtonHeight),
				VAnchor = VAnchor.Bottom
			};
			leadingTabAdornment.AfterDraw += (s, e) =>
			{
				var firstItem = this.AllTabs.OfType<MainTab>().FirstOrDefault();
				MainTab.DrawTabLowerRight(e.graphics2D, leadingTabAdornment.LocalBounds, (firstItem == this.ActiveTab) ? MainTab.ActiveTabColor : MainTab.InactiveTabColor);
			};
			this.TabBar.ActionBar.AddChild(leadingTabAdornment);

			// TODO: add in the printers and designs that are currently open (or were open last run).
			plusTabButton = new NewTabButton(
				AggContext.StaticData.LoadIcon("fa-plus_12.png", IconColor.Theme),
				this,
				theme)
			{
				VAnchor = VAnchor.Bottom,
				MinimumSize = new Vector2(16, theme.shortButtonHeight),
				ToolTipText = "Create New".Localize()
			};
			plusTabButton.IconButton.Click += (s, e) =>
			{
				this.AddTab(
					new MainTab("New Tab".Localize(), this, this.NewTabPage())
					{
						MinimumSize = new Vector2(0, theme.shortButtonHeight)
					});
			};

			this.TabBar.ActionBar.AddChild(plusTabButton);
		}

		public void AddTab(GuiWidget tab)
		{
			var position = this.TabBar.ActionBar.GetChildIndex(plusTabButton);

			if (tab is MainTab mainTab)
			{
				mainTab.PreviousTab = this.AllTabs.OfType<MainTab>().LastOrDefault();
				if (mainTab.PreviousTab != null)
				{
					mainTab.PreviousTab.NextTab = mainTab;
				}

				this.AddTab(tab, position);

				mainTab.CloseClicked += MainTab_CloseClicked;
				this.ActiveTab = mainTab;
			}
		}

		private void MainTab_CloseClicked(object sender, EventArgs e)
		{
			if (sender is ITab tab)
			{
				this.RemoveTab(sender as ITab);
				ApplicationController.Instance.ClearActivePrinter();
			}
		}

		public Func<GuiWidget> NewTabPage { get; set; }

		protected override void OnActiveTabChanged()
		{
			plusTabButton.LastTab = this.AllTabs.LastOrDefault();
			base.OnActiveTabChanged();
		}
	}
}
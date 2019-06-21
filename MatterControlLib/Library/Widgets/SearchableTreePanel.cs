/*
Copyright (c) 2019, John Lewin
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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public abstract class SearchableTreePanel : FlowLayoutWidget
	{
		protected SearchInputBox searchBox;
		protected TreeView treeView;
		protected Splitter horizontalSplitter;
		protected ThemeConfig theme;
		protected FlowLayoutWidget contentPanel;

		public SearchableTreePanel(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.TreeLoaded = false;

			var searchIcon = AggContext.StaticData.LoadIcon("icon_search_24x24.png", 16, 16, theme.InvertIcons).AjustAlpha(0.3);

			searchBox = new SearchInputBox(theme)
			{
				Name = "Search",
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(6),
			};

			searchBox.ResetButton.Visible = false;

			var searchInput = searchBox.searchInput;

			searchInput.BeforeDraw += (s, e) =>
			{
				if (!searchBox.ResetButton.Visible)
				{
					e.Graphics2D.Render(
						searchIcon,
						searchInput.Width - searchIcon.Width - 5,
						searchInput.LocalBounds.Bottom + searchInput.Height / 2 - searchIcon.Height / 2);
				}
			};

			searchBox.ResetButton.Click += (s, e) =>
			{
				this.ClearSearch();
			};

			searchBox.KeyDown += (s, e) =>
			{
				if (e.KeyCode == Keys.Escape)
				{
					this.ClearSearch();
					e.Handled = true;
				}
			};

			searchBox.searchInput.ActualTextEditWidget.TextChanged += (s, e) =>
			{
				if (string.IsNullOrWhiteSpace(searchBox.Text))
				{
					this.ClearSearch();
				}
				else
				{
					this.PerformSearch(searchBox.Text);
				}
			};

			horizontalSplitter = new Splitter()
			{
				SplitterDistance = Math.Max(UserSettings.Instance.LibraryViewWidth, 20),
				SplitterSize = theme.SplitterWidth,
				SplitterBackground = theme.SplitterBackground
			};
			horizontalSplitter.AnchorAll();

			horizontalSplitter.DistanceChanged += (s, e) =>
			{
				UserSettings.Instance.LibraryViewWidth = Math.Max(horizontalSplitter.SplitterDistance, 20);
			};

			this.AddChild(horizontalSplitter);

			var leftPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			leftPanel.AddChild(searchBox);

			treeView = new TreeView(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			leftPanel.AddChild(treeView);

			horizontalSplitter.Panel1.AddChild(leftPanel);

			contentPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(left: 2)
			};
			treeView.AddChild(contentPanel);
		}

		protected virtual void PerformSearch(string filter)
		{
			var matches = new List<TreeNode>();

			Console.WriteLine("Filter for: " + filter);

			foreach (var rootNode in contentPanel.Children.OfType<TreeNode>())
			{
				FilterTree(rootNode, filter, false, matches);
			}

			if (matches.Count == 1)
			{
				treeView.SelectedNode = matches.First();
			}
			else
			{
				treeView.SelectedNode = null;
			}

			searchBox.ResetButton.Visible = true;
		}

		private void ClearSearch()
		{
			foreach (var rootNode in contentPanel.Children.OfType<TreeNode>())
			{
				ResetTree(rootNode);
			}

			searchBox.Text = "";
			searchBox.ResetButton.Visible = false;
			treeView.SelectedNode = null;

			this.OnClearSearch();
		}

		protected abstract bool FilterTree(TreeNode context, string filter, bool parentVisible, List<TreeNode> matches);

		private void ResetTree(TreeNode context)
		{
			context.Visible = true;
			context.Expanded = false;

			foreach (var child in context.Nodes)
			{
				ResetTree(child);
			}
		}

		protected virtual void OnClearSearch()
		{
		}

		public bool TreeLoaded { get; protected set; }
	}
}

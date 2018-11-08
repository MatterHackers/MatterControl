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
using System.Collections.ObjectModel;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class TreeNode : FlowLayoutWidget, ICheckbox
	{
		private GuiWidget content;
		private TreeView _treeView;
		private ImageBuffer _image = null;
		private TextWidget textWidget;
		private TreeExpandWidget expandWidget;
		private ImageWidget imageWidget;
		private bool isDirty;

		public TreeNode(ThemeConfig theme, bool useIcon = true)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Fit | HAnchor.Left;
			this.VAnchor = VAnchor.Fit;

			this.TitleBar = new FlowLayoutWidget();
			this.TitleBar.Click += (s, e) =>
			{
				if (TreeView != null)
				{
					TreeView.SelectedNode = this;
				}

				this.TreeView.NotifyItemClicked(this.TitleBar, e);
			};

			this.TitleBar.MouseDown += (s, e) =>
			{
				if (TreeView != null
					&& e.Button == MouseButtons.Left
					&& e.Clicks == 2)
				{
					TreeView.SelectedNode = this;
					this.TreeView.NotifyItemDoubleClicked(this.TitleBar, e);
				}
			};

			this.AddChild(this.TitleBar);

			// add a check box
			expandWidget = new TreeExpandWidget(theme)
			{
				Expandable = GetNodeCount(false) != 0,
				VAnchor = VAnchor.Fit | VAnchor.Center,
				Height = 16,
				Width = 16
			};

			expandWidget.Click += (s, e) =>
			{
				this.Expanded = !this.Expanded;
				expandWidget.Expanded = this.Expanded;
			};

			this.TitleBar.AddChild(expandWidget);

			this.HighlightRegion = new FlowLayoutWidget()
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Fit,
				Padding = useIcon ? new BorderDouble(2) : new BorderDouble(4, 2),
				Selectable = false
			};
			this.TitleBar.AddChild(this.HighlightRegion);

			// add a check box
			if (useIcon)
			{
				_image = new ImageBuffer(16, 16);

				this.HighlightRegion.AddChild(imageWidget = new ImageWidget(this.Image)
				{
					VAnchor = VAnchor.Center,
					Margin = new BorderDouble(right: 4),
					Selectable = false
				});
			};

			this.HighlightRegion.AddChild(textWidget = new TextWidget(this.Text, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Selectable = false,
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center
			});

			content = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit | HAnchor.Left,
				Visible = false, // content starts out not visible
				Name = "content",
				Margin = new BorderDouble(12, 3),
			};
			this.AddChild(content);

			this.Nodes.CollectionChanged += (s, e) => isDirty = true;
		}

		public FlowLayoutWidget TitleBar { get; }

		public FlowLayoutWidget HighlightRegion { get; }

		// **** Not implemented ****
		public void BeginEdit() => throw new NotImplementedException();
		public void Collapse(bool collapseChildren) => throw new NotImplementedException();
		public void Collapse() => throw new NotImplementedException();
		public void EndEdit(bool cancel) => throw new NotImplementedException();
		public void EnsureVisible() => throw new NotImplementedException();
		public void ExpandAll() => throw new NotImplementedException();
		public void Remove() => throw new NotImplementedException();

		public int GetNodeCount(bool includeSubTrees)
		{
			if (includeSubTrees)
			{
				return this.Descendants<TreeNode>().Count();
			}

			return content?.Children.Where((c) => c is TreeNode).Count() ?? 0;
		}

		public bool AlwaysExpandable
		{
			get => expandWidget.AlwaysExpandable;
			set => expandWidget.AlwaysExpandable = value;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (isDirty)
			{
				// doing this durring draw will often result in a enumeration changed
				UiThread.RunOnIdle(RebuildContentSection);
			}

			base.OnDraw(graphics2D);
		}

		public override void OnTextChanged(EventArgs e)
		{
			if (textWidget != null)
			{
				textWidget.Text = this.Text;
			}

			base.OnTextChanged(e);
		}

		public void Toggle()
		{
			content.Visible = !content.Visible;
		}

		private void RebuildContentSection()
		{
			// Remove but don't close all the current nodes
			content.RemoveAllChildren();

			// Then add them back in (after the change)
			foreach (var node in Nodes)
			{
				node.NodeParent = this;
				node.ClearRemovedFlag();
				content.AddChild(node);
			}

			// If the node count is ending at 0 we removed content and need to rebuild the title bar so it will net have a + in it
			expandWidget.Expandable = GetNodeCount(false) != 0;

			isDirty = false;

		}

		#region Properties

		public bool Checked { get; set; }

		public bool Editing { get; }

		public bool Expandable
		{
			get => expandWidget.Expandable;
			set => expandWidget.Expandable = value;
		}

		public bool ReserveIconSpace
		{
			get => expandWidget.ReserveIconSpace;
			set => expandWidget.ReserveIconSpace = value;
		}

		private bool _expanded;
		public bool Expanded
		{
			get => _expanded;
			set
			{
				if (_expanded != value || content.Visible != value)
				{
					_expanded = value;
					expandWidget.Expanded = _expanded;

					content.Visible = _expanded && this.Nodes.Count > 0;
					ExpandedChanged?.Invoke(this, null);
				}
			}
		}

		public TreeNode FirstNode { get; }

		public ImageBuffer Image
		{
			get
			{
				return _image;
			}

			set
			{
				if (_image != value)
				{
					_image = value;

					if(imageWidget != null)
					{
						imageWidget.Image = _image;
					}

					OnImageChanged(null);
				}
			}
		}

		public TreeNode LastNode { get; }

		/// <summary>
		/// Gets the zero-based depth of the tree node in the TreeView control.
		/// </summary>
		public int Level { get; }

		//
		// Summary:
		//     Gets the next sibling tree node.
		//
		// Returns:
		//     A TreeNode that represents the next sibling tree node.
		public TreeNode NextNode { get; }

		//
		// Summary:
		//     Gets the next visible tree node.
		//
		// Returns:
		//     A TreeNode that represents the next visible tree node.
		public TreeNode NextVisibleNode { get; }

		//
		// Summary:
		//     Gets or sets the font that is used to display the text on the tree node label.
		//
		// Returns:
		//     The StyledTypeFace that is used to display the text on the tree node label.
		public StyledTypeFace NodeFont { get; set; }

		//
		// Summary:
		//     Gets the parent tree node of the current tree node.
		//
		// Returns:
		//     A TreeNode that represents the parent of the current tree
		//     node.
		public TreeNode NodeParent { get; protected set; }

		public ObservableCollection<TreeNode> Nodes { get; } = new ObservableCollection<TreeNode>();

		public int PointSize { get; set; }

		//
		// Summary:
		//     Gets the previous sibling tree node.
		//
		// Returns:
		//     A TreeNode that represents the previous sibling tree node.
		public TreeNode PrevNode { get; }

		//
		// Summary:
		//     Gets the previous visible tree node.
		//
		// Returns:
		//     A TreeNode that represents the previous visible tree node.
		public TreeNode PrevVisibleNode { get; }

		//
		// Summary:
		//     Gets a value indicating whether the tree node is in the selected state.
		//
		// Returns:
		//     true if the tree node is in the selected state; otherwise, false.
		public bool Selected
		{
			get
			{
				if (TreeView != null)
				{
					return TreeView.SelectedNode == this;
				}

				return false;
			}
		}

		//
		// Summary:
		//     Gets or sets the image list index value of the image that is displayed when the
		//     tree node is in the selected state.
		//
		// Returns:
		//     A zero-based index value that represents the image position in an ImageList.
		public ImageBuffer SelectedImage { get; set; }

		//
		// Summary:
		//     Gets or sets the index of the image that is used to indicate the state of the
		//     TreeNode when the parent TreeView has
		//     its TreeView.CheckBoxes property set to false.
		//
		// Returns:
		//     The index of the image that is used to indicate the state of the TreeNode.
		//
		// Exceptions:
		//   T:System.ArgumentOutOfRangeException:
		//     The specified index is less than -1 or greater than 14.
		public ImageBuffer StateImage { get; set; }

		//
		// Summary:
		//     Gets or sets the object that contains data about the tree node.
		//
		// Returns:
		//     An System.Object that contains data about the tree node. The default is null.
		public object Tag { get; set; }

		public Color TextColor { get; set; }

		//
		// Summary:
		//     Gets the parent tree view that the tree node is assigned to.
		//
		// Returns:
		//     A TreeView that represents the parent tree view that the
		//     tree node is assigned to, or null if the node has not been assigned to a tree
		//     view.

		public virtual TreeView TreeView
		{
			get => _treeView ?? NodeParent.TreeView;
			set => _treeView = value;
		}

		private void OnImageChanged(EventArgs args)
		{
			ImageChanged?.Invoke(this, null);
		}

		#endregion Properties

		#region Events

		public event EventHandler CheckedStateChanged;

		public event EventHandler ExpandedChanged;

		public event EventHandler ImageChanged;

		#endregion Events

		private class TreeExpandWidget : FlowLayoutWidget
		{
			private ImageBuffer arrowRight;
			private ImageBuffer arrowDown;
			private ImageBuffer placeholder;
			private IconButton imageButton = null;

			public TreeExpandWidget(ThemeConfig theme)
			{
				arrowRight = AggContext.StaticData.LoadIcon("fa-angle-right_12.png", theme.InvertIcons);
				arrowDown = AggContext.StaticData.LoadIcon("fa-angle-down_12.png", theme.InvertIcons);
				placeholder = new ImageBuffer(16, 16);

				this.Margin = new BorderDouble(right: 4);

				imageButton = new IconButton(placeholder, theme)
				{
					MinimumSize = new Vector2(16, 16),
					VAnchor = VAnchor.Center,
					Selectable = false,
					Width = 16,
					Height = 16
				};

				this.AddChild(imageButton);
			}

			private bool _alwaysExpandable;
			public bool AlwaysExpandable
			{
				get => _alwaysExpandable;
				set
				{
					imageButton.SetIcon((_expanded) ? arrowDown : arrowRight);
					_alwaysExpandable = value;
				}
			}

			private bool? _expandable = null;
			public bool Expandable
			{
				get => _expandable == true || this.AlwaysExpandable;
				set
				{
					if (_expandable != value)
					{
						_expandable = value;
					}

					this.EnsureExpansionState();
				}
			}

			private bool _expanded;
			public bool Expanded
			{
				get => _expanded;
				set
				{
					if (_expanded != value)
					{
						_expanded = value;

						this.EnsureExpansionState();
					}
				}
			}

			private void EnsureExpansionState()
			{
				if (!this.Expandable)
				{
					if (this.ReserveIconSpace)
					{
						imageButton.SetIcon(placeholder);
					}

					imageButton.Visible = this.ReserveIconSpace;
				}
				else
				{
					imageButton.Visible = true;
					imageButton.SetIcon((_expanded) ? arrowDown : arrowRight);
				}
			}

			public bool ReserveIconSpace { get; set; } = true;
		}
	}
}
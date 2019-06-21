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
using System.Collections;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class TreeView : ScrollableWidget
	{
		protected ThemeConfig theme;

		public TreeView(ThemeConfig theme)
			: this(0, 0, theme)
		{
		}

		public TreeView(int width, int height, ThemeConfig theme)
			: base(width, height)
		{
			this.theme = theme;
			this.AutoScroll = true;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
		}

		#region Events

		public event EventHandler AfterCheck;

		public event EventHandler AfterCollapse;

		public event EventHandler AfterExpand;

		public event EventHandler AfterLabelEdit;

		public event EventHandler<TreeNode> AfterSelect;

		public event EventHandler BeforeCheck;

		internal void NotifyItemClicked(GuiWidget sourceWidget, MouseEventArgs e)
		{
			this.NodeMouseClick?.Invoke(sourceWidget, e);
		}

		internal void NotifyItemDoubleClicked(GuiWidget sourceWidget, MouseEventArgs e)
		{
			this.NodeMouseDoubleClick?.Invoke(sourceWidget, e);
		}

		public event EventHandler BeforeCollapse;

		public event EventHandler BeforeExpand;

		public event EventHandler BeforeLabelEdit;

		public event EventHandler<TreeNode> BeforeSelect;

		public event EventHandler NodeMouseClick;

		public event EventHandler NodeMouseDoubleClick;

		public event EventHandler NodeMouseHover;

		#endregion Events

		#region Properties

		// Summary:
		//     Gets or sets a value indicating whether check boxes are displayed next to the
		//     tree nodes in the tree view control.
		//
		// Returns:
		//     true if a check box is displayed next to each tree node in the tree view control;
		//     otherwise, false. The default is false.
		public bool CheckBoxes { get; set; }

		// Summary:
		//     Gets or sets a value indicating whether the selection highlight spans the width
		//     of the tree view control.
		//
		// Returns:
		//     true if the selection highlight spans the width of the tree view control; otherwise,
		//     false. The default is false.
		public bool FullRowSelect { get; set; }

		// Summary:
		//     Gets or sets a value indicating whether the selected tree node remains highlighted
		//     even when the tree view has lost the focus.
		//
		// Returns:
		//     true if the selected tree node is not highlighted when the tree view has lost
		//     the focus; otherwise, false. The default is true.
		public bool HideSelection { get; set; }

		// Summary:
		//     Gets or sets the distance to indent each child tree node level.
		//
		// Returns:
		//     The distance, in pixels, to indent each child tree node level. The default value
		//     is 19.
		//
		// Exceptions:
		//   T:System.ArgumentOutOfRangeException:
		//     The assigned value is less than 0 (see Remarks).-or- The assigned value is greater
		//     than 32,000.
		public int Indent { get; set; }

		// Summary:
		//     Gets or sets the height of each tree node in the tree view control.
		//
		// Returns:
		//     The height, in pixels, of each tree node in the tree view.
		//
		// Exceptions:
		//   T:System.ArgumentOutOfRangeException:
		//     The assigned value is less than one.-or- The assigned value is greater than the
		//     System.Int16.MaxValue value.
		public int ItemHeight { get; set; }

		// Summary:
		//     Gets or sets a value indicating whether the label text of the tree nodes can
		//     be edited.
		//
		// Returns:
		//     true if the label text of the tree nodes can be edited; otherwise, false. The
		//     default is false.
		public bool LabelEdit { get; set; }

		// Summary:
		//     Gets or sets the color of the lines connecting the nodes of the TreeView
		//     control.
		//
		// Returns:
		//     The System.Drawing.Color of the lines connecting the tree nodes.
		public Color LineColor { get; set; }

		// Summary:
		//     Gets or sets the delimiter string that the tree node path uses.
		//
		// Returns:
		//     The delimiter string that the tree node TreeNode.FullPath
		//     property uses. The default is the backslash character (\).
		public string PathSeparator { get; set; }

		public Color TextColor { get; set; } = Color.Black;

		public double PointSize { get; set; } = 12;

		// Summary:
		//     Gets or sets a value indicating whether the tree view control displays scroll
		//     bars when they are needed.
		//
		// Returns:
		//     true if the tree view control displays scroll bars when they are needed; otherwise,
		//     false. The default is true.
		public bool Scrollable { get; set; }

		// Summary:
		//     Gets or sets the tree node that is currently selected in the tree view control.
		//
		// Returns:
		//     The TreeNode that is currently selected in the tree view
		//     control.
		private TreeNode _selectedNode;

		public void Clear()
		{
			this.ScrollArea.CloseAllChildren();

			// Release held reference
			_selectedNode = null;
		}

		public TreeNode SelectedNode
		{
			get => _selectedNode;
			set
			{
				if (value != _selectedNode)
				{
					OnBeforeSelect(null);

					// if the current selection (before change) is !null than clear its background color
					if (_selectedNode != null)
					{
						_selectedNode.HighlightRegion.BackgroundColor = Color.Transparent;
					}

					// change the selection
					_selectedNode = value;

					if (_selectedNode != null)
					{
						// Ensure tree is expanded, walk backwards to the root, reverse, expand back to this node
						foreach (var ancestor in _selectedNode.Ancestors().Reverse())
						{
							ancestor.Expanded = true;
						}
					}

					if (_selectedNode != null)
					{
						_selectedNode.HighlightRegion.BackgroundColor = theme.AccentMimimalOverlay;
					}

					this.ScrollIntoView(_selectedNode);

					OnAfterSelect(null);
				}
			}
		}

		public bool ShowLines { get; set; }

		public bool ShowNodeToolTips { get; set; }

		public bool ShowPlusMinus { get; set; }

		public bool ShowRootLines { get; set; }

		public bool Sorted { get; set; }

		public IComparer TreeViewNodeSorter { get; set; }

		// Summary:
		//     Gets the number of tree nodes that can be fully visible in the tree view control.
		//
		// Returns:
		//     The number of TreeNode items that can be fully visible in
		//     the TreeView control.
		public int VisibleCount { get; }

		#endregion Properties

		// Summary:
		//     Disables any redrawing of the tree view.
		public void BeginUpdate()
		{
			throw new NotImplementedException();
		}

		// Summary:
		//     Collapses all the tree nodes.
		public void CollapseAll()
		{
			throw new NotImplementedException();
		}

		// Summary:
		//     Enables the redrawing of the tree view.
		public void EndUpdate()
		{
			throw new NotImplementedException();
		}

		// Summary:
		//     Expands all the tree nodes.
		public void ExpandAll()
		{
			throw new NotImplementedException();
		}

		// Summary:
		//     Retrieves the tree node that is at the specified point.
		//
		// Parameters:
		//   pt:
		//     The System.Drawing.Point to evaluate and retrieve the node from.
		//
		// Returns:
		//     The TreeNode at the specified point, in tree view (client)
		//     coordinates, or null if there is no node at that location.
		public TreeNode GetNodeAt(Vector2 pt)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Retrieves the number of tree nodes, optionally including those in all subtrees,
		/// assigned to the tree view control.
		/// </summary>
		/// <param name="includeSubTrees">true to count the TreeNode items that the subtrees contain;
		/// otherwise, false.</param>
		/// <returns>The number of tree nodes, optionally including those in all subtrees, assigned
		/// to the tree view control.</returns>
		public int GetNodeCount(bool includeSubTrees)
		{
			throw new NotImplementedException();
		}

		public void Sort()
		{
			throw new NotImplementedException();
		}

		protected internal virtual void OnAfterCollapse(EventArgs e)
		{
			throw new NotImplementedException();
		}

		protected internal virtual void OnBeforeCollapse(EventArgs e)
		{
			throw new NotImplementedException();
		}

		protected virtual void OnAfterCheck(EventArgs e)
		{
			throw new NotImplementedException();
		}

		protected virtual void OnAfterExpand(EventArgs e)
		{
			throw new NotImplementedException();
		}

		protected virtual void OnAfterLabelEdit(EventArgs e)
		{
			throw new NotImplementedException();
		}

		protected virtual void OnAfterSelect(TreeNode e)
		{
			AfterSelect?.Invoke(this, e);
		}

		protected virtual void OnBeforeCheck(EventArgs e)
		{
			throw new NotImplementedException();
		}

		protected virtual void OnBeforeExpand(EventArgs e)
		{
			throw new NotImplementedException();
		}

		protected virtual void OnBeforeLabelEdit(EventArgs e)
		{
			throw new NotImplementedException();
		}

		protected virtual void OnBeforeSelect(TreeNode e)
		{
			BeforeSelect?.Invoke(this, e);
		}

		protected virtual void OnNodeMouseClick(EventArgs e)
		{
			throw new NotImplementedException();
		}

		protected virtual void OnNodeMouseDoubleClick(EventArgs e)
		{
			throw new NotImplementedException();
		}
	}
}
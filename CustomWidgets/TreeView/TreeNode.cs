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
using System.Collections.Specialized;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.CustomWidgets.TreeView
{
	public class TreeNode : FlowLayoutWidget, ICheckbox
	{
		private GuiWidget content;

		public TreeNode()
			: base(FlowDirection.TopToBottom)
		{
			HAnchor = HAnchor.Fit | HAnchor.Left;
			VAnchor = VAnchor.Fit;

			TitleBar = new FlowLayoutWidget();
			TitleBar.Click += (s, e) =>
			{
				if (TreeView != null)
				{
					TreeView.SelectedNode = this;
				}
			};
			AddChild(TitleBar);
			RebuildTitleBar();

			content = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit | HAnchor.Left,
				Visible = false, // content starts out not visible
				Name = "content",
				Margin = new BorderDouble(25, 3),
			};
			AddChild(content);

			Nodes.CollectionChanged += Nodes_CollectionChanged;
		}

		public FlowLayoutWidget TitleBar { get; }

		public void BeginEdit()
		{
			throw new NotImplementedException();
		}

		public void Collapse(bool collapseChildren)
		{
			throw new NotImplementedException();
		}

		public void Collapse()
		{
			throw new NotImplementedException();
		}

		public void EndEdit(bool cancel)
		{
			throw new NotImplementedException();
		}

		public void EnsureVisible()
		{
			throw new NotImplementedException();
		}

		public void ExpandAll()
		{
			throw new NotImplementedException();
		}

		public int GetNodeCount(bool includeSubTrees)
		{
			if (includeSubTrees)
			{
				return this.Descendants<TreeNode>().Count();
			}

			return content.Children.Where((c) => c is TreeNode).Count();
		}

		public override void OnTextChanged(EventArgs e)
		{
			RebuildTitleBar();
			base.OnTextChanged(e);
		}

		public void Remove()
		{
			throw new NotImplementedException();
		}

		public void Toggle()
		{
			content.Visible = !content.Visible;
		}

		private void Nodes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			RebuildContentSection();
		}

		private void RebuildContentSection()
		{
			// If the node count is starting at 0 we are adding content and need to rebuild the title bar so it will have a + in it
			bool needToRebuildTitleBar = GetNodeCount(false) == 0;

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
			needToRebuildTitleBar |= GetNodeCount(false) == 0;
			if (needToRebuildTitleBar)
			{
				RebuildTitleBar();
			}
		}

		private void RebuildTitleBar()
		{
			TitleBar.RemoveAllChildren();
			if (content != null
				&& GetNodeCount(false) > 0)
			{
				// add a check box
				var expandCheckBox = new CheckBox("")
				{
					Checked = Expanded,
					VAnchor = VAnchor.Center
				};
				ExpandedChanged += (s, e) =>
				{
					expandCheckBox.Checked = Expanded;
				};
				expandCheckBox.CheckedStateChanged += (s, e) =>
				{
					Expanded = expandCheckBox.Checked;
				};
				TitleBar.AddChild(expandCheckBox);
			}
			// add a check box
			if (Image != null)
			{
				TitleBar.AddChild(new ImageWidget(Image)
				{
					VAnchor = VAnchor.Center,
					BackgroundColor = new Color(ActiveTheme.Instance.PrimaryTextColor, 12),
					Margin = 2,
				});
			};
			TitleBar.AddChild(new TextWidget(Text)
			{
				Selectable = false
			});
		}

		#region Properties

		private ImageBuffer _image = new ImageBuffer(16, 16);

		public bool Checked { get; set; }

		public bool Editing { get; }

		public bool Expanded
		{
			get
			{
				return content.Visible;
			}
			set
			{
				if (content.Visible != value)
				{
					content.Visible = value;
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
			get
			{
				return NodeParent.TreeView;
			}
		}

		private void OnImageChanged(EventArgs args)
		{
			ImageChanged?.Invoke(this, null);

			RebuildTitleBar();
		}

		#endregion Properties

		#region Events

		public event EventHandler CheckedStateChanged;

		public event EventHandler ExpandedChanged;

		public event EventHandler ImageChanged;

		#endregion Events
	}
}
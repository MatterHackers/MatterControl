using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public partial class InspectForm : Form
	{
		private TreeNode activeTreeNode;
		private GuiWidget inspectedWidget;
		private GuiWidget InspectedWidget
		{
			get => inspectedWidget;
			set
			{
				if (inspectedWidget != null)
				{
					inspectedWidget.DebugShowBounds = false;
				}

				inspectedWidget = value;
				inspectedWidget.DebugShowBounds = true;

				if (inspectedWidget != null)
				{
					propertyGrid1.SelectedObject = inspectedWidget;
				}

				if (activeTreeNode != null)
				{
					activeTreeNode.Checked = false;
				}

				if (treeNodes.TryGetValue(inspectedWidget, out TreeNode treeNode))
				{
					treeView1.SelectedNode = treeNode;
					activeTreeNode = treeNode;
					activeTreeNode.Checked = true;
				}

				inspectedWidget.Invalidate();
			}
		}

		bool showNamesUnderMouse = true;

		private GuiWidget inspectedSystemWindow;

		private Vector2 mousePosition;

		Dictionary<GuiWidget, TreeNode> treeNodes = new Dictionary<GuiWidget, TreeNode>();

		public InspectForm(GuiWidget inspectionSource)
		{
			InitializeComponent();

			inspectionSource.MouseMove += (s, e) =>
			{
				mousePosition = e.Position;
			};

			inspectionSource.AfterDraw += (s, e) =>
			{
				if (showNamesUnderMouse && !inspectionSource.HasBeenClosed)
				{
					var namedChildren = new List<GuiWidget.WidgetAndPosition>();
					inspectedSystemWindow.FindNamedChildrenRecursive(
						"",
						namedChildren,
						new RectangleDouble(mousePosition.x, mousePosition.y, mousePosition.x + 1, mousePosition.y + 1),
						GuiWidget.SearchType.Partial,
						allowDisabledOrHidden: false);

					// If the context changed, update the UI
					if (namedChildren.LastOrDefault()?.widget is GuiWidget firstUnderMouse
						&& firstUnderMouse != this.InspectedWidget)
					{
						RebuildUI(namedChildren);

						this.InspectedWidget = firstUnderMouse;
					}
				}
			};

			this.inspectedSystemWindow = inspectionSource;

			inspectionSource.Invalidate();
		}

		private void AddItem(GuiWidget widget, string text, TreeNode childNode = null)
		{
			if (treeNodes.TryGetValue(widget, out TreeNode existingNode))
			{
				existingNode.Nodes.Add(childNode);
				existingNode.Expand();
			}
			else
			{
				var node = new TreeNode(text)
				{
					Tag = widget
				};

				if (childNode != null)
				{
					node.Nodes.Add(childNode);
					node.Expand();
				}
				treeNodes.Add(widget, node);

				var parent = widget.Parent;
				if (parent == null)
				{
					treeView1.Nodes.Add(node);
				}
				else
				{
					AddItem(parent, parent.Text, node);
				}
			}
		}

		public void RebuildUI(List<GuiWidget.WidgetAndPosition> namedChildren)
		{
			treeView1.Nodes.Clear();
			treeNodes.Clear();

			treeView1.SuspendLayout();

			for (int i = 0; i < namedChildren.Count; i++)
			{
				var child = namedChildren[i];
				AddItem(child.widget, BuildName(child.widget));
			}

			treeView1.ResumeLayout();
		}

		private string BuildName(GuiWidget widget)
		{
			string nameToWrite = inspectedWidget == widget ? "* " : "";
			if (!string.IsNullOrEmpty(widget.Name))
			{
				nameToWrite += $"{widget.GetType().Name} --- {widget.Name}";
			}
			else
			{
				nameToWrite += $"{widget.GetType().Name}";
			}

			return nameToWrite;
		}

		int selectionIndex;

		protected override void OnKeyDown(System.Windows.Forms.KeyEventArgs e)
		{
			if (e.KeyCode ==  System.Windows.Forms.Keys.F2)
			{
				selectionIndex++;
			}
			else if (e.KeyCode == System.Windows.Forms.Keys.F2)
			{
				selectionIndex--;
			}

			base.OnKeyDown(e);
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			this.InspectedWidget = e.Node.Tag as GuiWidget;
		}
	}
}

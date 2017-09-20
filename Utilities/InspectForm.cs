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
		private GuiWidget inspectedSystemWindow;

		private Vector2 mousePosition;

		private Dictionary<GuiWidget, TreeNode> treeNodes = new Dictionary<GuiWidget, TreeNode>();

		public InspectForm(GuiWidget inspectedSystemWindow)
		{
			InitializeComponent();

			this.inspectedSystemWindow = inspectedSystemWindow;

			// Store position on move, invalidate in needed
			inspectedSystemWindow.MouseMove += systemWindow_MouseMove;
			inspectedSystemWindow.AfterDraw += systemWindow_AfterDraw;
			inspectedSystemWindow.Invalidate();
		}

		public bool Inspecting { get; set; } = true;

		private GuiWidget mouseUpWidget;

		protected override bool ShowWithoutActivation => true;

		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams baseParams = base.CreateParams;

				const int WS_EX_NOACTIVATE = 0x08000000;
				const int WS_EX_TOOLWINDOW = 0x00000080;
				baseParams.ExStyle |= (int)(WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

				return baseParams;
			}
		}

		private GuiWidget _inspectedWidget;
		private GuiWidget InspectedWidget
		{
			get => _inspectedWidget;
			set
			{
				if (_inspectedWidget == value)
				{
					return;
				}

				if (_inspectedWidget != null)
				{
					_inspectedWidget.DebugShowBounds = false;
				}

				if (mouseUpWidget != null)
				{
					mouseUpWidget.MouseUp -= inspectedWidget_MouseUp;
				}

				_inspectedWidget = value;

				if (_inspectedWidget != null)
				{
					propertyGrid1.SelectedObject = _inspectedWidget;

					_inspectedWidget.DebugShowBounds = true;

					var context = _inspectedWidget;
					while(!context.CanSelect && context.Parent != null)
					{
						context = context.Parent;
					}

					if (context.CanSelect)
					{
						// Hook to stop listing on click
						mouseUpWidget = context;
						mouseUpWidget.MouseUp += inspectedWidget_MouseUp;
					}
				}

				if (activeTreeNode != null)
				{
					activeTreeNode.Checked = false;
				}

				if (treeNodes.TryGetValue(_inspectedWidget, out TreeNode treeNode))
				{
					treeView1.SelectedNode = treeNode;
					treeNode.EnsureVisible();
					activeTreeNode = treeNode;
					activeTreeNode.Checked = true;
				}

				_inspectedWidget.Invalidate();
			}
		}

		private void inspectedWidget_MouseUp(object sender, Agg.UI.MouseEventArgs e)
		{
			// Stop listing on click
			this.Inspecting = false;
		}

		private void AddItem(GuiWidget widget, string text = null, TreeNode childNode = null)
		{

			if (text == null)
			{
				text = BuildName(widget);
			}

			if (treeNodes.TryGetValue(widget, out TreeNode existingNode))
			{
				if (childNode != null)
				{
					existingNode.Nodes.Add(childNode);
				}
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
				AddItem(child.widget);
			}

			treeView1.ResumeLayout();
		}

		private string BuildName(GuiWidget widget)
		{
			string nameToWrite = _inspectedWidget == widget ? "* " : "";
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

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			this.InspectedWidget = e.Node.Tag as GuiWidget;
		}

		private void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
		{
			this.InspectedWidget.Invalidate();
		}

		public void MoveUpTree()
		{
			if (activeTreeNode?.Parent is TreeNode parent)
			{
				this.InspectedWidget = parent.Tag as GuiWidget;
			}
		}

		public void MoveDownTree()
		{
			if (activeTreeNode?.Nodes.Cast<TreeNode>().FirstOrDefault() is TreeNode firstChild)
			{
				this.InspectedWidget = firstChild.Tag as GuiWidget;
			}
		}

		private void systemWindow_MouseMove(object sender, Agg.UI.MouseEventArgs e)
		{
			mousePosition = e.Position;

			if (this.InspectedWidget?.FirstWidgetUnderMouse == false)
			{
				this.inspectedSystemWindow.Invalidate();
			}
		}

		private void systemWindow_AfterDraw(object sender, EventArgs e)
		{
			if (this.Inspecting && !inspectedSystemWindow.HasBeenClosed)
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
		}

		private void btnAddSiblings_Click(object sender, EventArgs e)
		{
			AddAllItems(this.InspectedWidget?.Parent?.Children);
		}

		private void btnAddChildren_Click(object sender, EventArgs e)
		{
			AddAllItems(this.InspectedWidget?.Children);
		}

		private void AddAllItems(IEnumerable<GuiWidget> items)
		{
			if (items != null)
			{
				foreach (var item in items)
				{
					this.AddItem(item);
				}
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			inspectedSystemWindow.AfterDraw -= systemWindow_AfterDraw;
			inspectedSystemWindow.MouseMove -= systemWindow_MouseMove;

			if (mouseUpWidget != null)
			{
				mouseUpWidget.MouseUp -= inspectedWidget_MouseUp;
			}

			base.OnFormClosing(e);
		}

		private void treeView1_DrawNode(object sender, DrawTreeNodeEventArgs e)
		{
			e.DrawDefault = true;
		}
	}
}

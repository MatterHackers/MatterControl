using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public partial class InspectForm : WinformsSystemWindow.FormInspector
	{
		private TreeNode activeTreeNode;
		private GuiWidget inspectedSystemWindow;

		private Vector2 mousePosition;

		private Dictionary<GuiWidget, TreeNode> aggTreeNodes = new Dictionary<GuiWidget, TreeNode>();
		private Dictionary<IObject3D, TreeNode> sceneTreeNodes = new Dictionary<IObject3D, TreeNode>();

		private InteractiveScene scene;
		private View3DWidget view3DWidget;

		public InspectForm(GuiWidget inspectedSystemWindow, InteractiveScene scene, View3DWidget view3DWidget)
			: this(inspectedSystemWindow)
		{
			this.view3DWidget = view3DWidget;
			this.scene = scene;
			this.scene.Children.ItemsModified += Scene_ChildrenModified;
			sceneTreeView.SuspendLayout();
			this.AddTree(scene, null, "Scene");
			sceneTreeView.ResumeLayout();

			if (view3DWidget.ContainsFocus)
			{
				tabControl1.SelectedIndex = 1;
			}
		}

		private void Scene_ChildrenModified(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				sceneTreeView.SuspendLayout();
				sceneTreeView.Nodes.Clear();
				sceneTreeNodes.Clear();

				this.AddTree(scene, null, "Scene");
				sceneTreeView.ResumeLayout();
			});
		}

		public InspectForm(GuiWidget inspectedSystemWindow)
		{
			InitializeComponent();

			this.inspectedSystemWindow = inspectedSystemWindow;

			// Store position on move, invalidate in needed
			inspectedSystemWindow.MouseMove += systemWindow_MouseMove;
			inspectedSystemWindow.AfterDraw += systemWindow_AfterDraw;
			inspectedSystemWindow.Invalidate();

			aggTreeView.SuspendLayout();
			this.AddTree(inspectedSystemWindow, null, "SystemWindow");
			aggTreeView.ResumeLayout();

			this.TopMost = true;
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
				baseParams.ExStyle |= (int)(WS_EX_NOACTIVATE); // | WS_EX_TOOLWINDOW);

				return baseParams;
			}
		}

		private HashSet<GuiWidget> aggAncestryTree = new HashSet<GuiWidget>();
		//private HashSet<IObject3D> sceneAncestryTree = new HashSet<IObject3D>();

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

				this.Text = "Inspector" + (string.IsNullOrEmpty(_inspectedWidget?.Name) ? "" : " - " + _inspectedWidget.Name);

				if (_inspectedWidget != null)
				{
					aggAncestryTree = new HashSet<GuiWidget>(_inspectedWidget.Parents<GuiWidget>());
					aggAncestryTree.Add(_inspectedWidget);

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

				if (aggTreeNodes.TryGetValue(_inspectedWidget, out TreeNode treeNode))
				{
					aggTreeView.SelectedNode = treeNode;

					treeNode.EnsureVisible();
					activeTreeNode = treeNode;
					aggTreeView.Invalidate();
				}

				_inspectedWidget.Invalidate();
			}

		}
		private IObject3D _inspectedObject3D = null;
		public IObject3D InspectedObject3D
		{
			get => _inspectedObject3D;
			set
			{
				if (_inspectedObject3D != value)
				{
					_inspectedObject3D = value;

					if (_inspectedObject3D != null)
					{
						propertyGrid1.SelectedObject = _inspectedObject3D;

						//sceneAncestryTree = new HashSet<IObject3D>();
					}
				}
			}
		}

		private Font boldFont;

		private void inspectedWidget_MouseUp(object sender, Agg.UI.MouseEventArgs e)
		{
			// Stop listing on click
			this.Inspecting = false;
		}

		private void AddItem(GuiWidget widget, string text = null, TreeNode childNode = null, bool showAllParents = true)
		{
			if (text == null)
			{
				text = BuildDefaultName(widget);
			}

			if (aggTreeNodes.TryGetValue(widget, out TreeNode existingNode))
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

				if (boldFont == null)
				{
					boldFont = new Font(node.NodeFont, FontStyle.Bold);
				}

				if (childNode != null)
				{
					node.Nodes.Add(childNode);
					node.Expand();
				}
				aggTreeNodes.Add(widget, node);

				if (showAllParents)
				{
					var parent = widget.Parent;
					if (parent == null)
					{
						aggTreeView.Nodes.Add(node);
					}
					else
					{
						AddItem(parent, parent.Text, node);
					}
				}
				else
				{
					aggTreeView.Nodes.Add(node);
				}
			}
		}

		private TreeNode AddItem(GuiWidget widget, TreeNode parentNode, string overrideText = null)
		{
			var node = new TreeNode(overrideText ?? BuildDefaultName(widget))
			{
				Tag = widget
			};
			aggTreeNodes.Add(widget, node);

			if (parentNode == null)
			{
				aggTreeView.Nodes.Add(node);
			}
			else
			{
				parentNode.Nodes.Add(node);
			}

			node.Expand();

			return node;
		}

		private TreeNode AddItem(IObject3D item, TreeNode parentNode, string overrideText = null)
		{
			var node = new TreeNode(overrideText ?? BuildDefaultName(item))
			{
				Tag = item
			};
			sceneTreeNodes.Add(item, node);


			if (parentNode == null)
			{
				sceneTreeView.Nodes.Add(node);
				node.Expand();

			}
			else
			{
				parentNode.Nodes.Add(node);
				parentNode.Expand();
			}



			return node;
		}

		private void AddTree(GuiWidget widget, TreeNode parent, string text = null, TreeNode childNode = null)
		{
			var node = AddItem(widget, parent);

			foreach(var child in widget.Children)
			{
				AddTree(child, node);
			}
		}

		private void AddTree(IObject3D item, TreeNode parent, string text = null, TreeNode childNode = null)
		{
			var node = AddItem(item, parent);

			foreach (var child in item.Children)
			{
				AddTree(child, node);
			}
		}

		private string BuildDefaultName(GuiWidget widget)
		{
			string nameToWrite = "";
			if (!string.IsNullOrEmpty(widget.Name))
			{
				nameToWrite += $"{widget.GetType().Name} - {widget.Name}";
			}
			else
			{
				nameToWrite += $"{widget.GetType().Name}";
			}

			return nameToWrite;
		}

		private string BuildDefaultName(IObject3D item)
		{
			string nameToWrite = "";
			if (!string.IsNullOrEmpty(item.Name))
			{
				nameToWrite += $"{item.GetType().Name} - {item.Name}";
			}
			else
			{
				nameToWrite += $"{item.GetType().Name}";
			}

			return nameToWrite;
		}

		private void AggTreeView_AfterSelect(object sender, TreeViewEventArgs e)
		{
			this.InspectedWidget = e.Node.Tag as GuiWidget;
		}

		private void SceneTreeView_AfterSelect(object sender, TreeViewEventArgs e)
		{
			this.InspectedObject3D = e.Node.Tag as IObject3D;
			this.scene.DebugItem = this.InspectedObject3D;
			view3DWidget.PartHasBeenChanged();
		}

		private void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
		{
			this.InspectedWidget?.Invalidate();
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
			if (this.Inspecting 
				&& !inspectedSystemWindow.HasBeenClosed
				&& tabControl1.SelectedIndex == 0)
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
					this.InspectedWidget = firstUnderMouse;
				}
			}
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

			if (scene != null)
			{
				scene.Children.ItemsModified -= Scene_ChildrenModified;
				scene.DebugItem = null;
			}

			if (mouseUpWidget != null)
			{
				mouseUpWidget.MouseUp -= inspectedWidget_MouseUp;
			}

			base.OnFormClosing(e);
		}

		private void AggTreeView_DrawNode(object sender, DrawTreeNodeEventArgs e)
		{
			var node = e.Node;

			if (node.IsVisible)
			{
				var widget = node.Tag as GuiWidget;
				Brush brush;
				if (node == activeTreeNode)
				{
					brush = SystemBrushes.Highlight;
				}
				else if (aggAncestryTree.Contains(widget))
				{
					brush = Brushes.LightBlue;
				}
				else
				{
					brush = Brushes.Transparent;
				}
				
				e.Graphics.FillRectangle(brush, e.Node.Bounds);

				TextRenderer.DrawText(
					e.Graphics,
					node.Text,
					node == activeTreeNode ? boldFont : node.NodeFont,
					new Point(node.Bounds.Left, node.Bounds.Top),
					widget.ActuallyVisibleOnScreen() ? SystemColors.ControlText : SystemColors.GrayText,
					Color.Transparent);
			}
		}

		private void SceneTreeView_DrawNode(object sender, DrawTreeNodeEventArgs e)
		{
			var node = e.Node;
			if (node.IsVisible)
			{
				//var item = node.Tag as IObject3D;
				e.Graphics.FillRectangle(
					(sceneTreeView.SelectedNode == node) ? SystemBrushes.Highlight : Brushes.Transparent, 
					node.Bounds);

				TextRenderer.DrawText(
					e.Graphics,
					node.Text,
					node == activeTreeNode ? boldFont : node.NodeFont,
					new Point(node.Bounds.Left, node.Bounds.Top),
					SystemColors.ControlText,
					Color.Transparent);
			}
		}

		private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (this.activeTreeNode != null
				&& tabControl1.SelectedIndex != 0
				&& this.activeTreeNode.Tag is GuiWidget widget)
			{
				widget.DebugShowBounds = false;
			}

			if (scene.DebugItem != null
				&& tabControl1.SelectedIndex != 1)
			{
				scene.DebugItem = null;
			}
		}
	}
}

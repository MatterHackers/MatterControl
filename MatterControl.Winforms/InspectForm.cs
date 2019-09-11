using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public partial class InspectForm : WinformsSystemWindow.FormInspector
	{
		private System.Windows.Forms.TreeNode activeTreeNode;
		private GuiWidget inspectedSystemWindow;

		private Vector2 mousePosition;

		private Dictionary<GuiWidget, System.Windows.Forms.TreeNode> aggTreeNodes = new Dictionary<GuiWidget, System.Windows.Forms.TreeNode>();
		private Dictionary<IObject3D, System.Windows.Forms.TreeNode> sceneTreeNodes = new Dictionary<IObject3D, System.Windows.Forms.TreeNode>();

		private InteractiveScene scene;
		private View3DWidget view3DWidget;

		public InspectForm(GuiWidget inspectedSystemWindow, InteractiveScene scene, View3DWidget view3DWidget)
			: this(inspectedSystemWindow)
		{
			this.view3DWidget = view3DWidget;
			this.scene = scene;
			if (scene != null)
			{
				this.scene.Children.ItemsModified += Scene_ChildrenModified;
				sceneTreeView.SuspendLayout();
				this.AddTree(scene, null);

				sceneTreeView.ResumeLayout();
			}
		}

		private void Scene_ChildrenModified(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				sceneTreeView.SuspendLayout();
				sceneTreeView.Nodes.Clear();
				sceneTreeNodes.Clear();

				this.AddTree(scene, null);
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
			this.AddTree(inspectedSystemWindow, null);
			aggTreeView.ResumeLayout();

			this.TopMost = true;
		}

		protected override bool ShowWithoutActivation => true;

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
				}

				if (activeTreeNode != null)
				{
					activeTreeNode.Checked = false;
				}

				if (aggTreeNodes.TryGetValue(_inspectedWidget, out System.Windows.Forms.TreeNode treeNode))
				{
					aggTreeView.SelectedNode = treeNode;

					treeNode.EnsureVisible();
					activeTreeNode = treeNode;
					aggTreeView.Invalidate();
				}
				else
				{
					this.AddItemEnsureAncestors(_inspectedWidget);
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

		private void AddItemEnsureAncestors(GuiWidget widget, string text = null, System.Windows.Forms.TreeNode childNode = null, bool showAllParents = true)
		{
			if (text == null)
			{
				text = BuildDefaultName(widget);
			}

			if (aggTreeNodes.TryGetValue(widget, out System.Windows.Forms.TreeNode existingNode))
			{
				if (childNode != null)
				{
					existingNode.Nodes.Add(childNode);
				}
				existingNode.Expand();
			}
			else
			{
				var node = new System.Windows.Forms.TreeNode(text)
				{
					Tag = widget
				};

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
						AddItemEnsureAncestors(parent, null, node);
					}
				}
				else
				{
					aggTreeView.Nodes.Add(node);
				}
			}
		}

		private TreeNode AddItem(GuiWidget widget, System.Windows.Forms.TreeNode parentNode)
		{
			var node = new System.Windows.Forms.TreeNode(BuildDefaultName(widget))
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

		private TreeNode AddItem(IObject3D item, System.Windows.Forms.TreeNode parentNode)
		{
			var node = new System.Windows.Forms.TreeNode(BuildDefaultName(item))
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

		private void AddTree(GuiWidget widget, System.Windows.Forms.TreeNode parent)
		{
			var node = AddItem(widget, parent);

			foreach(var child in widget.Children)
			{
				AddTree(child, node);
			}
		}

		private void AddTree(IObject3D item, System.Windows.Forms.TreeNode parent)
		{
			var node = AddItem(item, parent);

			foreach (var child in item.Children)
			{
				AddTree(child, node);
			}
		}

		private string BuildDefaultName(GuiWidget widget)
		{
			var type = widget.GetType();
			string baseType = type == typeof(GuiWidget) || type.BaseType == typeof(GuiWidget) ? "" : $":{type.BaseType.Name}";
			string controlName = string.IsNullOrEmpty(widget.Name) ? "" : $" - '{widget.Name}'";

			return $"{type.Name}{baseType}{controlName}";
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

			var selector = string.Join(".", this.InspectedObject3D.AncestorsAndSelf().Select(o => $"Children<{o.GetType().Name.ToString()}>").Reverse().ToArray());
			Console.WriteLine(selector);

			view3DWidget.Invalidate();
		}

		private void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
		{
			this.InspectedWidget?.Invalidate();
		}

		public void MoveUpTree()
		{
			if (activeTreeNode?.Parent is System.Windows.Forms.TreeNode parent)
			{
				this.InspectedWidget = parent.Tag as GuiWidget;
			}
		}

		public void MoveDownTree()
		{
			if (activeTreeNode?.Nodes.Cast<System.Windows.Forms.TreeNode>().FirstOrDefault() is System.Windows.Forms.TreeNode firstChild)
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
				inspectedSystemWindow.FindDescendants(
					new[] { "" },
					namedChildren,
					new RectangleDouble(mousePosition.X, mousePosition.Y, mousePosition.X + 1, mousePosition.Y + 1),
					GuiWidget.SearchType.Partial,
					allowDisabledOrHidden: false);

				// If the context changed, update the UI
				if (namedChildren.LastOrDefault()?.Widget is GuiWidget firstUnderMouse
					&& firstUnderMouse != this.InspectedWidget)
				{
					this.InspectedWidget = firstUnderMouse;
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
					node.NodeFont,
					new Point(node.Bounds.Left, node.Bounds.Top),
					widget.ActuallyVisibleOnScreen() ? SystemColors.ControlText : SystemColors.GrayText,
					System.Drawing.Color.Transparent);
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
					node.NodeFont,
					new Point(node.Bounds.Left, node.Bounds.Top),
					SystemColors.ControlText,
					System.Drawing.Color.Transparent);
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

			if (scene != null
				&& scene.DebugItem != null
				&& tabControl1.SelectedIndex != 1)
			{
				scene.DebugItem = null;
			}

		}

		private void debugTextWidget_CheckedChanged(object sender, EventArgs e)
		{
			TextWidget.DebugShowSize = debugTextWidget.Checked;

			foreach(var widget in this.inspectedSystemWindow.Descendants<TextWidget>())
			{
				widget.Invalidate();
			}
		}

		protected override void OnKeyUp(System.Windows.Forms.KeyEventArgs e)
		{
			if (e.KeyCode == System.Windows.Forms.Keys.F12)
			{
				this.Inspecting = !this.Inspecting;
			}

			base.OnKeyUp(e);
		}

		private void InspectForm_Load(object sender, EventArgs e1)
		{
			var rootNode = new TreeNode("Theme");

			var themeNode = new TreeNode("Theme");

			var menuThemeNode = new TreeNode("MenuTheme");

			rootNode.Nodes.Add(themeNode);
			rootNode.Nodes.Add(menuThemeNode);

			themeTreeView.Nodes.Add(rootNode);

			rootNode.Expand();

			themeTreeView.AfterSelect += (s, e) =>
			{
				if (e.Node == rootNode)
				{
					propertyGrid1.SelectedObject = MatterControl.AppContext.ThemeSet;
				}
				else if (e.Node == themeNode)
				{
					propertyGrid1.SelectedObject = MatterControl.AppContext.Theme;

				}
				else if (e.Node == menuThemeNode)
				{
					propertyGrid1.SelectedObject = MatterControl.AppContext.MenuTheme;
				}
			};
		}

		private void btnApply_Click(object sender, EventArgs e)
		{
			ApplicationController.Instance.ReloadAll().ConfigureAwait(false);
		}

		private void button1_Click(object sender, EventArgs e)
		{
			var context = ApplicationController.Instance.ActivePrinters.First().Connection.TotalGCodeStream;
			textBox1.Text = context.GetDebugState();
		}

		private void DebugMenus_CheckedChanged(object sender, EventArgs e)
		{
			PopupWidget.DebugKeepOpen = debugMenus.Checked;
		}
	}
}

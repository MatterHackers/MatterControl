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

using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.CustomWidgets.TreeView;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public static class Object3DTreeBuilder
	{
		public static TreeView GetPartTreeView(IObject3D rootItem, ThemeConfig theme)
		{
			var topNode = new TopNode()
			{
				Text = BuildDefaultName(rootItem),
				Tag = rootItem,
				TextColor = theme.Colors.PrimaryTextColor,
				PointSize = theme.DefaultFontSize
			};

			var treeView = new TreeView(topNode, theme)
			{
				TextColor = theme.Colors.PrimaryTextColor,
				PointSize = theme.DefaultFontSize
			};

			treeView.SuspendLayout();

			//selectionTreeNodes.Clear();
			//selectionTreeNodes.Add(rootItem, topNode);

			// add the children to the root node
			foreach (var child in rootItem.Children)
			{
				AddTree(child, topNode, theme);
			}

			treeView.ResumeLayout();

			return treeView;
		}

		private static void AddTree(IObject3D item, TreeNode parent, ThemeConfig theme)
		{
			// Suppress MeshWrapper nodes in treeview - retain parent node as context reference
			var contextNode = (item is MeshWrapper) ? parent : AddItem(item, parent, theme);

			foreach (var child in item.Children)
			{
				AddTree(child, contextNode, theme);
			}
		}

		private static TreeNode AddItem(IObject3D item, TreeNode parentNode, ThemeConfig theme)
		{
			var node = new TreeNode()
			{
				Text = BuildDefaultName(item),
				Tag = item,
				TextColor = theme.Colors.PrimaryTextColor,
				PointSize = theme.DefaultFontSize,
			};

			// Check for operation resulting in the given type
			if (ApplicationController.Instance.OperationsByType.TryGetValue(item.GetType(), out SceneSelectionOperation operation))
			{
				// If exists, use the operation icon
				node.Image = operation.Icon;
			}
			else
			{
				// Otherwise wire up icon generation
				var inmemoryItem = new InMemoryLibraryItem(item.Clone());
				var iconView = new IconViewItem(new ListViewItem(inmemoryItem, ApplicationController.Instance.Library.PlatingHistory), 16, 16, theme);

				node.Load += (s, e) =>
				{
					iconView.OnLoad(e);

					iconView.ImageSet += (s1, e1) =>
					{
						node.Image = iconView.imageWidget.Image;
						node.Invalidate();
					};
				};
			}

			parentNode.Nodes.Add(node);
			parentNode.Expanded = true;

			return node;
		}

		private static string BuildDefaultName(IObject3D item)
		{
			string nameToWrite = "";
			if (!string.IsNullOrEmpty(item.Name))
			{
				nameToWrite += $"{item.Name}";
			}
			else
			{
				nameToWrite += $"{item.GetType().Name}";
			}

			return nameToWrite;
		}

	}
}
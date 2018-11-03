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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public static class Object3DTreeBuilder
	{
		public static TreeNode BuildTree(IObject3D rootItem, Dictionary<IObject3D, TreeNode> keyValues, ThemeConfig theme)
		{
			return AddTree(BuildItemView(rootItem), null, keyValues, theme);
		}

		private static TreeNode AddTree(ObjectView item, TreeNode parent, Dictionary<IObject3D, TreeNode> keyValues, ThemeConfig theme)
		{
			// Suppress MeshWrapper and OperationSource nodes in tree
			bool shouldCollapseToParent = item.Source is ModifiedMeshObject3D || item.Source is OperationSourceObject3D;
			var contextNode = (shouldCollapseToParent && parent != null) ? parent : AddItem(item, parent, keyValues, theme);

			using (contextNode.LayoutLock())
			{
				var componentObject3D = item.Source as ComponentObject3D;

				if (componentObject3D?.Finalized == false
					|| componentObject3D is null && !(item.Source is IVisualLeafNode))
				{
					foreach (var child in item.Children)
					{
						if (child != null)
						{
							AddTree(BuildItemView(child), contextNode, keyValues, theme);
						}
					}
				}
			}

			return contextNode;
		}

		private static TreeNode AddItem(ObjectView item, TreeNode parentNode, Dictionary<IObject3D, TreeNode> keyValues, ThemeConfig theme)
		{
			if(item.Source is InsertionGroupObject3D insertionGroup)
			{
				return new TreeNode(theme)
				{
					Text = "Loading".Localize(),
					Tag = item.Source,
					TextColor = theme.TextColor,
					PointSize = theme.DefaultFontSize,
				};
			}

			var node = new TreeNode(theme)
			{
				Text = GetName(item),
				Tag = item.Source,
				TextColor = theme.TextColor,
				PointSize = theme.DefaultFontSize,
			};

			keyValues.Add(item.Source, node);

			// Check for operation resulting in the given type
			if (ApplicationController.Instance.Thumbnails.OperationIcons.TryGetValue(item.Source.GetType(), out Func<ImageBuffer> iconSource))
			{
				// If exists, use the operation icon
				node.Image = iconSource?.Invoke();
			}
			else
			{
				node.Image = ApplicationController.Instance.Thumbnails.DefaultThumbnail();
			}

			if (parentNode != null)
			{
				parentNode.Nodes.Add(node);
				parentNode.Expanded = true;
			}

			return node;
		}

		private static string GetName(ObjectView item)
		{
			return !string.IsNullOrEmpty(item.Name) ? $"{item.Name}" : $"{item.GetType().Name}";
		}

		private static ObjectView BuildItemView(IObject3D item)
		{
			switch (item)
			{
				case TransformWrapperObject3D fitToBounds3D:
					return new ObjectView()
					{
						Children = new IObject3D[] { fitToBounds3D.SourceItem },
						Name = item.Name,
						Source = item
					};

				case ArrayLinearObject3D arrayLinear3D:
					return new ObjectView()
					{
						Children = item.Children.OfType<OperationSourceObject3D>().ToList(),
						Name = $"{arrayLinear3D.Name} ({arrayLinear3D.Count})",
						Source = item
					};

				case ArrayAdvancedObject3D arrayAdvanced3D:
					return new ObjectView()
					{
						Children = item.Children.OfType<OperationSourceObject3D>().ToList(),
						Name = $"{arrayAdvanced3D.Name} ({arrayAdvanced3D.Count})",
						Source = item
					};

				// TODO: array operations should only expose OperationSource
				case ArrayRadialObject3D arrayRadial3D:
					return new ObjectView()
					{
						Children = item.Children.OfType<OperationSourceObject3D>().ToList(),
						Name = $"{arrayRadial3D.Name} ({arrayRadial3D.Count})",
						Source = item
					};

				default:
					return new ObjectView(item);
			}
		}

		private class ObjectView
		{
			public ObjectView()
			{
			}

			public ObjectView(IObject3D source)
			{
				this.Source = source;
				this.Children = this.Source.Children;
				this.Name = this.Source.Name;
			}

			public IEnumerable<IObject3D> Children { get; set; }

			public string Name { get; set; }

			public IObject3D Source { get; set; }
		}
	}
}
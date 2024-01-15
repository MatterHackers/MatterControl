﻿/*
Copyright (c) 2023, Lars Brubaker, John Lewin
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

using Matter_CAD_Lib.DesignTools._Object3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public static class Object3DTreeBuilder
    {
        public static TreeNode BuildTree(IObject3D rootItem, Dictionary<IObject3D, TreeNode> keyValues, ThemeConfig theme)
        {
            return AddTree(BuildItemView(rootItem), null, keyValues, theme);
        }

        private static TreeNode AddTree(ObjectView objectView, TreeNode parentTreeNode, Dictionary<IObject3D, TreeNode> keyValues, ThemeConfig theme)
        {
            // Suppress MeshWrapper and OperationSource nodes in tree
            bool shouldCollapseToParent = objectView.Source is ModifiedMeshObject3D || objectView.Source is OperationSourceObject3D;
            TreeNode contextNode;

            if (shouldCollapseToParent
                && parentTreeNode != null
                && !keyValues.ContainsKey(objectView.Source))
            {
                contextNode = parentTreeNode;
            }
            else
            {
                var itemName = GetName(objectView);
                contextNode = AddItem(objectView.Source, itemName, parentTreeNode, keyValues, theme);
            }

            using (contextNode.LayoutLock())
            {
                var componentObject3D = objectView.Source as IComponentObject3D;
                var hideChildren = objectView.Source.GetType().GetCustomAttributes(typeof(HideChildrenFromTreeViewAttribute), true).Any();

                if ((componentObject3D?.Finalized == false
                    || !hideChildren)
                    && objectView.Children?.Any() == true)
                {
                    var orderChildrenByIndex = objectView.Source.GetType().GetCustomAttributes(typeof(OrderChildrenByIndexAttribute), true).Any();
                    IEnumerable<IObject3D> children = objectView.Children;

                    if (!orderChildrenByIndex)
                    {
                        children = objectView.Children.OrderBy(i => i.Name);
                    }
                    foreach (var child in children)
                    {
                        if (child != null
                            && !child.GetType().GetCustomAttributes(typeof(HideFromTreeViewAttribute), true).Any())
                        {
                            AddTree(BuildItemView(child), contextNode, keyValues, theme);
                        }
                    }
                }
            }

            return contextNode;
        }

        private static TreeNode AddItem(IObject3D item, string itemName, TreeNode parentNode, Dictionary<IObject3D, TreeNode> keyValues, ThemeConfig theme)
        {
            if (item is InsertionGroupObject3D insertionGroup)
            {
                return new TreeNode(theme)
                {
                    Text = "Loading".Localize(),
                    Tag = item,
                    TextColor = theme.TextColor,
                    PointSize = theme.DefaultFontSize,
                };
            }

            var node = new TreeNode(theme)
            {
                Text = itemName,
                Tag = item,
                TextColor = theme.TextColor,
                PointSize = theme.DefaultFontSize,
            };

            if (!keyValues.ContainsKey(item))
            {
                keyValues.Add(item, node);
            }

            // Check for operation resulting in the given type
            var image = SceneOperations.GetIcon(item.GetType(), theme);

            if (image != null)
            {
                node.Image = image;
            }
            else
            {
                node.Image = ApplicationController.Instance.Thumbnails.DefaultThumbnail();

                node.Load += (s, e) =>
                {
                    string contentID = item.MeshRenderId().ToString();
                    if (item is IStaticThumbnail staticThumbnail)
                    {
                        contentID = $"MatterHackers/ItemGenerator/{staticThumbnail.ThumbnailName}".GetLongHashCode().ToString();
                    }

                    var thumbnail = ApplicationController.Instance.Thumbnails.LoadCachedImage(contentID, 16, 16);

                    node.Image = thumbnail ?? ApplicationController.Instance.Thumbnails.DefaultThumbnail();
                };
            }

            if (parentNode != null)
            {
                parentNode.Nodes.Add(node);
                if (parentNode.Tag is IObject3D object3D)
                {
                    parentNode.Expanded = object3D.Expanded;
                }
            }

            node.ExpandedChanged += (s, e) =>
            {
                if (item is Object3D object3D)
                {
                    object3D.Expanded = node.Expanded;
                }
            };

            return node;
        }

        private static string GetName(ObjectView item)
        {
            return !string.IsNullOrEmpty(item.Name) ? $"{item.Name}" : $"{item.GetType().Name}";
        }

        private static ObjectView BuildItemView(IObject3D item)
        {
            string GetArrayName(ArrayObject3D arrayItem)
            {
                if (string.IsNullOrWhiteSpace(item.Name)
                    && arrayItem?.SourceContainer?.Children?.Any() == true)
                {
                    var childName = arrayItem.SourceContainer.Children.First().Name;
                    if (childName.Length > 20)
                    {
                        childName = childName.Substring(0, 20) + "...";
                    }

                    return $"{childName} - x{arrayItem.Count}";
                }

                return arrayItem.Name;
            }

            if (item is ArrayObject3D array)
            {
                return new ObjectView()
                {
                    Children = item.Children.OfType<OperationSourceObject3D>().ToList(),
                    Name = GetArrayName(array),
                    Source = item
                };
            }
            else
            {
                switch (item)
                {
                    case TransformWrapperObject3D transformWrapperObject3D:
                        return new ObjectView()
                        {
                            Children = transformWrapperObject3D.UntransformedChildren,
                            Name = item.Name,
                            Source = item
                        };

                    case OperationSourceContainerObject3D operationSourceContainerObject3D:
                        return new ObjectView()
                        {
                            Children = item.Children.OfType<OperationSourceObject3D>().ToList(),
                            Name = operationSourceContainerObject3D.Name,
                            Source = item
                        };

                    default:
                        return new ObjectView(item);
                }
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
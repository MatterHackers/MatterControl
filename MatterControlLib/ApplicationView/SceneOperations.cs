/*
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.VectorMath;
using MatterControlLib.PartPreviewWindow.View3D.GeometryNodes;

[assembly: InternalsVisibleTo("MatterControl.Tests")]
[assembly: InternalsVisibleTo("MatterControl.AutomationTests")]
[assembly: InternalsVisibleTo("CloudServices.Tests")]

namespace MatterHackers.MatterControl
{
    public static class SceneOperations
	{
		private static bool built;

		private static List<SceneOperation> registeredOperations;

		public static IEnumerable<SceneOperation> All => registeredOperations;

		private static Dictionary<Type, Func<ThemeConfig, ImageBuffer>> Icons { get; set; }

		private static Dictionary<string, SceneOperation> OperationsById { get; } = new Dictionary<string, SceneOperation>();

		public static SceneOperation AddBaseOperation()
		{
			return new SceneOperation("AddBase")
			{
				TitleGetter = () => "Add Base".Localize(),
				ResultType = typeof(BaseObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var item = scene.SelectedItem;

					var newChild = item.DeepCopy();
					var baseMesh = new BaseObject3D()
					{
						Matrix = newChild.Matrix
					};
					newChild.Matrix = Matrix4X4.Identity;
					baseMesh.Children.Add(newChild);
					baseMesh.Invalidate(InvalidateType.Properties);

					scene.UndoBuffer.AddAndDo(
						new ReplaceCommand(
							new List<IObject3D> { item },
							new List<IObject3D> { baseMesh }));

					scene.SelectedItem = baseMesh;
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("add_base.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				// this is for when base is working with generic meshes
				//IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem.IsPathObject()),
				// this is for when only IPathObjects are working correctly
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem.DescendantsAndSelf().Where(i => i is IPathProvider).Any(),
			};
		}

		public static PopupMenu AddModifyItems(PopupMenu popupMenu, ThemeConfig theme, ISceneContext sceneContext, Func<SceneOperation, bool> includeInToolbarOverflow = null)
		{
			bool Show(SceneOperation operation)
			{
				// If we are creating the toolbar overflow
				if (includeInToolbarOverflow != null)
				{
					if (registeredOperations.Where(i => i.Id == operation.Id).Any())
					{
						// it is in the root so check it
						return includeInToolbarOverflow(operation);
					}
					else
					{
						// if the group it is in is expanded, check it
					}

					return true;
				}

				// It is a context popup menu, do more filtering
				if (operation.ShowInModifyMenu?.Invoke(sceneContext) == false
					|| operation.IsEnabled?.Invoke(sceneContext) != true)
				{
					return false;
				}

				bool visible = true;
				if (operation is OperationGroup operationGroup)
				{
					visible = false;
					foreach (var childOperation in operationGroup.Operations)
					{
						visible |= Show(childOperation);
					}
				}

				return visible;
			}

			foreach (var operation in All)
			{
				if (!Show(operation))
				{
					continue;
				}

				if (operation is OperationGroup operationGroup)
				{
					popupMenu.CreateSubMenu(
						operationGroup.Title,
						theme,
						(subMenu) =>
						{
							foreach (var childOperation in operationGroup.Operations)
							{
								if (!Show(childOperation))
								{
									continue;
								}

								var menuItem = subMenu.CreateMenuItem(childOperation.Title, childOperation.Icon(theme));
								menuItem.Click += (s, e) => UiThread.RunOnIdle(() =>
								{
									childOperation.Action?.Invoke(sceneContext);
								});

								menuItem.Enabled = childOperation.IsEnabled(sceneContext);
								menuItem.ToolTipText = childOperation.HelpText ?? "";
							}
						});
				}
				else if (operation is SceneSelectionSeparator separator)
				{
				}
				else
				{
					var menuItem = popupMenu.CreateMenuItem(operation.Title, operation.Icon(theme));
					menuItem.Click += (s, e) => operation.Action(sceneContext);
					menuItem.Enabled = operation.IsEnabled(sceneContext);
					menuItem.ToolTipText = operation.HelpText ?? "";
				}
			}

			return popupMenu;
		}

		public static void AddOperation(SceneOperation operation, string id)
		{
			Build();

			foreach (var item in All)
			{
				if (item is OperationGroup group)
				{
					if (group.Id == id)
					{
						group.Operations.Add(operation);
					}
				}
			}

			RegisterIconsAndIdsRecursive(operation);
		}

		public static SceneOperation ById(string id)
		{
			return OperationsById[id];
		}

		public static SceneOperation EditComponentOperation()
		{
			return new SceneOperation("EditComponent")
			{
				TitleGetter = () => "Edit Component".Localize(),
				ResultType = typeof(IComponentObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					if (sceneItem is IComponentObject3D componentObject)
					{
						// Enable editing mode
						componentObject.Finalized = false;

						// Force editor rebuild
						scene.SelectedItem = null;
						scene.SelectedItem = componentObject as Object3D;

						scene.Invalidate(new InvalidateArgs(null, InvalidateType.Children));
					}
				},
				ShowInModifyMenu = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					return sceneItem.Parent != null
						&& sceneItem.Parent.Parent == null
						&& sceneItem is IComponentObject3D componentObject
						&& componentObject.Finalized
						&& !componentObject.ProOnly;
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("scale_32x32.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A component must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && (sceneContext.Scene.SelectedItem is IComponentObject3D),
			};
		}

		public static ImageBuffer GetIcon(Type type, ThemeConfig theme)
		{
			if (Icons.ContainsKey(type))
			{
				return Icons[type].Invoke(theme);
			}

			return null;
		}

		public static PopupMenu GetToolbarOverflowMenu(ThemeConfig theme, ISceneContext sceneContext, Func<SceneOperation, bool> includeInToolbarOverflow = null)
		{
			var popupMenu = new PopupMenu(theme);
			AddModifyItems(popupMenu, theme, sceneContext, includeInToolbarOverflow);
			return popupMenu;
		}

		public static SceneOperation ImageConverterOperation()
		{
			return new SceneOperation("ImageConverter")
			{
				TitleGetter = () => "Image Converter".Localize(),
				ResultType = typeof(IComponentObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					var imageObject = sceneItem.DeepCopy() as ImageObject3D;
					var finalMatrix = imageObject.Matrix;
					imageObject.Matrix = Matrix4X4.Identity;

					var path = new ImageToPathObject3D_2();
					path.Children.Add(imageObject);

					var smooth = new SmoothPathObject3D();
					smooth.Children.Add(path);

					var extrude = new LinearExtrudeObject3D();
					extrude.Children.Add(smooth);

					var baseObject = new BaseObject3D()
					{
						BaseType = BaseTypes.None
					};
					baseObject.Children.Add(extrude);

					var component = new ComponentObject3D(new[] { baseObject })
					{
						Name = "Image Converter".Localize(),
						ComponentID = "4D9BD8DB-C544-4294-9C08-4195A409217A",
						SurfacedEditors = new List<string>
							{
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Children<SmoothPathObject3D>.Children<ImageToPathObject3D_2>.Children<ImageObject3D>.ImageSearch",
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Children<SmoothPathObject3D>.Children<ImageToPathObject3D_2>.Image",
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Children<SmoothPathObject3D>.Children<ImageToPathObject3D_2>.Children<ImageObject3D>.Invert",
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Children<SmoothPathObject3D>.Children<ImageToPathObject3D_2>.Children<ImageObject3D>.AssetPath",
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Children<SmoothPathObject3D>.Children<ImageToPathObject3D_2>.AnalysisType",
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Children<SmoothPathObject3D>.Children<ImageToPathObject3D_2>.TransparencyMessage",
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Children<SmoothPathObject3D>.Children<ImageToPathObject3D_2>.Histogram",
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Children<SmoothPathObject3D>.Children<ImageToPathObject3D_2>.MinSurfaceArea",
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Height",
								"$.Children<BaseObject3D>",
							}
					};

					component.Matrix = finalMatrix;

					scene.SelectedItem = null;
					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { component }));
					scene.SelectedItem = component;

					// Invalidate image to kick off rebuild of ImageConverter stack
					imageObject.Invalidate(InvalidateType.Image);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("image_converter.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "An image must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is ImageObject3D,
			};
		}

		public static SceneOperation ImageToPathOperation()
		{
			return new SceneOperation("ImageToPath")
			{
				TitleGetter = () => "Image to Path".Localize(),
				ResultType = typeof(ImageToPathObject3D_2),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					if (sceneItem is IImageProvider imageObject)
					{
						// TODO: make it look like this (and get rid of all the other stuff)
						// scene.Replace(sceneItem, new ImageToPathObject3D_2(sceneItem.Clone()));

						var path = new ImageToPathObject3D_2();

						var itemClone = sceneItem.DeepCopy();
						path.Children.Add(itemClone);
						path.Matrix = itemClone.Matrix;
						itemClone.Matrix = Matrix4X4.Identity;

						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { path }));
						scene.SelectedItem = null;
						scene.SelectedItem = path;
						path.Invalidate(InvalidateType.Properties);
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("image_to_path.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "An image must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IImageProvider,
			};
		}

		public static SceneOperation InflatePathOperation()
		{
			return new SceneOperation("InflatePath")
			{
				TitleGetter = () => "Inflate Path".Localize(),
				ResultType = typeof(InflatePathObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					var inflatePath = new InflatePathObject3D();
					var itemClone = sceneItem.DeepCopy();
					inflatePath.Children.Add(itemClone);
					inflatePath.Matrix = itemClone.Matrix;
					itemClone.Matrix = Matrix4X4.Identity;

					scene.SelectedItem = null;
					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { inflatePath }));
					scene.SelectedItem = inflatePath;


					inflatePath.Invalidate(InvalidateType.Properties);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("inflate_path.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IPathProvider,
			};
		}

        public static SceneOperation SelectPathsOperation()
        {
            return new SceneOperation("SelectPaths")
            {
                TitleGetter = () => "Select Paths".Localize(),
                ResultType = typeof(SelectPathsObject3D),
                Action = (sceneContext) =>
                {
                    var scene = sceneContext.Scene;
                    var sceneItem = scene.SelectedItem;
                    var outlinePath = new SelectPathsObject3D();
                    var itemClone = sceneItem.DeepCopy();
                    outlinePath.Children.Add(itemClone);
                    outlinePath.Matrix = itemClone.Matrix;
                    itemClone.Matrix = Matrix4X4.Identity;

                    scene.SelectedItem = null;
                    scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { outlinePath }));
                    scene.SelectedItem = outlinePath;


                    outlinePath.Invalidate(InvalidateType.Properties);
                },
                Icon = (theme) => StaticData.Instance.LoadIcon("select_curves.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
                HelpTextGetter = () => "A path must be selected".Localize().Stars(),
                IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IPathProvider,
            };
        }
        
		public static SceneOperation LinearExtrudeOperation()
		{
			return new SceneOperation("LinearExtrude")
			{
				TitleGetter = () => "Linear Extrude".Localize(),
				ResultType = typeof(LinearExtrudeObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					var pathObject = sceneItem as IPathProvider;
                    if (pathObject != null)
					{
						var extrude = new LinearExtrudeObject3D();

						var itemClone = sceneItem.DeepCopy();
						extrude.Children.Add(itemClone);

						scene.SelectedItem = null;
						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { extrude }));
						scene.SelectedItem = extrude;

						extrude.Invalidate(InvalidateType.Properties);
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("linear_extrude.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IPathProvider,
			};
		}

		public static SceneOperation RevolveOperation()
		{
			return new SceneOperation("Revolve")
			{
				TitleGetter = () => "Revolve".Localize(),
				ResultType = typeof(RevolveObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					var pathObject = sceneItem as IPathProvider;
                    if (pathObject != null)
					{
						var revolve = new RevolveObject3D();

						var itemClone = sceneItem.DeepCopy();
						revolve.Children.Add(itemClone);

						scene.SelectedItem = null;
						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { revolve }));
						scene.SelectedItem = revolve;

						revolve.Invalidate(InvalidateType.Properties);
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("revolve.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IPathProvider,
			};
		}

		public static SceneOperation MakeComponentOperation()
		{
			return new SceneOperation("Make Component")
			{
				TitleGetter = () => "Make Component".Localize(),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;

					IEnumerable<IObject3D> items = new[] { sceneItem };

					// If SelectionGroup, operate on Children instead
					if (sceneItem is SelectionGroupObject3D)
					{
						items = sceneItem.Children;
					}

					// Dump selection forcing collapse of selection group
					scene.SelectedItem = null;
					var component = new ComponentObject3D
					{
						Name = "New Component",
						Finalized = false
					};

					// Copy an selected item into the component as a clone
					component.Children.Modify(children =>
					{
						children.AddRange(items.Select(o => o.DeepCopy()));
					});

					component.MakeNameNonColliding();

					scene.UndoBuffer.AddAndDo(new ReplaceCommand(items, new[] { component }));
					scene.SelectedItem = component;
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("component.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) =>
				{
					var sceneItem = sceneContext.Scene.SelectedItem;
					return sceneItem?.Parent != null
						&& sceneItem.Parent.Parent == null;
				},
			};
		}

		public static SceneOperation MirrorOperation()
		{
			return new SceneOperation("Mirror")
			{
				ResultType = typeof(MirrorObject3D_2),
				TitleGetter = () => "Mirror".Localize(),
				Action = (sceneContext) =>
				{
					new MirrorObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("mirror_32x32.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		public static SceneOperation OutlinePathOperation()
		{
			return new SceneOperation("OutlinePath")
			{
				TitleGetter = () => "Outline Path".Localize(),
				ResultType = typeof(OutlinePathObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					var outlinePath = new OutlinePathObject3D();
					var itemClone = sceneItem.DeepCopy();
					outlinePath.Children.Add(itemClone);
					outlinePath.Matrix = itemClone.Matrix;
					itemClone.Matrix = Matrix4X4.Identity;

					scene.SelectedItem = null;
					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { outlinePath }));
					scene.SelectedItem = outlinePath;

					outlinePath.Invalidate(InvalidateType.Properties);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("outline.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IPathProvider,
			};
		}

		public static SceneOperation RotateOperation()
		{
			return new SceneOperation("Rotate")
			{
				ResultType = typeof(RotateObject3D_2),
				TitleGetter = () => "Rotate".Localize(),
				Action = (sceneContext) =>
				{
					new RotateObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon(Path.Combine("ViewTransformControls", "rotate.png"), 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		public static SceneOperation ScaleOperation()
		{
			return new SceneOperation("Scale")
			{
				ResultType = typeof(ScaleObject3D_3),
				TitleGetter = () => "Scale".Localize(),
				Action = (sceneContext) =>
				{
					new ScaleObject3D_3().WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("scale_32x32.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		public static SceneOperation SmoothPathOperation()
		{
			return new SceneOperation("SmoothPath")
			{
				TitleGetter = () => "Smooth Path".Localize(),
				ResultType = typeof(SmoothPathObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					var smoothPath = new SmoothPathObject3D();
					var itemClone = sceneItem.DeepCopy();
					smoothPath.Children.Add(itemClone);
					smoothPath.Matrix = itemClone.Matrix;
					itemClone.Matrix = Matrix4X4.Identity;

					scene.SelectedItem = null;
					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { smoothPath }));
					scene.SelectedItem = smoothPath;

					smoothPath.Invalidate(InvalidateType.Properties);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("smooth_path.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IPathProvider,
			};
		}

		public static SceneOperation TranslateOperation()
		{
			return new SceneOperation("Translate")
			{
				ResultType = typeof(TranslateObject3D),
				TitleGetter = () => "Translate".Localize(),
				Action = (sceneContext) =>
				{
					new TranslateObject3D().WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon(Path.Combine("ViewTransformControls", "translate.png"), 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		private static SceneOperation AdvancedArrayOperation()
		{
			return new SceneOperation("Advanced Array")
			{
				ResultType = typeof(ArrayAdvancedObject3D),
				TitleGetter = () => "Advanced Array".Localize(),
				Action = (sceneContext) =>
				{
					var array = new ArrayAdvancedObject3D
					{
						Name = "" // this will get the default behavior of showing the child's name + a count
					};
					array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("array_advanced.png", 16, 16).SetPreMultiply(),
				HelpTextGetter = () => "A single part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private static SceneOperation AlignOperation()
		{
			return new SceneOperation("Align")
			{
				ResultType = typeof(AlignObject3D_2),
				TitleGetter = () => "Align".Localize(),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					var align = new AlignObject3D_2();
					align.AddSelectionAsChildren(scene, selectedItem);
					align.Name = align.NameFromChildren();
					align.NameOverriden = false;
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("align_left_dark.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 2 parts must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem is SelectionGroupObject3D,
			};
		}

		private static SceneOperation ArrangeAllPartsOperation()
		{
			return new SceneOperation("ArrangeAllParts")
			{
				TitleGetter = () => "Arrange All Parts".Localize(),
				Action = async (sceneContext) =>
				{
					await sceneContext.Scene.AutoArrangeChildren(new Vector3(sceneContext.BedCenter)).ConfigureAwait(false);
				},
				HelpTextGetter = () => "No part to arrange".Localize().Stars(),
				IsEnabled = (sceneContext) =>
				{
					return sceneContext.EditableScene && sceneContext.Scene.VisibleMeshes().Any();
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("arrange_all.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				ShowInModifyMenu = (sceneContext) => false,
			};
		}

		/// <summary>
        /// Determines if the selected item is a candidate for boolean operations.
        /// </summary>
        /// <param name="selectedItem">The selected item in the scene.</param>
        /// <param name="includePaths">Flag indicating whether to include path items in the check.</param>
        /// <returns>Returns true if the selected item is a candidate for boolean operations, false otherwise.</returns>
        private static bool BooleanCandidate(IObject3D selectedItem, bool includePaths)
        {
            if (selectedItem != null)
            {
                // all are path items
                if (includePaths
                    && selectedItem.VisibleMeshes().Count() > 1
                    && selectedItem.VisibleMeshes().All(i => i is IPathProvider))
                {
                    return true;
                }

                // mesh items
                if (selectedItem.VisibleMeshes().Count() > 1
                    && selectedItem.VisibleMeshes().All(i => IsMeshObject(i)))
                {
                    return true;
                }
            }

            return false;
        }

		private static void Build()
		{
			if (built)
			{
				return;
			}

			built = true;

			Object3D.RunAysncRebuild = (name, func) => ApplicationController.Instance.Tasks.Execute(name, null, func);

			registeredOperations = new List<SceneOperation>()
			{
				ArrangeAllPartsOperation(),
				new SceneSelectionSeparator(),
				LayFlatOperation(),
				RebuildOperation(),
				GroupOperation(),
				UngroupOperation(),
				new SceneSelectionSeparator(),
				DuplicateOperation(),
				RemoveOperation(),
				new SceneSelectionSeparator(),
				new OperationGroup("Transform")
				{
					TitleGetter = () => "Transform".Localize(),
					InitialSelectionIndex = 2,
					Operations = new List<SceneOperation>()
					{
						TranslateOperation(),
						RotateOperation(),
						ScaleOperation(),
						MirrorOperation(),
					}
				},
				new OperationGroup("Placement")
				{
					TitleGetter = () => "Placement".Localize(),
					Operations = new List<SceneOperation>()
					{
						AlignOperation(),
						DualExtrusionAlignOperation(),
					},
				},
				new OperationGroup("Reshape")
				{
					TitleGetter = () => "Reshape".Localize(),
					Operations = new List<SceneOperation>()
					{
						CurveOperation(),
						PinchOperation(),
                        RadialPinchOperation(),
                        TwistOperation(),
						PlaneCutOperation(),
#if DEBUG
						SliceToPathOperation(),
#endif
						HollowOutOperation(),
					}
				},
				new OperationGroup("Image")
				{
					TitleGetter = () => "Image".Localize(),
					Operations = new List<SceneOperation>()
					{
						ImageConverterOperation(),
						ImageToPathOperation(),
					}
				},
				new OperationGroup("Path")
				{
					TitleGetter = () => "Path".Localize(),
					Visible = OperationGroup.GetVisible("Path", false),
					Operations = new List<SceneOperation>()
					{
						LinearExtrudeOperation(),
						RevolveOperation(),
						SmoothPathOperation(),
						InflatePathOperation(),
						OutlinePathOperation(),
						AddBaseOperation(),
                        SelectPathsOperation(),
                    }
                },
				new OperationGroup("Merge")
				{
					TitleGetter = () => "Merge".Localize(),
					InitialSelectionIndex = 1,
					Operations = new List<SceneOperation>()
					{
						CombineOperation(),
						SubtractOperation(),
						IntersectOperation(),
						SubtractAndReplaceOperation(),
					}
				},
				new OperationGroup("Duplication")
				{
					TitleGetter = () => "Duplication".Localize(),
					Operations = new List<SceneOperation>()
					{
                        CloneOperation(),
                        LinearArrayOperation(),
						RadialArrayOperation(),
						AdvancedArrayOperation(),
					}
				},
				new OperationGroup("Mesh")
				{
					TitleGetter = () => "Mesh".Localize(),
					InitialSelectionIndex = 1,
					Operations = new List<SceneOperation>()
					{
						ReduceOperation(),
						RepairOperation(),
					}
				},
				new OperationGroup("Constraints")
				{
					TitleGetter = () => "Constraints".Localize(),
					Visible = OperationGroup.GetVisible("Path", false),
					Operations = new List<SceneOperation>()
					{
						FitToBoundsOperation(),

#if DEBUG
						FitToCylinderOperation(),
#endif
						MakeComponentOperation(),
						EditComponentOperation(),
                        AddGeometyNodesOperation(),
					},
				},
			};

			Icons = new Dictionary<Type, Func<ThemeConfig, ImageBuffer>>();

			foreach (var operation in registeredOperations)
			{
				RegisterIconsAndIdsRecursive(operation);
			}

			// Explicitly register SelectionGroup icon
			if (Icons.TryGetValue(typeof(GroupObject3D), out Func<ThemeConfig, ImageBuffer> groupIconSource))
			{
				Icons.Add(typeof(SelectionGroupObject3D), groupIconSource);
			}

			// register legacy types so they still show, they don't have ui to create so they don't have icons set dynamically
			Icons.Add(typeof(AlignObject3D), (theme) => StaticData.Instance.LoadIcon("align_left_dark.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply());

			Icons.Add(typeof(ImageObject3D), (theme) => StaticData.Instance.LoadIcon("image_converter.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply());
		}

        private static SceneOperation AddGeometyNodesOperation()
        {
            return new SceneOperation("Add Geometry Nodes")
            {
                ResultType = typeof(GeometryNodesObject3D),
                TitleGetter = () => "Add Geometry Nodes".Localize(),
                Action = async (sceneContext) =>
                {
                    var geometryNodes = new GeometryNodesObject3D();
                    await geometryNodes.ConvertChildrenToNodes(sceneContext.Scene);
                },
                Icon = (theme) => StaticData.Instance.LoadIcon("nodes.png", 16, 16).GrayToColor(theme.TextColor),
                HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
                IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
            };
        }

        private static SceneOperation CloneOperation()
        {
            return new SceneOperation("Clone")
            {
                Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
                    var selectedItem = scene.SelectedItem;
					if (string.IsNullOrEmpty(selectedItem.CloneID))
					{
						// set it to a new guid
						selectedItem.CloneID = Guid.NewGuid().ToString();
					}

					var clone = selectedItem.DeepCopy();
                    scene.UndoBuffer.AddAndDo(new InsertCommand(scene, clone));
                },
                HelpTextGetter = () => "A single part must be selected".Localize().Stars(),
                Icon = (theme) => StaticData.Instance.LoadIcon("clone.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
                IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
                TitleGetter = () => "Clone".Localize(),
            };
        }

        private static SceneOperation CombineOperation()
		{
			return new SceneOperation("Combine")
			{
				ResultType = typeof(CombineObject3D_2),
				TitleGetter = () => "Combine".Localize(),
				Action = (sceneContext) =>
				{
                    if (sceneContext.Scene.SelectedItem.VisibleMeshes().All(o => o is IPathProvider))
                    {
                        new MergePathObject3D("Combine".Localize(), ClipperLib.ClipType.ctUnion).WrapSelectedItemAndSelect(sceneContext.Scene);
					}
					else
					{
						new CombineObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene);
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("combine.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 2 parts must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => BooleanCandidate(sceneContext.Scene.SelectedItem, true),
			};
		}

		private static SceneOperation CurveOperation()
		{
			return new SceneOperation("Curve")
			{
				ResultType = typeof(CurveObject3D_3),
				TitleGetter = () => "Curve".Localize(),
				Action = (sceneContext) =>
				{
					var curve = new CurveObject3D_3();
					curve.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("curve.png", 16, 16).GrayToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation DualExtrusionAlignOperation()
		{
			return new SceneOperation("Dual Extrusion Align")
			{
				TitleGetter = () => "Dual Extrusion Align".Localize(),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;

					if (selectedItem is SelectionGroupObject3D selectionGroup)
					{
						var first = selectionGroup.Children.FirstOrDefault();
						var center = first.GetCenter();
						var startMatrix = first.Matrix;
						first.Matrix = Matrix4X4.Identity;
						var offset = center - first.GetCenter();
						first.Matrix = startMatrix;

						var transformData = selectionGroup.Children.Select(c => new TransformData()
						{
							TransformedObject = c,
							UndoTransform = c.Matrix,
							RedoTransform = Matrix4X4.CreateTranslation(offset)
						}).ToList();

						scene.UndoBuffer.AddAndDo(new TransformCommand(transformData));
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("dual_align.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 2 parts must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem is SelectionGroupObject3D,
			};
		}

		private static SceneOperation DuplicateOperation()
		{
			return new SceneOperation("Duplicate")
			{
				TitleGetter = () => "Duplicate".Localize(),
				Action = (sceneContext) => sceneContext.DuplicateItemAddToScene(5),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
				Icon = (theme) => StaticData.Instance.LoadIcon("duplicate.png", 16, 16).SetPreMultiply(),
			};
		}

		private static SceneOperation FitToBoundsOperation()
		{
			return new SceneOperation("Fit to Bounds")
			{
				ResultType = typeof(FitToBoundsObject3D_4),
				TitleGetter = () => "Fit to Bounds".Localize(),
				Action = async (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					scene.SelectedItem = null;
					var fit = await FitToBoundsObject3D_4.Create(selectedItem.DeepCopy());
					fit.MakeNameNonColliding();
					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { selectedItem }, new[] { fit }));
					scene.SelectedItem = fit;
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("fit.png", 16, 16).GrayToColor(theme.TextColor),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private static SceneOperation FitToCylinderOperation()
		{
			return new SceneOperation("Fit to Cylinder")
			{
				ResultType = typeof(FitToCylinderObject3D),
				TitleGetter = () => "Fit to Cylinder".Localize(),
				Action = async (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					scene.SelectedItem = null;
					var fit = await FitToCylinderObject3D.Create(selectedItem.DeepCopy());
					fit.MakeNameNonColliding();
					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { selectedItem }, new[] { fit }));
					scene.SelectedItem = fit;
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("fit.png", 16, 16).GrayToColor(theme.TextColor),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private static SceneOperation GroupOperation()
		{
			return new SceneOperation("Group")
			{
				ResultType = typeof(GroupHolesAppliedObject3D),
				TitleGetter = () => "Group".Localize(),
				Action = (sceneContext) =>
				{
					var group = new GroupHolesAppliedObject3D();
					group.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				HelpTextGetter = () => "At least 2 parts must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene is InteractiveScene scene
					&& scene.SelectedItem != null
					&& scene.SelectedItem is SelectionGroupObject3D
					&& scene.SelectedItem.Children.Count > 1,
				Icon = (theme) => StaticData.Instance.LoadIcon("group.png", 16, 16).SetPreMultiply(),
				UiHint = "G Key".Localize(),
			};
		}

		private static SceneOperation HollowOutOperation()
		{
			return new SceneOperation("Hollow Out")
			{
				ResultType = typeof(HollowOutObject3D),
				TitleGetter = () => "Hollow Out".Localize(),
				Action = (sceneContext) =>
				{
					var hollowOut = new HollowOutObject3D();
					hollowOut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("hollow.png", 16, 16).GrayToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation IntersectOperation()
		{
			return new SceneOperation("Intersect")
			{
				ResultType = typeof(IntersectionObject3D_2),
				TitleGetter = () => "Intersect".Localize(),
				Action = (sceneContext) =>
				{
                    if (sceneContext.Scene.SelectedItem.VisibleMeshes().All(o => o is IPathProvider))
                    {
                        new MergePathObject3D("Intersect".Localize(), ClipperLib.ClipType.ctIntersection).WrapSelectedItemAndSelect(sceneContext.Scene);
					}
					else
					{
						new IntersectionObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene);
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("intersect.png", 16, 16),
				HelpTextGetter = () => "At least 2 parts must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => BooleanCandidate(sceneContext.Scene.SelectedItem, true),
			};
		}

		private static bool IsMeshObject(IObject3D item)
		{
			if (item != null)
			{
				if (item is ImageObject3D)
				{
					return false;
				}

				if (item is IPathProvider pathObject)
				{
					return pathObject.MeshIsSolidObject;
                }

				return true;
			}

			return false;
		}

		private static SceneOperation LayFlatOperation()
		{
			return new SceneOperation("Lay Flat")
			{
				TitleGetter = () => "Lay Flat".Localize(),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					if (selectedItem != null)
					{
						try
						{
							scene.MakeLowestFaceFlat(selectedItem);
						}
						catch
						{
						}
					}
				},
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
				Icon = (theme) => StaticData.Instance.LoadIcon("lay_flat.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
			};
		}

		private static SceneOperation RebuildOperation()
		{
			return new SceneOperation("Rebuild")
			{
				TitleGetter = () => "Rebuild".Localize(),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					if (selectedItem != null)
					{
						try
						{
							var updateItems = Expressions.SortAndLockUpdateItems(selectedItem.Parent, (item) =>
							{
								if (item == selectedItem || item.Parent == selectedItem)
								{
									// don't process this
									return false;
								}
								return true;
							}, false);

							Expressions.SendInvalidateInRebuildOrder(updateItems, InvalidateType.Properties, null);
						}
						catch
						{
						}
					}
				},
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
				Icon = (theme) => StaticData.Instance.LoadIcon("update.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
			};
		}

		private static SceneOperation LinearArrayOperation()
		{
			return new SceneOperation("Linear Array")
			{
				ResultType = typeof(ArrayLinearObject3D),
				TitleGetter = () => "Linear Array".Localize(),
				Action = (sceneContext) =>
				{
					var array = new ArrayLinearObject3D
					{
						Name = "" // this will get the default behavior of showing the child's name + a count
					};
					array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("array_linear.png", 16, 16).SetPreMultiply(),
				HelpTextGetter = () => "A single part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private static SceneOperation PinchOperation()
		{
			return new SceneOperation("Pinch")
			{
				ResultType = typeof(PinchObject3D_3),
				TitleGetter = () => "Pinch".Localize(),
				Action = (sceneContext) =>
				{
					var pinch = new PinchObject3D_3();
					pinch.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("pinch.png", 16, 16).GrayToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation PlaneCutOperation()
		{
			return new SceneOperation("Plane Cut")
			{
				ResultType = typeof(PlaneCutObject3D),
				TitleGetter = () => "Plane Cut".Localize(),
				Action = (sceneContext) =>
				{
					var cut = new PlaneCutObject3D();
					cut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("plane_cut.png", 16, 16).GrayToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation SliceToPathOperation()
		{
			return new SceneOperation("Slice to Path")
			{
				ResultType = typeof(FindSliceObject3D),
				TitleGetter = () => "Slice to Path".Localize(),
				Action = (sceneContext) =>
				{
					var cut = new FindSliceObject3D();
					cut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("slice_to_path.png", 16, 16).GrayToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation RadialArrayOperation()
		{
			return new SceneOperation("Radial Array")
			{
				ResultType = typeof(ArrayRadialObject3D),
				TitleGetter = () => "Radial Array".Localize(),
				Action = (sceneContext) =>
				{
					var array = new ArrayRadialObject3D
					{
						Name = "" // this will get the default behavior of showing the child's name + a count
					};
					array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("array_radial.png", 16, 16).SetPreMultiply(),
				HelpTextGetter = () => "A single part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private static SceneOperation ReduceOperation()
		{
			return new SceneOperation("Reduce")
			{
				ResultType = typeof(DecimateObject3D),
				TitleGetter = () => "Reduce".Localize(),
				Action = (sceneContext) =>
				{
					var hollowOut = new DecimateObject3D();
					hollowOut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("reduce.png", 16, 16).GrayToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static void RegisterIconsAndIdsRecursive(SceneOperation operation)
		{
			if (operation == null)
            {
				return;
            }

			if (operation.ResultType != null
				&& !Icons.ContainsKey(operation.ResultType))
			{
				Icons.Add(operation.ResultType, operation.Icon);
			}

			if (operation.Id != null)
			{
				OperationsById.Add(operation.Id, operation);
			}

			if (operation is OperationGroup group)
			{
				foreach (var item in group.Operations)
				{
					RegisterIconsAndIdsRecursive(item);
				}
			}
		}

		private static SceneOperation RemoveOperation()
		{
			return new SceneOperation("Remove")
			{
				Action = (sceneContext) => sceneContext.Scene.DeleteSelection(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				Icon = (theme) => StaticData.Instance.LoadIcon("remove.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
				ShowInModifyMenu = (sceneContext) => false,
				TitleGetter = () => "Remove".Localize(),
				UiHint = "Delete Key".Localize(),
			};
		}

		private static SceneOperation RepairOperation()
		{
			return new SceneOperation("Repair")
			{
				ResultType = typeof(RepairObject3D),
				TitleGetter = () => "Repair".Localize(),
				Action = (sceneContext) =>
				{
					var hollowOut = new RepairObject3D();
					hollowOut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("repair.png", 16, 16).GrayToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation SubtractAndReplaceOperation()
		{
			return new SceneOperation("Subtract & Replace")
			{
				ResultType = typeof(SubtractAndReplaceObject3D_2),
				TitleGetter = () => "Subtract & Replace".Localize(),
				Action = (sceneContext) => new SubtractAndReplaceObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene),
				Icon = (theme) => StaticData.Instance.LoadIcon("subtract_and_replace.png", 16, 16).SetPreMultiply(),
				HelpTextGetter = () => "At least 2 parts must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => BooleanCandidate(sceneContext.Scene.SelectedItem, false),
			};
		}

		private static SceneOperation SubtractOperation()
		{
			return new SceneOperation("Subtract")
			{
				ResultType = typeof(SubtractObject3D_2),
				TitleGetter = () => "Subtract".Localize(),
				Action = (sceneContext) =>
				{
                    if (sceneContext.Scene.SelectedItem.VisibleMeshes().All(o => o is IPathProvider))
                    {
                        new SubtractPathObject3D().WrapSelectedItemAndSelect(sceneContext.Scene);
					}
					else
					{
						var subtractItem = new SubtractObject3D_2();
						subtractItem.WrapSelectedItemAndSelect(sceneContext.Scene);
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("subtract.png", 16, 16).SetPreMultiply(),
				HelpTextGetter = () => "At least 2 parts must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => BooleanCandidate(sceneContext.Scene.SelectedItem, true),
			};
		}

		private static SceneOperation TwistOperation()
		{
			return new SceneOperation("Twist")
			{
				ResultType = typeof(TwistObject3D),
				TitleGetter = () => "Twist".Localize(),
				Action = (sceneContext) =>
				{
					var twist = new TwistObject3D();
					twist.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("twist.png", 16, 16).GrayToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

        private static SceneOperation RadialPinchOperation()
        {
            return new SceneOperation("Radial Pinch")
            {
                ResultType = typeof(RadialPinchObject3D),
                TitleGetter = () => "Radial Pinch".Localize(),
                Action = (sceneContext) =>
                {
                    var radialPinch = new RadialPinchObject3D();
                    radialPinch.WrapSelectedItemAndSelect(sceneContext.Scene);
                },
                Icon = (theme) => StaticData.Instance.LoadIcon("radial-pinch.png", 16, 16).GrayToColor(theme.TextColor),
                HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
                IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
            };
        }


        private static SceneOperation UngroupOperation()
		{
			return new SceneOperation("Ungroup")
			{
				TitleGetter = () => "Ungroup".Localize(),
				Action = (sceneContext) => sceneContext.Scene.UngroupSelection(),
				HelpTextGetter = () => "A single part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) =>
				{
					var selectedItem = sceneContext.Scene.SelectedItem;
					if (selectedItem != null)
					{
						return selectedItem is GroupObject3D
							|| selectedItem is GroupHolesAppliedObject3D
							|| selectedItem.GetType() == typeof(Object3D)
							|| selectedItem.CanApply;
					}

					return false;
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("ungroup.png", 16, 16).GrayToColor(theme.TextColor).SetPreMultiply(),
				UiHint = "Shift + G".Localize(),
			};
		}
	}

    public interface IPrimaryOperationsSpecifier
    {
        IEnumerable<SceneOperation> GetOperations();
    }
}
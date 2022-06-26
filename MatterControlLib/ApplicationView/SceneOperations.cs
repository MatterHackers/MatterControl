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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

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

		private static Dictionary<Type, List<SceneOperation>> PrimaryOperations { get; } = new Dictionary<Type, List<SceneOperation>>();

		public static SceneOperation AddBaseOperation()
		{
			return new SceneOperation("AddBase")
			{
				OperationType = typeof(IObject3D),
				TitleGetter = () => "Add Base".Localize(),
				ResultType = typeof(BaseObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var item = scene.SelectedItem;

					var newChild = item.Clone();
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
				Icon = (theme) => StaticData.Instance.LoadIcon("add_base.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				// this is for when base is working with generic meshes
				//IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is IPathObject),
				// this is for when only IPathObjects are working correctly
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem.DescendantsAndSelf().Where(i => i is IPathObject).Any(),
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
				OperationType = typeof(IObject3D),
				TitleGetter = () => "Edit Component".Localize(),
				ResultType = typeof(ComponentObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					if (sceneItem is ComponentObject3D componentObject)
					{
						// Enable editing mode
						componentObject.Finalized = false;

						// Force editor rebuild
						scene.SelectedItem = null;
						scene.SelectedItem = componentObject;

						scene.Invalidate(new InvalidateArgs(null, InvalidateType.Children));
					}
				},
				ShowInModifyMenu = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					return sceneItem.Parent != null
						&& sceneItem.Parent.Parent == null
						&& sceneItem is ComponentObject3D componentObject
						&& componentObject.Finalized
						&& !componentObject.ProOnly;
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("scale_32x32.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A component must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && (sceneContext.Scene.SelectedItem is ComponentObject3D),
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

		public static IEnumerable<SceneOperation> GetPrimaryOperations(Type type)
		{
			if (PrimaryOperations.ContainsKey(type))
			{
				return PrimaryOperations[type];
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
				OperationType = typeof(ImageObject3D),
				TitleGetter = () => "Image Converter".Localize(),
				ResultType = typeof(ComponentObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					var imageObject = sceneItem.Clone() as ImageObject3D;
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
				Icon = (theme) => StaticData.Instance.LoadIcon("image_converter.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "An image must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is ImageObject3D,
			};
		}

		public static SceneOperation ImageToPathOperation()
		{
			return new SceneOperation("ImageToPath")
			{
				OperationType = typeof(ImageObject3D),
				TitleGetter = () => "Image to Path".Localize(),
				ResultType = typeof(ImageToPathObject3D_2),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					if (sceneItem is IObject3D imageObject)
					{
						// TODO: make it look like this (and get rid of all the other stuff)
						// scene.Replace(sceneItem, new ImageToPathObject3D_2(sceneItem.Clone()));

						var path = new ImageToPathObject3D_2();

						var itemClone = sceneItem.Clone();
						path.Children.Add(itemClone);
						path.Matrix = itemClone.Matrix;
						itemClone.Matrix = Matrix4X4.Identity;

						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { path }));
						scene.SelectedItem = null;
						scene.SelectedItem = path;
						path.Invalidate(InvalidateType.Properties);
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("image_to_path.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "An image must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is ImageObject3D,
			};
		}

		public static SceneOperation InflatePathOperation()
		{
			return new SceneOperation("InflatePath")
			{
				OperationType = typeof(IPathObject),
				TitleGetter = () => "Inflate Path".Localize(),
				ResultType = typeof(InflatePathObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					var inflatePath = new InflatePathObject3D();
					var itemClone = sceneItem.Clone();
					inflatePath.Children.Add(itemClone);
					inflatePath.Matrix = itemClone.Matrix;
					itemClone.Matrix = Matrix4X4.Identity;

					scene.SelectedItem = null;
					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { inflatePath }));
					scene.SelectedItem = inflatePath;


					inflatePath.Invalidate(InvalidateType.Properties);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("inflate_path.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IPathObject,
			};
		}

		public static SceneOperation LinearExtrudeOperation()
		{
			return new SceneOperation("LinearExtrude")
			{
				OperationType = typeof(IPathObject),
				TitleGetter = () => "Linear Extrude".Localize(),
				ResultType = typeof(LinearExtrudeObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					if (sceneItem is IPathObject pathObject)
					{
						var extrude = new LinearExtrudeObject3D();

						var itemClone = sceneItem.Clone();
						extrude.Children.Add(itemClone);
						extrude.Matrix = itemClone.Matrix;
						itemClone.Matrix = Matrix4X4.Identity;

						scene.SelectedItem = null;
						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { extrude }));
						scene.SelectedItem = extrude;

						extrude.Invalidate(InvalidateType.Properties);
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("linear_extrude.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IPathObject,
			};
		}

		public static SceneOperation RevolveOperation()
		{
			return new SceneOperation("Revolve")
			{
				OperationType = typeof(IPathObject),
				TitleGetter = () => "Revolve".Localize(),
				ResultType = typeof(RevolveObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					if (sceneItem is IPathObject pathObject)
					{
						var revolve = new RevolveObject3D();

						var itemClone = sceneItem.Clone();
						revolve.Children.Add(itemClone);
						revolve.Matrix = itemClone.Matrix;
						itemClone.Matrix = Matrix4X4.Identity;

						scene.SelectedItem = null;
						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { revolve }));
						scene.SelectedItem = revolve;

						revolve.Invalidate(InvalidateType.Properties);
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("revolve.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IPathObject,
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
						children.AddRange(items.Select(o => o.Clone()));
					});

					component.MakeNameNonColliding();

					scene.UndoBuffer.AddAndDo(new ReplaceCommand(items, new[] { component }));
					scene.SelectedItem = component;
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("component.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
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
				OperationType = typeof(IObject3D),
				ResultType = typeof(MirrorObject3D_2),
				TitleGetter = () => "Mirror".Localize(),
				Action = (sceneContext) =>
				{
					new MirrorObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("mirror_32x32.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		public static SceneOperation OutlinePathOperation()
		{
			return new SceneOperation("OutlinePath")
			{
				OperationType = typeof(IPathObject),
				TitleGetter = () => "Outline Path".Localize(),
				ResultType = typeof(OutlinePathObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					var outlinePath = new OutlinePathObject3D();
					var itemClone = sceneItem.Clone();
					outlinePath.Children.Add(itemClone);
					outlinePath.Matrix = itemClone.Matrix;
					itemClone.Matrix = Matrix4X4.Identity;

					scene.SelectedItem = null;
					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { outlinePath }));
					scene.SelectedItem = outlinePath;

					outlinePath.Invalidate(InvalidateType.Properties);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("outline.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IPathObject,
			};
		}

		public static SceneOperation RotateOperation()
		{
			return new SceneOperation("Rotate")
			{
				OperationType = typeof(IObject3D),
				ResultType = typeof(RotateObject3D_2),
				TitleGetter = () => "Rotate".Localize(),
				Action = (sceneContext) =>
				{
					new RotateObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon(Path.Combine("ViewTransformControls", "rotate.png"), 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		public static SceneOperation ScaleOperation()
		{
			return new SceneOperation("Scale")
			{
				OperationType = typeof(IObject3D),
				ResultType = typeof(ScaleObject3D_3),
				TitleGetter = () => "Scale".Localize(),
				Action = (sceneContext) =>
				{
					new ScaleObject3D_3().WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("scale_32x32.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		public static SceneOperation SmoothPathOperation()
		{
			return new SceneOperation("SmoothPath")
			{
				OperationType = typeof(IPathObject),
				TitleGetter = () => "Smooth Path".Localize(),
				ResultType = typeof(SmoothPathObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					var smoothPath = new SmoothPathObject3D();
					var itemClone = sceneItem.Clone();
					smoothPath.Children.Add(itemClone);
					smoothPath.Matrix = itemClone.Matrix;
					itemClone.Matrix = Matrix4X4.Identity;

					scene.SelectedItem = null;
					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { smoothPath }));
					scene.SelectedItem = smoothPath;

					smoothPath.Invalidate(InvalidateType.Properties);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("smooth_path.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "A path must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && sceneContext.Scene.SelectedItem is IPathObject,
			};
		}

		public static SceneOperation TranslateOperation()
		{
			return new SceneOperation("Translate")
			{
				OperationType = typeof(IObject3D),
				ResultType = typeof(TranslateObject3D),
				TitleGetter = () => "Translate".Localize(),
				Action = (sceneContext) =>
				{
					new TranslateObject3D().WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon(Path.Combine("ViewTransformControls", "translate.png"), 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		private static SceneOperation AdvancedArrayOperation()
		{
			return new SceneOperation("Advanced Array")
			{
				OperationType = typeof(IObject3D),
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
				OperationType = typeof(IObject3D),
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
				Icon = (theme) => StaticData.Instance.LoadIcon("align_left_dark.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
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
				Icon = (theme) => StaticData.Instance.LoadIcon("arrange_all.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				ShowInModifyMenu = (sceneContext) => false,
			};
		}

		private static bool BooleanCandidate(IObject3D selectedItem, bool includePaths)
		{
			if (selectedItem != null)
			{
				// mesh items
				if (selectedItem.VisibleMeshes().Count() > 1
					&& selectedItem.VisibleMeshes().All(i => IsMeshObject(i)))
				{
					return true;
				}

#if DEBUG
				// path items
				if (includePaths
					&& selectedItem.VisiblePaths().Count() > 1
					&& selectedItem.VisiblePaths().All(i => IsPathObject(i)))
				{
					return true;
				}
#endif
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

			OperationSourceContainerObject3D.TaskBuilder = (name, func) => ApplicationController.Instance.Tasks.Execute(name, null, func);

			registeredOperations = new List<SceneOperation>()
			{
				ArrangeAllPartsOperation(),
				new SceneSelectionSeparator(),
				LayFlatOperation(),
#if DEBUG
				RebuildOperation(),
#endif
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
						TwistOperation(),
						PlaneCutOperation(),
#if DEBUG
						FindSliceOperation(),
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
				new OperationGroup("Printing")
				{
					TitleGetter = () => "Printing".Localize(),
					Visible = OperationGroup.GetVisible("Path", false),
					Operations = new List<SceneOperation>()
					{
						ToggleSupportOperation(),
						ToggleWipeTowerOperation(),
						ToggleFuzzyOperation(),
					}
				},
				new OperationGroup("Design Apps")
				{
					TitleGetter = () => "Design Apps".Localize(),
					Visible = OperationGroup.GetVisible("Path", false),
					Operations = new List<SceneOperation>()
					{
						FitToBoundsOperation(),

#if DEBUG
						FitToCylinderOperation(),
#endif
						MakeComponentOperation(),
						EditComponentOperation(),
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

			// image operations
			PrimaryOperations.Add(typeof(ImageObject3D), new List<SceneOperation> { SceneOperations.ById("ImageConverter"), SceneOperations.ById("ImageToPath"), });

			// path operations
			PrimaryOperations.Add(typeof(ImageToPathObject3D_2), new List<SceneOperation>
			{
				SceneOperations.ById("LinearExtrude"), SceneOperations.ById("Revolve"), SceneOperations.ById("SmoothPath")
			});
			PrimaryOperations.Add(typeof(SmoothPathObject3D), new List<SceneOperation>
			{
				SceneOperations.ById("LinearExtrude"), SceneOperations.ById("Revolve"), SceneOperations.ById("InflatePath"), SceneOperations.ById("OutlinePath")
			});
			PrimaryOperations.Add(typeof(TextPathObject3D), new List<SceneOperation>
			{
				SceneOperations.ById("LinearExtrude"), SceneOperations.ById("Revolve"), SceneOperations.ById("InflatePath"), SceneOperations.ById("OutlinePath")
			});
			PrimaryOperations.Add(typeof(BoxPathObject3D), new List<SceneOperation>
			{
				SceneOperations.ById("LinearExtrude"), SceneOperations.ById("Revolve"), SceneOperations.ById("InflatePath"), SceneOperations.ById("OutlinePath")
			});
			PrimaryOperations.Add(typeof(InflatePathObject3D), new List<SceneOperation>
			{
				SceneOperations.ById("LinearExtrude"), SceneOperations.ById("Revolve"), SceneOperations.ById("OutlinePath")
			});
			PrimaryOperations.Add(typeof(OutlinePathObject3D), new List<SceneOperation>
			{
				SceneOperations.ById("LinearExtrude"), SceneOperations.ById("Revolve"), SceneOperations.ById("InflatePath")
			});
			PrimaryOperations.Add(typeof(LinearExtrudeObject3D), new List<SceneOperation>
			{
				SceneOperations.ById("AddBase")
			});

			// default operations
			PrimaryOperations.Add(typeof(Object3D), new List<SceneOperation> { SceneOperations.ById("Scale") });

			Icons.Add(typeof(ImageObject3D), (theme) => StaticData.Instance.LoadIcon("image_converter.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply());
			// Icons.Add(typeof(CubeObject3D), (theme) => StaticData.Instance.LoadIcon("image_converter.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply());
		}

		private static SceneOperation CombineOperation()
		{
			return new SceneOperation("Combine")
			{
				OperationType = typeof(IObject3D),
				ResultType = typeof(CombineObject3D_2),
				TitleGetter = () => "Combine".Localize(),
				Action = (sceneContext) =>
				{
#if DEBUG
					if (sceneContext.Scene.SelectedItem.VisiblePaths().Count() > 1)
					{
						new MergePathObject3D("Combine".Localize(), ClipperLib.ClipType.ctUnion).WrapSelectedItemAndSelect(sceneContext.Scene);
					}
					else
#endif
					{
						new CombineObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene);
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("combine.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 2 parts must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => BooleanCandidate(sceneContext.Scene.SelectedItem, true),
			};
		}

		private static SceneOperation CurveOperation()
		{
			return new SceneOperation("Curve")
			{
				OperationType = typeof(IObject3D),
				ResultType = typeof(CurveObject3D_3),
				TitleGetter = () => "Curve".Localize(),
				Action = (sceneContext) =>
				{
					var curve = new CurveObject3D_3();
					curve.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("curve.png", 16, 16).SetToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation DualExtrusionAlignOperation()
		{
			return new SceneOperation("Dual Extrusion Align")
			{
				OperationType = typeof(IObject3D),
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
				Icon = (theme) => StaticData.Instance.LoadIcon("dual_align.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
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
				OperationType = typeof(IObject3D),
				ResultType = typeof(FitToBoundsObject3D_3),
				TitleGetter = () => "Fit to Bounds".Localize(),
				Action = async (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					scene.SelectedItem = null;
					var fit = await FitToBoundsObject3D_3.Create(selectedItem.Clone());
					fit.MakeNameNonColliding();
					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { selectedItem }, new[] { fit }));
					scene.SelectedItem = fit;
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("fit.png", 16, 16).SetToColor(theme.TextColor),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private static SceneOperation FitToCylinderOperation()
		{
			return new SceneOperation("Fit to Cylinder")
			{
				OperationType = typeof(IObject3D),
				ResultType = typeof(FitToCylinderObject3D),
				TitleGetter = () => "Fit to Cylinder".Localize(),
				Action = async (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					scene.SelectedItem = null;
					var fit = await FitToCylinderObject3D.Create(selectedItem.Clone());
					fit.MakeNameNonColliding();
					scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { selectedItem }, new[] { fit }));
					scene.SelectedItem = fit;
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("fit.png", 16, 16).SetToColor(theme.TextColor),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private static SceneOperation GroupOperation()
		{
			return new SceneOperation("Group")
			{
				OperationType = typeof(SelectionGroupObject3D),
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
				OperationType = typeof(IObject3D),
				ResultType = typeof(HollowOutObject3D),
				TitleGetter = () => "Hollow Out".Localize(),
				Action = (sceneContext) =>
				{
					var hollowOut = new HollowOutObject3D();
					hollowOut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("hollow.png", 16, 16).SetToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation IntersectOperation()
		{
			return new SceneOperation("Intersect")
			{
				OperationType = typeof(IObject3D),
				ResultType = typeof(IntersectionObject3D_2),
				TitleGetter = () => "Intersect".Localize(),
				Action = (sceneContext) =>
				{
#if DEBUG
					if (sceneContext.Scene.SelectedItem.VisiblePaths().Count() > 1)
					{
						new MergePathObject3D("Intersect".Localize(), ClipperLib.ClipType.ctIntersection).WrapSelectedItemAndSelect(sceneContext.Scene);
					}
					else
#endif
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
			return item != null
				&& !(item is ImageObject3D)
				&& !(item is IPathObject);
		}

		private static bool IsPathObject(IObject3D item)
		{
			return item != null
				&& !(item is ImageObject3D)
				&& (item is IPathObject);
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
				Icon = (theme) => StaticData.Instance.LoadIcon("lay_flat.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
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
							var updateItems = SheetObject3D.SortAndLockUpdateItems(selectedItem.Parent, (item) =>
							{
								if (item == selectedItem || item.Parent == selectedItem)
								{
									// don't process this
									return false;
								}
								return true;
							}, false);

							SheetObject3D.SendInvalidateInRebuildOrder(updateItems, InvalidateType.Properties, null);
						}
						catch
						{
						}
					}
				},
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
				Icon = (theme) => StaticData.Instance.LoadIcon("update.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
			};
		}

		private static SceneOperation LinearArrayOperation()
		{
			return new SceneOperation("Linear Array")
			{
				OperationType = typeof(IObject3D),
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
				OperationType = typeof(IObject3D),
				ResultType = typeof(PinchObject3D_3),
				TitleGetter = () => "Pinch".Localize(),
				Action = (sceneContext) =>
				{
					var pinch = new PinchObject3D_3();
					pinch.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("pinch.png", 16, 16).SetToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation PlaneCutOperation()
		{
			return new SceneOperation("Plane Cut")
			{
				OperationType = typeof(IObject3D),
				ResultType = typeof(PlaneCutObject3D),
				TitleGetter = () => "Plane Cut".Localize(),
				Action = (sceneContext) =>
				{
					var cut = new PlaneCutObject3D();
					cut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("plane_cut.png", 16, 16).SetToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation FindSliceOperation()
		{
			return new SceneOperation("Find Slice")
			{
				OperationType = typeof(IObject3D),
				ResultType = typeof(PlaneCutObject3D),
				TitleGetter = () => "Find Slice".Localize(),
				Action = (sceneContext) =>
				{
					var cut = new FindSliceObject3D();
					cut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("plane_cut.png", 16, 16).SetToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation RadialArrayOperation()
		{
			return new SceneOperation("Radial Array")
			{
				OperationType = typeof(IObject3D),
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
				OperationType = typeof(IObject3D),
				ResultType = typeof(DecimateObject3D),
				TitleGetter = () => "Reduce".Localize(),
				Action = (sceneContext) =>
				{
					var hollowOut = new DecimateObject3D();
					hollowOut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("reduce.png", 16, 16).SetToColor(theme.TextColor),
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
				Icon = (theme) => StaticData.Instance.LoadIcon("remove.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
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
				OperationType = typeof(IObject3D),
				ResultType = typeof(RepairObject3D),
				TitleGetter = () => "Repair".Localize(),
				Action = (sceneContext) =>
				{
					var hollowOut = new RepairObject3D();
					hollowOut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("repair.png", 16, 16).SetToColor(theme.TextColor),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation SubtractAndReplaceOperation()
		{
			return new SceneOperation("Subtract & Replace")
			{
				OperationType = typeof(IObject3D),
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
				OperationType = typeof(IObject3D),
				ResultType = typeof(SubtractObject3D_2),
				TitleGetter = () => "Subtract".Localize(),
				Action = (sceneContext) =>
				{
#if DEBUG
					if (sceneContext.Scene.SelectedItem.VisiblePaths().Count() > 1)
					{
						new SubtractPathObject3D().WrapSelectedItemAndSelect(sceneContext.Scene);
					}
					else
#endif
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

		private static SceneOperation ToggleSupportOperation()
		{
			return new SceneOperation("Convert to Support")
			{
				TitleGetter = () => "Convert to Support".Localize(),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					if (selectedItem != null)
					{
						bool allAreSupport = false;
						if (selectedItem is SelectionGroupObject3D)
						{
							allAreSupport = selectedItem.Children.All(i => i.OutputType == PrintOutputTypes.Support);
						}
						else
						{
							allAreSupport = selectedItem.OutputType == PrintOutputTypes.Support;
						}

						scene.UndoBuffer.AddAndDo(new SetOutputType(selectedItem, allAreSupport ? PrintOutputTypes.Default : PrintOutputTypes.Support));
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("support.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation ToggleWipeTowerOperation()
		{
			return new SceneOperation("Convert to Wipe Tower")
			{
				TitleGetter = () => "Convert to Wipe Tower".Localize(),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					if (selectedItem != null)
					{
						bool allAreWipeTower = false;

						if (selectedItem is SelectionGroupObject3D)
						{
							allAreWipeTower = selectedItem.Children.All(i => i.OutputType == PrintOutputTypes.WipeTower);
						}
						else
						{
							allAreWipeTower = selectedItem.OutputType == PrintOutputTypes.WipeTower;
						}

						scene.UndoBuffer.AddAndDo(new SetOutputType(selectedItem, allAreWipeTower ? PrintOutputTypes.Default : PrintOutputTypes.WipeTower));
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("wipe_tower.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation ToggleFuzzyOperation()
		{
			return new SceneOperation("Convert to Fuzzy Region")
			{
				TitleGetter = () => "Convert to Fuzzy Region".Localize(),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					if (selectedItem != null)
					{
						bool allAreFuzzy = false;

						if (selectedItem is SelectionGroupObject3D)
						{
							allAreFuzzy = selectedItem.Children.All(i => i.OutputType == PrintOutputTypes.Fuzzy);
						}
						else
						{
							allAreFuzzy = selectedItem.OutputType == PrintOutputTypes.Fuzzy;
						}

						scene.UndoBuffer.AddAndDo(new SetOutputType(selectedItem, allAreFuzzy ? PrintOutputTypes.Default : PrintOutputTypes.Fuzzy));
					}
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("fuzzy_region.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				HelpTextGetter = () => "At least 1 part must be selected".Localize().Stars(),
				IsEnabled = (sceneContext) => IsMeshObject(sceneContext.Scene.SelectedItem),
			};
		}

		private static SceneOperation TwistOperation()
		{
			return new SceneOperation("Twist")
			{
				OperationType = typeof(IObject3D),
				ResultType = typeof(TwistObject3D),
				TitleGetter = () => "Twist".Localize(),
				Action = (sceneContext) =>
				{
					var twist = new TwistObject3D();
					twist.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (theme) => StaticData.Instance.LoadIcon("twist.png", 16, 16).SetToColor(theme.TextColor),
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
				Icon = (theme) => StaticData.Instance.LoadIcon("ungroup.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
				UiHint = "Shift + G".Localize(),
			};
		}
	}
}
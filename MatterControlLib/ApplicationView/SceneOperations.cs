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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

[assembly: InternalsVisibleTo("MatterControl.Tests")]
[assembly: InternalsVisibleTo("MatterControl.AutomationTests")]
[assembly: InternalsVisibleTo("CloudServices.Tests")]

namespace MatterHackers.MatterControl
{
	public class SceneOperations
	{
		private static SceneOperations _instance = null;

		private List<SceneOperation> registeredOperations;

		private SceneOperations()
		{
		}

		public static SceneOperations Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new SceneOperations();
					_instance.Build();
				}

				return _instance;
			}
		}

		public Dictionary<Type, Func<bool, ImageBuffer>> Icons { get; private set; }

		public IEnumerable<SceneOperation> RegisteredOperations => registeredOperations;

		public static SceneOperation AddBaseOperation()
		{
			return new SceneOperation()
			{
				OperationID = "AddBase",
				OperationType = typeof(IObject3D),
				TitleResolver = () => "Add Base".Localize(),
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
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("add_base.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A path must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is IPathObject),
			};
		}

		public static SceneOperation EditComponentOperation()
		{
			return new SceneOperation()
			{
				OperationID = "EditComponent",
				OperationType = typeof(IObject3D),
				TitleResolver = () => "Edit Component".Localize(),
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
					}
				},
				IsVisible = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					return sceneItem.Parent != null
						&& sceneItem.Parent.Parent == null
						&& sceneItem is ComponentObject3D componentObject
						&& componentObject.Finalized
						&& !componentObject.ProOnly;
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("scale_32x32.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A component must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is ImageObject3D),
			};
		}

		public static SceneOperation ImageConverterOperation()
		{
			return new SceneOperation()
			{
				OperationID = "ImageConverter",
				OperationType = typeof(ImageObject3D),
				TitleResolver = () => "Image Converter".Localize(),
				ResultType = typeof(ComponentObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					var imageObject = sceneItem.Clone() as ImageObject3D;

					var path = new ImageToPathObject3D();
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
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Children<SmoothPathObject3D>.Children<ImageToPathObject3D>.Children<ImageObject3D>",
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Height",
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Children<SmoothPathObject3D>.SmoothDistance",
								"$.Children<BaseObject3D>.Children<LinearExtrudeObject3D>.Children<SmoothPathObject3D>.Children<ImageToPathObject3D>",
								"$.Children<BaseObject3D>",
							}
					};

					component.Matrix = imageObject.Matrix;
					imageObject.Matrix = Matrix4X4.Identity;

					using (new SelectionMaintainer(scene))
					{
						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { component }));
					}

					// Invalidate image to kick off rebuild of ImageConverter stack
					imageObject.Invalidate(InvalidateType.Image);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("140.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*An image must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is ImageObject3D),
			};
		}

		public static SceneOperation ImageToPathOperation()
		{
			return new SceneOperation()
			{
				OperationID = "ImageToPath",
				OperationType = typeof(ImageObject3D),
				TitleResolver = () => "Image to Path".Localize(),
				ResultType = typeof(ImageToPathObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					if (sceneItem is IObject3D imageObject)
					{
						// TODO: make it look like this (and get rid of all the other stuff)
						// scene.Replace(sceneItem, new ImageToPathObject3D(sceneItem.Clone()));

						var path = new ImageToPathObject3D();

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
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("image_to_path.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*An image must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is ImageObject3D),
			};
		}

		public static SceneOperation InflatePathOperation()
		{
			return new SceneOperation()
			{
				OperationID = "InflatePath",
				OperationType = typeof(IPathObject),
				TitleResolver = () => "Inflate Path".Localize(),
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

					using (new SelectionMaintainer(scene))
					{
						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { inflatePath }));
					}

					inflatePath.Invalidate(InvalidateType.Properties);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("inflate_path.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A path must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is IPathObject),
			};
		}

		public static SceneOperation LinearExtrudeOperation()
		{
			return new SceneOperation()
			{
				OperationID = "LinearExtrude",
				OperationType = typeof(IPathObject),
				TitleResolver = () => "Linear Extrude".Localize(),
				ResultType = typeof(LinearExtrudeObject3D),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var sceneItem = scene.SelectedItem;
					if (sceneItem is IPathObject imageObject)
					{
						var extrude = new LinearExtrudeObject3D();

						var itemClone = sceneItem.Clone();
						extrude.Children.Add(itemClone);
						extrude.Matrix = itemClone.Matrix;
						itemClone.Matrix = Matrix4X4.Identity;

						using (new SelectionMaintainer(scene))
						{
							scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { extrude }));
						}

						extrude.Invalidate(InvalidateType.Properties);
					}
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("linear_extrude.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A path must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is IPathObject),
			};
		}

		public static SceneOperation MakeComponentOperation()
		{
			return new SceneOperation()
			{
				TitleResolver = () => "Make Component".Localize(),
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
					using (new SelectionMaintainer(scene))
					{
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
					}
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("component.png", 16, 16, invertIcon).SetPreMultiply(),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) =>
				{
					var sceneItem = sceneContext.Scene.SelectedItem;
					return sceneItem?.Parent != null
						&& sceneItem.Parent.Parent == null
						&& sceneItem.DescendantsAndSelf().All(d => !(d is ComponentObject3D));
				},
			};
		}

		public static SceneOperation MirrorOperation()
		{
			return new SceneOperation()
			{
				OperationID = "Mirror",
				OperationType = typeof(IObject3D),
				ResultType = typeof(MirrorObject3D_2),
				TitleResolver = () => "Mirror".Localize(),
				Action = (sceneContext) =>
				{
					new MirrorObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("mirror_32x32.png", 16, 16, invertIcon).SetPreMultiply(),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		public static SceneOperation OutlinePathOperation()
		{
			return new SceneOperation()
			{
				OperationID = "OutlinePath",
				OperationType = typeof(IPathObject),
				TitleResolver = () => "Outline Path".Localize(),
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

					using (new SelectionMaintainer(scene))
					{
						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { outlinePath }));
					}

					outlinePath.Invalidate(InvalidateType.Properties);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("outline.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A path must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is IPathObject),
			};
		}

		public static SceneOperation RotateOperation()
		{
			return new SceneOperation()
			{
				OperationID = "Rotate",
				OperationType = typeof(IObject3D),
				ResultType = typeof(RotateObject3D_2),
				TitleResolver = () => "Rotate".Localize(),
				Action = (sceneContext) =>
				{
					new RotateObject3D_2().WrapItems(sceneContext.Scene.GetSelectedItems(), sceneContext.Scene.UndoBuffer);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon(Path.Combine("ViewTransformControls", "rotate.png"), 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		public static SceneOperation ScaleOperation()
		{
			return new SceneOperation()
			{
				OperationID = "Scale",
				OperationType = typeof(IObject3D),
				ResultType = typeof(ScaleObject3D),
				TitleResolver = () => "Scale".Localize(),
				Action = (sceneContext) =>
				{
					new ScaleObject3D().WrapItems(sceneContext.Scene.GetSelectedItems(), sceneContext.Scene.UndoBuffer);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("scale_32x32.png", 16, 16, invertIcon).SetPreMultiply(),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		public static SceneOperation SmoothPathOperation()
		{
			return new SceneOperation()
			{
				OperationID = "SmoothPath",
				OperationType = typeof(IPathObject),
				TitleResolver = () => "Smooth Path".Localize(),
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

					using (new SelectionMaintainer(scene))
					{
						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { sceneItem }, new[] { smoothPath }));
					}

					smoothPath.Invalidate(InvalidateType.Properties);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("smooth_path.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A path must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is IPathObject),
			};
		}

		public static SceneOperation TranslateOperation()
		{
			return new SceneOperation()
			{
				OperationID = "Translate",
				OperationType = typeof(IObject3D),
				ResultType = typeof(TranslateObject3D),
				TitleResolver = () => "Translate".Localize(),
				Action = (sceneContext) =>
				{
					new TranslateObject3D().WrapItems(sceneContext.Scene.GetSelectedItems(), sceneContext.Scene.UndoBuffer);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon(Path.Combine("ViewTransformControls", "translate.png"), 16, 16, invertIcon).SetPreMultiply(),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		/// <summary>
		/// Register the given SceneSelectionOperation
		/// </summary>
		/// <param name="operation">The action to register</param>
		public void RegisterSceneOperation(SceneOperation operation)
		{
			if (operation.OperationType != null)
			{
				Icons.Add(operation.OperationType, operation.Icon);
			}

			registeredOperations.Add(operation);
		}

		private SceneOperation AdvancedArrayOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(ArrayAdvancedObject3D),
				TitleResolver = () => "Advanced Array".Localize(),
				Action = (sceneContext) =>
				{
					var array = new ArrayAdvancedObject3D
					{
						Name = "" // this will get the default behavior of showing the child's name + a count
					};
					array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("array_advanced.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A single part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private SceneOperation AlignOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(AlignObject3D),
				TitleResolver = () => "Align".Localize(),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					var align = new AlignObject3D();
					align.AddSelectionAsChildren(scene, selectedItem);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("align_left_dark.png", 16, 16, invertIcon).SetPreMultiply(),
				HelpTextResolver = () => "*At least 2 parts must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem is SelectionGroupObject3D,
			};
		}

		private SceneOperation ArrangeAllPartsOperation()
		{
			return new SceneOperation()
			{
				OperationID = "ArrangeAllPartsOperation",
				TitleResolver = () => "Arrange All Parts".Localize(),
				Action = async (sceneContext) =>
				{
					await sceneContext.Scene.AutoArrangeChildren(new Vector3(sceneContext.BedCenter)).ConfigureAwait(false);
				},
				HelpTextResolver = () => "*No part to arrange*".Localize(),
				IsEnabled = (sceneContext) =>
				{
					return sceneContext.EditableScene && sceneContext.Scene.VisibleMeshes().Any();
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("arrange_all.png", 16, 16, invertIcon).SetPreMultiply(),
			};
		}

		private void Build()
		{
			OperationSourceContainerObject3D.TaskBuilder = (name, func) => ApplicationController.Instance.Tasks.Execute(name, null, func);

			registeredOperations = new List<SceneOperation>()
			{
				ArrangeAllPartsOperation(),
				new SceneSelectionSeparator(),
				LayFlatOperation(),
				GroupOperation(),
				UngroupOperation(),
				new SceneSelectionSeparator(),
				DuplicateOperation(),
				RemoveOperation(),
				new SceneSelectionSeparator(),
				new OperationGroup("Adjust")
				{
					Collapse = true,
					TitleResolver = () => "Adjust".Localize(),
					StickySelection = true,
					InitialSelection = 2,
					Operations = new List<SceneOperation>()
					{
						TranslateOperation(),
						RotateOperation(),
						ScaleOperation(),
						MirrorOperation(),
					}
				},
				new OperationGroup("Align")
				{
					Collapse = true,
					TitleResolver = () => "Align".Localize(),
					StickySelection = true,
					Operations = new List<SceneOperation>()
					{
						AlignOperation(),
						DualExtrusionAlignOperation(),
					},
				},
				new OperationGroup("Form")
				{
					Collapse = true,
					TitleResolver = () => "Form".Localize(),
					StickySelection = true,
					Operations = new List<SceneOperation>()
					{
						CurveOperation(),
						PinchOperation(),
						TwistOperation(),
#if DEBUG // don't make this part of the distribution until it is working
						PlaneCutOperation(),
#endif
						HollowOutOperation(),
					}
				},
				new OperationGroup("Boolean")
				{
					Collapse = true,
					TitleResolver = () => "Boolean".Localize(),
					StickySelection = true,
					Operations = new List<SceneOperation>()
					{
						CombineOperation(),
						SubtractOperation(),
						IntersectOperation(),
						SubtractAndReplaceOperation(),
					}
				},
				new OperationGroup("Array")
				{
					Collapse = true,
					TitleResolver = () => "Array".Localize(),
					StickySelection = true,
					Operations = new List<SceneOperation>()
					{
						LinearArrayOperation(),
						RadialArrayOperation(),
						AdvancedArrayOperation(),
					}
				},
				new OperationGroup("Mesh")
				{
					Collapse = true,
					TitleResolver = () => "Mesh".Localize(),
					StickySelection = true,
					Operations = new List<SceneOperation>()
					{
						ReduceOperation(),
						RepairOperation(),
					}
				},
				new OperationGroup("Printing")
				{
					Collapse = true,
					TitleResolver = () => "Printing".Localize(),
					StickySelection = true,
					Operations = new List<SceneOperation>()
					{
						ToggleWipeTowerOperation(),
						ToggleSupportOperation(),
					}
				},
				new OperationGroup("Design Apps")
				{
					Collapse = true,
					TitleResolver = () => "Design Apps".Localize(),
					StickySelection = true,
					Operations = new List<SceneOperation>()
					{
						FitToBoundsOperation(),

#if DEBUG
						FitToCylinderOperation(),
#endif
						MakeComponentOperation(),
					},
				},
			};

			Icons = new Dictionary<Type, Func<bool, ImageBuffer>>();

			foreach (var operation in registeredOperations)
			{
				if (operation.OperationType != null)
				{
					Icons.Add(operation.OperationType, operation.Icon);
				}
			}

			// TODO: Use custom selection group icon if reusing group icon seems incorrect
			//
			// Explicitly register SelectionGroup icon
			if (Icons.TryGetValue(typeof(GroupObject3D), out Func<bool, ImageBuffer> groupIconSource))
			{
				Icons.Add(typeof(SelectionGroupObject3D), groupIconSource);
			}

			Icons.Add(typeof(ImageObject3D), (invertIcon) => AggContext.StaticData.LoadIcon("140.png", 16, 16, invertIcon));
		}

		private SceneOperation CombineOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(CombineObject3D_2),
				TitleResolver = () => "Combine".Localize(),
				Action = (sceneContext) => new CombineObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene),
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("combine.png", 16, 16, !invertIcon).SetPreMultiply(),
				HelpTextResolver = () => "*At least 2 parts must be selected*".Localize(),
				IsEnabled = (sceneContext) =>
				{
					var selectedItem = sceneContext.Scene.SelectedItem;
					return selectedItem != null && selectedItem.VisibleMeshes().Count() > 1;
				},
			};
		}

		private SceneOperation CurveOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(CurveObject3D_2),
				TitleResolver = () => "Curve".Localize(),
				Action = (sceneContext) =>
				{
					var curve = new CurveObject3D_2();
					curve.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("curve.png", 16, 16, invertIcon),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		private SceneOperation DualExtrusionAlignOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(AlignObject3D),
				TitleResolver = () => "Dual Extrusion Align".Localize(),
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
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("dual_align.png", 16, 16, invertIcon).SetPreMultiply(),
				HelpTextResolver = () => "*At least 2 parts must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem is SelectionGroupObject3D,
			};
		}

		private SceneOperation DuplicateOperation()
		{
			return new SceneOperation()
			{
				TitleResolver = () => "Duplicate".Localize(),
				Action = (sceneContext) => sceneContext.DuplicateItem(5),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("duplicate.png", 16, 16).SetPreMultiply(),
			};
		}

		private SceneOperation FitToBoundsOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(FitToBoundsObject3D_2),
				TitleResolver = () => "Fit to Bounds".Localize(),
				Action = async (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					using (new SelectionMaintainer(scene))
					{
						var fit = await FitToBoundsObject3D_2.Create(selectedItem.Clone());
						fit.MakeNameNonColliding();

						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { selectedItem }, new[] { fit }));
					}
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("fit.png", 16, 16, invertIcon),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private SceneOperation FitToCylinderOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(FitToCylinderObject3D),
				TitleResolver = () => "Fit to Cylinder".Localize(),
				Action = async (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					using (new SelectionMaintainer(scene))
					{
						var fit = await FitToCylinderObject3D.Create(selectedItem.Clone());
						fit.MakeNameNonColliding();

						scene.UndoBuffer.AddAndDo(new ReplaceCommand(new[] { selectedItem }, new[] { fit }));
					}
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("fit.png", 16, 16, invertIcon),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private SceneOperation GroupOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(GroupObject3D),

				TitleResolver = () => "Group".Localize(),
				Action = (sceneContext) =>
				{
					var scene = sceneContext.Scene;
					var selectedItem = scene.SelectedItem;
					scene.SelectedItem = null;

					var newGroup = new GroupObject3D();
					// When grouping items, move them to be centered on their bounding box
					newGroup.Children.Modify((gChildren) =>
					{
						selectedItem.Clone().Children.Modify((sChildren) =>
						{
							var center = selectedItem.GetAxisAlignedBoundingBox().Center;

							foreach (var child in sChildren)
							{
								child.Translate(-center.X, -center.Y, 0);
								gChildren.Add(child);
							}

							newGroup.Translate(center.X, center.Y, 0);
						});
					});

					scene.UndoBuffer.AddAndDo(new ReplaceCommand(selectedItem.Children.ToList(), new[] { newGroup }));

					newGroup.MakeNameNonColliding();

					scene.SelectedItem = newGroup;
				},
				HelpTextResolver = () => "*At least 2 parts must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene is InteractiveScene scene
					&& scene.SelectedItem != null
					&& scene.SelectedItem is SelectionGroupObject3D
					&& scene.SelectedItem.Children.Count > 1,
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("group.png", 16, 16).SetPreMultiply(),
			};
		}

		private SceneOperation HollowOutOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(HollowOutObject3D),
				TitleResolver = () => "Hollow Out".Localize(),
				Action = (sceneContext) =>
				{
					var hollowOut = new HollowOutObject3D();
					hollowOut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("hollow.png", 16, 16, invertIcon),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		private SceneOperation IntersectOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(IntersectionObject3D_2),
				TitleResolver = () => "Intersect".Localize(),
				Action = (sceneContext) => new IntersectionObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene),
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("intersect.png", 16, 16),
				HelpTextResolver = () => "*At least 2 parts must be selected*".Localize(),
				IsEnabled = (sceneContext) =>
				{
					var selectedItem = sceneContext.Scene.SelectedItem;
					return selectedItem != null && selectedItem.VisibleMeshes().Count() > 1;
				},
			};
		}

		private SceneOperation LayFlatOperation()
		{
			return new SceneOperation()
			{
				TitleResolver = () => "Lay Flat".Localize(),
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
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("lay_flat.png", 16, 16, invertIcon).SetPreMultiply(),
			};
		}

		private SceneOperation LinearArrayOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(ArrayLinearObject3D),
				TitleResolver = () => "Linear Array".Localize(),
				Action = (sceneContext) =>
				{
					var array = new ArrayLinearObject3D
					{
						Name = "" // this will get the default behavior of showing the child's name + a count
					};
					array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("array_linear.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A single part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private SceneOperation PinchOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(PinchObject3D_2),
				TitleResolver = () => "Pinch".Localize(),
				Action = (sceneContext) =>
				{
					var pinch = new PinchObject3D_2();
					pinch.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("pinch.png", 16, 16, invertIcon),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		private SceneOperation PlaneCutOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(PlaneCutObject3D),
				TitleResolver = () => "Plane Cut".Localize(),
				Action = (sceneContext) =>
				{
					var cut = new PlaneCutObject3D();
					cut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("plane_cut.png", 16, 16, invertIcon),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		private SceneOperation RadialArrayOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(ArrayRadialObject3D),
				TitleResolver = () => "Radial Array".Localize(),
				Action = (sceneContext) =>
				{
					var array = new ArrayRadialObject3D
					{
						Name = "" // this will get the default behavior of showing the child's name + a count
					};
					array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("array_radial.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A single part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private SceneOperation ReduceOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(DecimateObject3D),
				TitleResolver = () => "Reduce".Localize(),
				Action = (sceneContext) =>
				{
					var hollowOut = new DecimateObject3D();
					hollowOut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("reduce.png", 16, 16, invertIcon),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		private SceneOperation RemoveOperation()
		{
			return new SceneOperation()
			{
				TitleResolver = () => "Remove".Localize(),
				Action = (sceneContext) => sceneContext.Scene.DeleteSelection(),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("remove.png", 16, 16, !invertIcon).SetPreMultiply(),
			};
		}

		private SceneOperation RepairOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(RepairObject3D),
				TitleResolver = () => "Repair".Localize(),
				Action = (sceneContext) =>
				{
					var hollowOut = new RepairObject3D();
					hollowOut.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("repair.png", 16, 16, invertIcon),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		private SceneOperation SubtractAndReplaceOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(SubtractAndReplaceObject3D_2),
				TitleResolver = () => "Subtract & Replace".Localize(),
				Action = (sceneContext) => new SubtractAndReplaceObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene),
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("subtract_and_replace.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*At least 2 parts must be selected*".Localize(),
				IsEnabled = (sceneContext) =>
				{
					var selectedItem = sceneContext.Scene.SelectedItem;
					return selectedItem != null && selectedItem.VisibleMeshes().Count() > 1;
				},
			};
		}

		private SceneOperation SubtractOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(SubtractObject3D_2),
				TitleResolver = () => "Subtract".Localize(),
				Action = (sceneContext) => new SubtractObject3D_2().WrapSelectedItemAndSelect(sceneContext.Scene),
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("subtract.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*At least 2 parts must be selected*".Localize(),
				IsEnabled = (sceneContext) =>
				{
					var selectedItem = sceneContext.Scene.SelectedItem;
					return selectedItem != null && selectedItem.VisibleMeshes().Count() > 1;
				},
			};
		}

		private SceneOperation ToggleSupportOperation()
		{
			return new SceneOperation()
			{
				TitleResolver = () => "Toggle Support".Localize(),
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
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("support.png", 16, 16, invertIcon).SetPreMultiply(),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		private SceneOperation ToggleWipeTowerOperation()
		{
			return new SceneOperation()
			{
				TitleResolver = () => "Toggle Wipe Tower".Localize(),
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
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("wipe_tower.png", 16, 16, invertIcon).SetPreMultiply(),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		private SceneOperation TwistOperation()
		{
			return new SceneOperation()
			{
				OperationType = typeof(TwistObject3D),
				TitleResolver = () => "Twist".Localize(),
				Action = (sceneContext) =>
				{
					var curve = new TwistObject3D();
					curve.WrapSelectedItemAndSelect(sceneContext.Scene);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("twist.png", 16, 16, invertIcon),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
			};
		}

		private SceneOperation UngroupOperation()
		{
			return new SceneOperation()
			{
				TitleResolver = () => "Ungroup".Localize(),
				Action = (sceneContext) => sceneContext.Scene.UngroupSelection(),
				HelpTextResolver = () => "*A single part must be selected*".Localize(),
				IsEnabled = (sceneContext) =>
				{
					var selectedItem = sceneContext.Scene.SelectedItem;
					if (selectedItem != null)
					{
						return selectedItem is GroupObject3D
							|| selectedItem.GetType() == typeof(Object3D)
							|| selectedItem.CanFlatten;
					}

					return false;
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("ungroup.png", 16, 16, !invertIcon).SetPreMultiply(),
			};
		}
	}
}
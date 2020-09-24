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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MatterControl.Tests")]
[assembly: InternalsVisibleTo("MatterControl.AutomationTests")]
[assembly: InternalsVisibleTo("CloudServices.Tests")]

namespace MatterHackers.MatterControl
{
	public class SceneOperations
	{
		private static SceneOperations _instance = null;

		private List<SceneSelectionOperation> registeredOperations;

		private Dictionary<Type, Func<bool, ImageBuffer>> iconsByType;

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

		private void Build()
		{
			OperationSourceContainerObject3D.TaskBuilder = (name, func) => ApplicationController.Instance.Tasks.Execute(name, null, func);

			registeredOperations = new List<SceneSelectionOperation>()
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
					Operations = new List<SceneSelectionOperation>()
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
					Operations = new List<SceneSelectionOperation>()
					{
						AlignOperation(),
						DualExtrusionAlignOperation(),
					},
				},
				new OperationGroup("Modify")
				{
					TitleResolver = () => "Modify".Localize(),
					StickySelection = true,
					Operations = new List<SceneSelectionOperation>()
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
					TitleResolver = () => "Boolean".Localize(),
					StickySelection = true,
					Operations = new List<SceneSelectionOperation>()
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
					Operations = new List<SceneSelectionOperation>()
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
					Operations = new List<SceneSelectionOperation>()
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
					Operations = new List<SceneSelectionOperation>()
					{
						ToggleWipeTowerOperation(),
						ToggleSupportOperation(),
					}
				},
				new OperationGroup("Design Apps")
				{
					TitleResolver = () => "Design Apps".Localize(),
					StickySelection = true,
					Operations = new List<SceneSelectionOperation>()
					{
						FitToBoundsOperation(),

#if DEBUG
						FitToCylinderOperation(),
#endif
						MakeComponentOperation(),
					},
				},
			};

			iconsByType = new Dictionary<Type, Func<bool, ImageBuffer>>();

			foreach (var operation in registeredOperations)
			{
				if (operation.OperationType != null)
				{
					iconsByType.Add(operation.OperationType, operation.Icon);
				}
			}

			// TODO: Use custom selection group icon if reusing group icon seems incorrect
			//
			// Explicitly register SelectionGroup icon
			if (iconsByType.TryGetValue(typeof(GroupObject3D), out Func<bool, ImageBuffer> groupIconSource))
			{
				iconsByType.Add(typeof(SelectionGroupObject3D), groupIconSource);
			}

			iconsByType.Add(typeof(ImageObject3D), (invertIcon) => AggContext.StaticData.LoadIcon("140.png", 16, 16, invertIcon));
		}

		private SceneSelectionOperation MakeComponentOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation ToggleSupportOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation ToggleWipeTowerOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation MirrorOperation()
		{
			return new SceneSelectionOperation()
			{
				OperationType = typeof(MirrorObject3D_2),
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

		private SceneSelectionOperation ScaleOperation()
		{
			return new SceneSelectionOperation()
			{
				OperationType = typeof(ScaleObject3D),
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

		private SceneSelectionOperation RotateOperation()
		{
			return new SceneSelectionOperation()
			{
				OperationType = typeof(RotateObject3D_2),
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

		private SceneSelectionOperation TranslateOperation()
		{
			return new SceneSelectionOperation()
			{
				OperationType = typeof(TranslateObject3D),
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

		private SceneSelectionOperation FitToCylinderOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation FitToBoundsOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation DualExtrusionAlignOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation RepairOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation HollowOutOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation CurveOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation TwistOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation PlaneCutOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation GroupOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation UngroupOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation DuplicateOperation()
		{
			return new SceneSelectionOperation()
			{
				TitleResolver = () => "Duplicate".Localize(),
				Action = (sceneContext) => sceneContext.DuplicateItem(5),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("duplicate.png", 16, 16).SetPreMultiply(),
			};
		}

		private SceneSelectionOperation RemoveOperation()
		{
			return new SceneSelectionOperation()
			{
				TitleResolver = () => "Remove".Localize(),
				Action = (sceneContext) => sceneContext.Scene.DeleteSelection(),
				HelpTextResolver = () => "*At least 1 part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null,
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("remove.png", 16, 16, !invertIcon).SetPreMultiply(),
			};
		}

		private SceneSelectionOperation AlignOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation LayFlatOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation ArrangeAllPartsOperation()
		{
			return new SceneSelectionOperation()
			{
				Id = "ArrangeAllPartsOperation",
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

		private SceneSelectionOperation SubtractAndReplaceOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation CombineOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation AdvancedArrayOperation()
		{
			return new SceneSelectionOperation()
			{
				OperationType = typeof(ArrayAdvancedObject3D),
				TitleResolver = () => "Advanced Array".Localize(),
				Action = (sceneContext) =>
				{
					var array = new ArrayAdvancedObject3D();
					array.Name = ""; // this will get the default behavior of showing the child's name + a count
					array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("array_advanced.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A single part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private SceneSelectionOperation LinearArrayOperation()
		{
			return new SceneSelectionOperation()
			{
				OperationType = typeof(ArrayLinearObject3D),
				TitleResolver = () => "Linear Array".Localize(),
				Action = (sceneContext) =>
				{
					var array = new ArrayLinearObject3D();
					array.Name = ""; // this will get the default behavior of showing the child's name + a count
					array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("array_linear.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A single part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private SceneSelectionOperation RadialArrayOperation()
		{
			return new SceneSelectionOperation()
			{
				OperationType = typeof(ArrayRadialObject3D),
				TitleResolver = () => "Radial Array".Localize(),
				Action = (sceneContext) =>
				{
					var array = new ArrayRadialObject3D();
					array.Name = ""; // this will get the default behavior of showing the child's name + a count
					array.AddSelectionAsChildren(sceneContext.Scene, sceneContext.Scene.SelectedItem);
				},
				Icon = (invertIcon) => AggContext.StaticData.LoadIcon("array_radial.png", 16, 16).SetPreMultiply(),
				HelpTextResolver = () => "*A single part must be selected*".Localize(),
				IsEnabled = (sceneContext) => sceneContext.Scene.SelectedItem != null && !(sceneContext.Scene.SelectedItem is SelectionGroupObject3D),
			};
		}

		private SceneSelectionOperation IntersectOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation SubtractOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation ReduceOperation()
		{
			return new SceneSelectionOperation()
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

		private SceneSelectionOperation PinchOperation()
		{
			return new SceneSelectionOperation()
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

		/// <summary>
		/// Register the given SceneSelectionOperation
		/// </summary>
		/// <param name="operation">The action to register</param>
		public void RegisterSceneOperation(SceneSelectionOperation operation)
		{
			if (operation.OperationType != null)
			{
				Icons.Add(operation.OperationType, operation.Icon);
			}

			registeredOperations.Add(operation);
		}

		public IEnumerable<SceneSelectionOperation> RegisteredOperations => registeredOperations;

		public Dictionary<Type, Func<bool, ImageBuffer>> Icons => iconsByType;
	}
}
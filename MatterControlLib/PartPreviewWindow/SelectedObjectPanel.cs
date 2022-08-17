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
using System.Threading;
using System.Threading.Tasks;
using JsonPath;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.SlicerConfiguration;
using static JsonPath.JsonPathContext.ReflectionValueSystem;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SelectedObjectPanel : FlowLayoutWidget, IContentStore
	{
		private IObject3D item = new Object3D();

		private readonly ThemeConfig theme;
		private readonly ISceneContext sceneContext;
		private readonly SectionWidget editorSectionWidget;

		private readonly GuiWidget editorPanel;

		private readonly string editorTitle = "Properties".Localize();

		public SelectedObjectPanel(View3DWidget view3DWidget, ISceneContext sceneContext, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Top | VAnchor.Fit;
			this.Padding = 0;
			this.theme = theme;
			this.sceneContext = sceneContext;

			var toolbar = new LeftClipFlowLayoutWidget()
			{
				BackgroundColor = theme.BackgroundColor,
				Padding = theme.ToolbarPadding,
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit
			};

			scene = sceneContext.Scene;

			// put in the container for dynamic actions
			primaryActionsPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Center | VAnchor.Fit
			};

			toolbar.AddChild(primaryActionsPanel);

			// put in a make permanent button
			var icon = StaticData.Instance.LoadIcon("apply.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply();
			applyButton = new ThemedIconButton(icon, theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Apply".Localize(),
				Enabled = true
			};
			applyButton.Click += (s, e) =>
			{
				if (this.item.CanApply)
				{
					var item = this.item;
					using (new DataConverters3D.SelectionMaintainer(view3DWidget.Scene))
					{
						item.Apply(view3DWidget.Scene.UndoBuffer);
					}
				}
				else
				{
					// try to ungroup it
					sceneContext.Scene.UngroupSelection();
				}
			};
			toolbar.AddChild(applyButton);

			// put in a remove button
			cancelButton = new ThemedIconButton(StaticData.Instance.LoadIcon("cancel.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(), theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Cancel".Localize(),
				Enabled = scene.SelectedItem != null
			};
			cancelButton.Click += (s, e) =>
			{
				var item = this.item;
				using (new DataConverters3D.SelectionMaintainer(view3DWidget.Scene))
				{
					item.Cancel(view3DWidget.Scene.UndoBuffer);
				}
			};
			toolbar.AddChild(cancelButton);

			overflowButton = new PopupMenuButton("Action".Localize(), theme)
			{
				Enabled = scene.SelectedItem != null,
				DrawArrow = true,
			};
			overflowButton.ToolTipText = "Object Actions".Localize();
			overflowButton.DynamicPopupContent = () =>
			{
				return ApplicationController.Instance.GetModifyMenu(view3DWidget.sceneContext);
			};
			toolbar.AddChild(overflowButton);

			editorPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Name = "editorPanel",
			};

			// Wrap editorPanel with scrollable container
			var scrollableWidget = new ScrollableWidget(true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			scrollableWidget.AddChild(editorPanel);
			scrollableWidget.ScrollArea.HAnchor = HAnchor.Stretch;

			editorSectionWidget = new SectionWidget(editorTitle, scrollableWidget, theme, toolbar, expandingContent: false, defaultExpansion: true, setContentVAnchor: false)
			{
				VAnchor = VAnchor.Stretch
			};
			this.AddChild(editorSectionWidget);

			this.ContentPanel = editorPanel;

			// Register listeners
			scene.SelectionChanged += Scene_SelectionChanged;
		}

		public GuiWidget ContentPanel { get; set; }

		private readonly JsonPathContext pathGetter = new JsonPathContext();
		private readonly ThemedIconButton applyButton;
		private readonly ThemedIconButton cancelButton;
		private readonly PopupMenuButton overflowButton;
		private readonly InteractiveScene scene;
		private readonly FlowLayoutWidget primaryActionsPanel;

		public void SetActiveItem(ISceneContext sceneContext)
		{
			var selectedItem = sceneContext?.Scene?.SelectedItem;
			if (this.item == selectedItem)
			{
				return;
			}

			this.item = selectedItem;
			editorPanel.CloseChildren();

			// Allow caller to clean up with passing null for selectedItem
			if (item == null)
			{
				editorSectionWidget.Text = editorTitle;
				return;
			}

			var selectedItemType = selectedItem.GetType();

			primaryActionsPanel.RemoveChildren();

			IEnumerable<SceneOperation> primaryActions;

			if ((primaryActions = SceneOperations.GetPrimaryOperations(selectedItemType)) == null)
			{
				primaryActions = new List<SceneOperation>();
			}
			else
			{
				// Loop over primary actions creating a button for each
				foreach (var primaryAction in primaryActions)
				{
					// TODO: Run visible/enable rules on actions, conditionally add/enable as appropriate
					var button = new ThemedIconButton(primaryAction.Icon(theme), theme)
					{
						// Name = namedAction.Title + " Button",
						ToolTipText = primaryAction.Title,
						Margin = theme.ButtonSpacing,
						BackgroundColor = theme.ToolbarButtonBackground,
						HoverColor = theme.ToolbarButtonHover,
						MouseDownColor = theme.ToolbarButtonDown,
					};

					button.Click += (s, e) =>
					{
						primaryAction.Action.Invoke(sceneContext);
					};

					primaryActionsPanel.AddChild(button);
				}
			}

			if (primaryActionsPanel.Children.Any())
			{
				// add in a separator from the apply and cancel buttons
				primaryActionsPanel.AddChild(new ToolbarSeparator(theme.GetBorderColor(50), theme.SeparatorMargin));
			}

			editorSectionWidget.Text = selectedItem.Name ?? selectedItemType.Name;

			HashSet<IObject3DEditor> mappedEditors = ApplicationController.Instance.Extensions.GetEditorsForType(selectedItemType);

			var undoBuffer = sceneContext.Scene.UndoBuffer;

			void GetNextSelectionColor(Action<Color> setColor)
			{
				var scene = sceneContext.Scene;
				var startingSelection = scene.SelectedItem;
				CancellationTokenSource cancellationToken = null;

				void SelectionChanged(object s, EventArgs e)
				{
					var selection = scene.SelectedItem;
					if (selection != null)
					{
						setColor?.Invoke(selection.WorldColor());
						scene.SelectionChanged -= SelectionChanged;
						cancellationToken?.Cancel();
						scene.SelectedItem = startingSelection;
					}
				}

				var durationSeconds = 20;

				ApplicationController.Instance.Tasks.Execute("Select an object to copy its color".Localize(),
					null,
					(progress, cancellationTokenIn) =>
					{
						cancellationToken = cancellationTokenIn;
						var time = UiThread.CurrentTimerMs;
						var status = new ProgressStatus();
						while (UiThread.CurrentTimerMs < time + durationSeconds * 1000
							&& !cancellationToken.IsCancellationRequested)
						{
							Thread.Sleep(30);
							status.Progress0To1 = (UiThread.CurrentTimerMs - time) / 1000.0 / durationSeconds;
							progress.Report(status);
						}

						scene.SelectionChanged -= SelectionChanged;
						return Task.CompletedTask;
					});

				scene.SelectionChanged += SelectionChanged;
			}

			if (!(selectedItem.GetType().GetCustomAttributes(typeof(HideMeterialAndColor), true).FirstOrDefault() is HideMeterialAndColor))
			{
				var firstDetectedColor = selectedItem.VisibleMeshes()?.FirstOrDefault()?.WorldColor();
				var worldColor = Color.White;
				if (firstDetectedColor != null)
                {
					worldColor = firstDetectedColor.Value;
				}

				// put in a color edit field
				var colorField = new ColorField(theme, worldColor, GetNextSelectionColor, true);
				colorField.Initialize(0);
				colorField.ValueChanged += (s, e) =>
				{
					if (selectedItem.Color != colorField.Color)
					{
						if (colorField.Color == Color.Transparent)
						{
							undoBuffer.AddAndDo(new ChangeColor(selectedItem, colorField.Color, PrintOutputTypes.Default));
						}
                        else
                        {
							undoBuffer.AddAndDo(new ChangeColor(selectedItem, colorField.Color, PrintOutputTypes.Solid));
						}
					}
				};

				ColorButton holeButton = null;
				var solidButton = colorField.Content.Descendants<ColorButton>().FirstOrDefault();
				GuiWidget otherContainer = null;
				TextWidget otherText = null;
				GuiWidget holeContainer = null;
				GuiWidget solidContainer = null;
				void SetOtherOutputSelection(string text)
                {
					otherText.Text = text;
					otherContainer.Visible = true;
					holeContainer.BackgroundOutlineWidth = 0;
					holeButton.BackgroundOutlineWidth = 1;

					solidContainer.BackgroundOutlineWidth = 0;
					solidButton.BackgroundOutlineWidth = 1;
				}

				var scaledButtonSize = 24 * GuiWidget.DeviceScale;
				void SetButtonStates()
				{
                    switch (selectedItem.OutputType)
                    {
                        case PrintOutputTypes.Hole:
							holeContainer.BackgroundOutlineWidth = 1;
							holeButton.BackgroundOutlineWidth = 2;
							holeButton.BackgroundRadius = scaledButtonSize / 2 - 1;

							solidContainer.BackgroundOutlineWidth = 0;
							solidButton.BackgroundOutlineWidth = 1;
							solidButton.BackgroundRadius = scaledButtonSize / 2;
							otherContainer.Visible = false;
							break;

                        case PrintOutputTypes.Default:
                        case PrintOutputTypes.Solid:
							holeContainer.BackgroundOutlineWidth = 0;
							holeButton.BackgroundOutlineWidth = 1;
							holeButton.BackgroundRadius = scaledButtonSize / 2;

							solidContainer.BackgroundOutlineWidth = 1;
							solidButton.BackgroundOutlineWidth = 2;
							solidButton.BackgroundRadius = scaledButtonSize / 2 - 1;
							otherContainer.Visible = false;
							break;

                        case PrintOutputTypes.Support:
							SetOtherOutputSelection("Support".Localize());
							break;

                        case PrintOutputTypes.WipeTower:
							SetOtherOutputSelection("Wipe Tower".Localize());
							break;

                        case PrintOutputTypes.Fuzzy:
							SetOtherOutputSelection("Fuzzy".Localize());
							break;
                    }
                }

				void SetToSolid()
                {
					// make sure the render mode is set to shaded or outline
					switch(sceneContext.ViewState.RenderType)
                    {
                        case RenderOpenGl.RenderTypes.Shaded:
                        case RenderOpenGl.RenderTypes.Outlines:
						case RenderOpenGl.RenderTypes.Polygons:
							break;

						default:
							// make sure the render mode is set to outline
							sceneContext.ViewState.RenderType = RenderOpenGl.RenderTypes.Outlines;
							break;
					}

					var currentOutputType = selectedItem.OutputType;
					if (currentOutputType != PrintOutputTypes.Solid && currentOutputType != PrintOutputTypes.Default)
					{
						undoBuffer.AddAndDo(new ChangeColor(selectedItem, colorField.Color, PrintOutputTypes.Solid));
					}

					SetButtonStates();
					Invalidate();
				}

				solidButton.Parent.MouseDown += (s, e) => SetToSolid();

				var colorRow = new SettingsRow("Output".Localize(), null, colorField.Content, theme)
				{
					// Special top border style for first item in editor
					Border = new BorderDouble(0, 1)
				};
				editorPanel.AddChild(colorRow);

				// put in a hole button
				holeButton = new ColorButton(Color.DarkGray)
				{
					Margin = new BorderDouble(5, 0, 11, 0),
					Width = scaledButtonSize,
					Height = scaledButtonSize,
					BackgroundRadius = scaledButtonSize / 2,
					BackgroundOutlineWidth = 1,
					VAnchor = VAnchor.Center,
					DisabledColor = theme.MinimalShade,
					BorderColor = theme.TextColor,
					ToolTipText = "Convert to Hole".Localize(),
				};

				GuiWidget NewTextContainer(string text)
                {
					var textWidget = new TextWidget(text.Localize(), pointSize: theme.FontSize10, textColor: theme.TextColor)
					{
						Margin = new BorderDouble(5, 4, 5, 5),
						AutoExpandBoundsToText = true,
					};

					var container = new GuiWidget()
					{
						Margin = new BorderDouble(5, 0),
						VAnchor = VAnchor.Fit | VAnchor.Center,
						HAnchor = HAnchor.Fit,
						BackgroundRadius = 3,
						BackgroundOutlineWidth = 1,
						BorderColor = theme.PrimaryAccentColor,
						Selectable = true,
					};

					container.AddChild(textWidget);

					return container;
				}

				var buttonRow = solidButton.Parents<FlowLayoutWidget>().FirstOrDefault();
				solidContainer = NewTextContainer("Solid");
				buttonRow.AddChild(solidContainer, 0);

				buttonRow.AddChild(holeButton, 0);
				holeContainer = NewTextContainer("Hole");
				buttonRow.AddChild(holeContainer, 0);

				otherContainer = NewTextContainer("");
				buttonRow.AddChild(otherContainer, 0);

				otherText = otherContainer.Children.First() as TextWidget;

				void SetToHole()
                {
					if (selectedItem.OutputType != PrintOutputTypes.Hole)
					{
						undoBuffer.AddAndDo(new MakeHole(selectedItem));
					}
					SetButtonStates();
					Invalidate();
				}

				holeButton.Click += (s, e) => SetToHole();
				holeContainer.Click += (s, e) => SetToHole();
				solidContainer.Click += (s, e) => SetToSolid();

				SetButtonStates();
				void SelectedItemOutputChanged(object sender, EventArgs e)
                {
					SetButtonStates();
				}

				selectedItem.Invalidated += SelectedItemOutputChanged;
				Closed += (s, e) => selectedItem.Invalidated -= SelectedItemOutputChanged;

				// put in a material edit field
				var materialField = new MaterialIndexField(sceneContext.Printer, theme, selectedItem.MaterialIndex);
				materialField.Initialize(0);
				materialField.ValueChanged += (s, e) =>
				{
					if (selectedItem.MaterialIndex != materialField.MaterialIndex)
					{
						undoBuffer.AddAndDo(new ChangeMaterial(selectedItem, materialField.MaterialIndex));
					}
				};

				materialField.Content.MouseDown += (s, e) =>
				{
					if (sceneContext.ViewState.RenderType != RenderOpenGl.RenderTypes.Materials)
					{
						// make sure the render mode is set to material
						sceneContext.ViewState.RenderType = RenderOpenGl.RenderTypes.Materials;
					}
				};

				// material row
				editorPanel.AddChild(new SettingsRow("Material".Localize(), null, materialField.Content, theme));
            }

            var rows = new SafeList<SettingsRow>();

			// put in the normal editor
			if (selectedItem is ComponentObject3D componentObject
				&& componentObject.Finalized)
            {
                AddComponentEditor(selectedItem, undoBuffer, rows, componentObject);
            }
            else
			{
				if (item != null
					&& ApplicationController.Instance.Extensions.GetEditorsForType(item.GetType())?.FirstOrDefault() is IObject3DEditor editor)
				{
					ShowObjectEditor((editor, item, item.Name), selectedItem);
				}
			}
        }

        private void AddComponentEditor(IObject3D selectedItem, UndoBuffer undoBuffer, SafeList<SettingsRow> rows, ComponentObject3D componentObject)
        {
            var context = new PPEContext();
            PublicPropertyEditor.AddUnlockLinkIfRequired(selectedItem, editorPanel, theme);
			var editorList = componentObject.SurfacedEditors;
            for (var editorIndex = 0; editorIndex < editorList.Count; editorIndex++)
            {
                // if it is a reference to a sheet cell
                if (editorList[editorIndex].StartsWith("!"))
                {
                    AddSheetCellEditor(undoBuffer, componentObject, editorList, editorIndex);
                }
                else // parse it as a path to an object
                {
                    // Get the named property via reflection
                    // Selector example:            '$.Children<CylinderObject3D>'
                    var match = pathGetter.Select(componentObject, editorList[editorIndex]).ToList();

                    //// - Add editor row for each
                    foreach (var instance in match)
                    {
                        if (instance is IObject3D object3D)
                        {
                            if (ApplicationController.Instance.Extensions.GetEditorsForType(object3D.GetType())?.FirstOrDefault() is IObject3DEditor editor)
                            {
                                ShowObjectEditor((editor, object3D, object3D.Name), selectedItem);
                            }
                        }
                        else if (JsonPathContext.ReflectionValueSystem.LastMemberValue is ReflectionTarget reflectionTarget)
                        {
                            if (reflectionTarget.Source is IObject3D editedChild)
                            {
                                context.item = editedChild;
                            }
                            else
                            {
                                context.item = item;
                            }

                            var editableProperty = new EditableProperty(reflectionTarget.PropertyInfo, reflectionTarget.Source);

                            var editor = PublicPropertyEditor.CreatePropertyEditor(rows, editableProperty, undoBuffer, context, theme);
                            if (editor != null)
                            {
                                editorPanel.AddChild(editor);
                            }

                            // Init with custom 'UpdateControls' hooks
                            (context.item as IPropertyGridModifier)?.UpdateControls(new PublicPropertyChange(context, "Update_Button"));
                        }
                    }
                }
            }

            // Enforce panel padding
            foreach (var sectionWidget in editorPanel.Descendants<SectionWidget>())
            {
                sectionWidget.Margin = 0;
            }
        }

        private void AddSheetCellEditor(UndoBuffer undoBuffer, ComponentObject3D componentObject, List<string> editorList, int editorIndex)
        {
            var firtSheet = componentObject.Descendants<SheetObject3D>().FirstOrDefault();
            if (firtSheet != null)
            {
                var (cellId, cellData) = componentObject.DecodeContent(editorIndex);
                var cell = firtSheet.SheetData[cellId];
                if (cell != null)
                {
                    // create an expresion editor
                    var field = new ExpressionField(theme)
                    {
                        Name = cellId + " Field"
                    };
                    field.Initialize(0);
                    if (cellData.Contains("="))
                    {
                        field.SetValue(cellData, false);
                    }
                    else // make sure it is formatted
                    {
                        double.TryParse(cellData, out double value);
                        var format = "0." + new string('#', 5);
                        field.SetValue(value.ToString(format), false);
                    }

                    field.ClearUndoHistory();

                    var doOrUndoing = false;
                    field.ValueChanged += (s, e) =>
                    {
                        if (!doOrUndoing)
                        {
                            var oldValue = componentObject.DecodeContent(editorIndex).cellData;
                            var newValue = field.Value;
                            undoBuffer.AddAndDo(new UndoRedoActions(() =>
                            {
                                doOrUndoing = true;
								editorList[editorIndex] = "!" + cellId + "," + oldValue;
								var expression = new DoubleOrExpression(oldValue);
								cell.Expression = expression.Value(componentObject).ToString();
								componentObject.Invalidate(InvalidateType.SheetUpdated);
                                doOrUndoing = false;
                            },
                            () =>
                            {
                                doOrUndoing = true;
								editorList[editorIndex] = "!" + cellId + "," + newValue;
								var expression = new DoubleOrExpression(newValue);
								cell.Expression = expression.Value(componentObject).ToString();
								componentObject.Invalidate(InvalidateType.SheetUpdated);
								doOrUndoing = false;
                            }));
                        }

                    };

                    var row = new SettingsRow(cell.Name == null ? cellId : cell.Name.Replace("_", " "), null, field.Content, theme);
                    editorPanel.AddChild(row);
                }
            }
        }

        private class OperationButton : ThemedTextButton
		{
			private readonly SceneOperation sceneOperation;
			private readonly ISceneContext sceneContext;

			public OperationButton(SceneOperation sceneOperation, ISceneContext sceneContext, ThemeConfig theme)
				: base(sceneOperation.Title, theme)
			{
				this.sceneOperation = sceneOperation;
				this.sceneContext = sceneContext;
			}

			public void EnsureAvailablity()
			{
				this.Enabled = sceneOperation.IsEnabled?.Invoke(sceneContext) != false;
			}
		}

		private void ShowObjectEditor((IObject3DEditor editor, IObject3D item, string displayName) scopeItem, IObject3D rootSelection)
		{
			var selectedItem = scopeItem.item;

			var editorWidget = scopeItem.editor.Create(selectedItem, sceneContext.Scene.UndoBuffer, theme);
			editorWidget.HAnchor = HAnchor.Stretch;
			editorWidget.VAnchor = VAnchor.Fit;

			if (scopeItem.item != rootSelection
				&& scopeItem.editor is PublicPropertyEditor)
			{
				editorWidget.Padding = new BorderDouble(10, 10, 10, 0);

				// EditOutline section
				var sectionWidget = new SectionWidget(
						scopeItem.displayName ?? "Unknown",
						editorWidget,
						theme);

				theme.ApplyBoxStyle(sectionWidget, margin: 0);

				editorWidget = sectionWidget;
			}
			else
			{
				editorWidget.Padding = 0;
			}

			editorPanel.AddChild(editorWidget);
		}

		public Task Save(ILibraryItem item, IObject3D content)
		{
			this.item.Parent.Children.Modify(children =>
			{
				children.Remove(this.item);
				children.Add(content);
			});

			return null;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			scene.SelectionChanged -= Scene_SelectionChanged;

			base.OnClosed(e);
		}

		private void Scene_SelectionChanged(object sender, EventArgs e)
		{
			if (editorPanel.Children.FirstOrDefault()?.DescendantsAndSelf<SectionWidget>().FirstOrDefault() is SectionWidget firstSectionWidget)
			{
				firstSectionWidget.Margin = firstSectionWidget.Margin.Clone(top: 0);
			}

			var selectedItem = scene.SelectedItem;

			applyButton.Enabled = selectedItem != null
				&& (selectedItem is GroupObject3D
				|| (selectedItem.GetType() == typeof(Object3D) && selectedItem.Children.Any())
				|| selectedItem.CanApply);
			cancelButton.Enabled = selectedItem != null;
			overflowButton.Enabled = selectedItem != null;
			if (selectedItem == null)
			{
				primaryActionsPanel.RemoveChildren();
			}
		}

        public void Dispose()
        {
        }
    }
}
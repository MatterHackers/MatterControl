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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public enum ViewControls3DButtons
	{
		Rotate,
		Scale,
		Translate,
		PartSelect
	}

	public enum PartViewMode
	{
		Layers2D,
		Layers3D,
		Model
	}

	public class ViewModeChangedEventArgs : EventArgs
	{
		public PartViewMode ViewMode { get; set; }

		public PartViewMode PreviousMode { get; set; }
	}

	public class TransformStateChangedEventArgs : EventArgs
	{
		public ViewControls3DButtons TransformMode { get; set; }
	}

	public class ViewControls3D : OverflowBar
	{
		public event EventHandler ResetView;

		public event EventHandler<TransformStateChangedEventArgs> TransformStateChanged;

		private RadioIconButton translateButton;
		private RadioIconButton rotateButton;
		private RadioIconButton scaleButton;
		private RadioIconButton partSelectButton;

		private View3DWidget view3DWidget;
		private ISceneContext sceneContext;
		private PartWorkspace workspace;
		private ViewControls3DButtons activeTransformState = ViewControls3DButtons.PartSelect;
		private Dictionary<GuiWidget, SceneSelectionOperation> operationButtons;
		private MainViewWidget mainViewWidget = null;
		private PopupMenuButton bedMenuButton;
		private ThemeConfig theme;
		private UndoBuffer undoBuffer;
		private IconButton undoButton;
		private IconButton redoButton;

		public ViewControls3D(PartWorkspace workspace, ThemeConfig theme,  UndoBuffer undoBuffer, bool isPrinterType, bool showPrintButton)
			: base(theme)
		{
			this.theme = theme;
			this.undoBuffer = undoBuffer;
			this.ActionArea.Click += (s, e) =>
			{
				view3DWidget.InteractionLayer.Focus();
			};

			this.OverflowButton.DynamicPopupContent = () =>
			{
				var menuTheme = AppContext.MenuTheme;
				var popupMenu = new PopupMenu(theme);
				int i = 0;

				foreach (var widget in this.ActionArea.Children.Where(c => !c.Visible && !ignoredInMenuTypes.Contains(c.GetType())))
				{
					if (operationButtons.TryGetValue(widget, out SceneSelectionOperation operation))
					{
						if (operation is OperationGroup operationGroup)
						{
							popupMenu.CreateSubMenu(
								operationGroup.Title,
								menuTheme,
								(subMenu) =>
								{
									foreach (var childOperation in operationGroup.Operations)
									{
										var menuItem = subMenu.CreateMenuItem(childOperation.Title, childOperation.Icon(menuTheme.InvertIcons));
										menuItem.Click += (s, e) => UiThread.RunOnIdle(() =>
										{
											childOperation.Action?.Invoke(sceneContext);
										});
									}
								});
						}
						else
						{
							popupMenu.CreateMenuItem(operation.Title, operation.Icon(menuTheme.InvertIcons));
						}
					}
				}

				return popupMenu;
			};

			this.IsPrinterMode = isPrinterType;
			this.sceneContext = workspace.SceneContext;
			this.workspace = workspace;

			string iconPath;

			this.AddChild(CreateAddButton(sceneContext, theme));

			this.AddChild(new ToolbarSeparator(theme));

			bedMenuButton = new PopupMenuButton(AggContext.StaticData.LoadIcon("bed.png", 16, 16, theme.InvertIcons), theme)
			{
				Name = "Bed Options Menu",
				ToolTipText = "Bed",
				Enabled = true,
				Margin = theme.ButtonSpacing,
				VAnchor = VAnchor.Center,
				DrawArrow = true
			};

			this.AddChild(bedMenuButton);

			this.AddChild(new ToolbarSeparator(theme));

			this.AddChild(CreateOpenButton(theme));

			this.AddChild(CreateSaveButton(theme));

			this.AddChild(new ToolbarSeparator(theme));

			undoButton = new IconButton(AggContext.StaticData.LoadIcon("Undo_grey_16x.png", 16, 16, theme.InvertIcons), theme)
			{
				Name = "3D View Undo",
				ToolTipText = "Undo".Localize(),
				Enabled = false,
				Margin = theme.ButtonSpacing,
				VAnchor = VAnchor.Center
			};
			undoButton.Click += (sender, e) =>
			{
				sceneContext.Scene.Undo();
				view3DWidget.InteractionLayer.Focus();
			};
			this.AddChild(undoButton);

			redoButton = new IconButton(AggContext.StaticData.LoadIcon("Redo_grey_16x.png", 16, 16, theme.InvertIcons), theme)
			{
				Name = "3D View Redo",
				Margin = theme.ButtonSpacing,
				ToolTipText = "Redo".Localize(),
				Enabled = false,
				VAnchor = VAnchor.Center
			};
			redoButton.Click += (sender, e) =>
			{
				sceneContext.Scene.Redo();
				view3DWidget.InteractionLayer.Focus();
			};
			this.AddChild(redoButton);

			if (showPrintButton)
			{
				var printButton = new TextButton("Print", theme)
				{
					Name = "Print Button",
					BackgroundColor = theme.AccentMimimalOverlay
				};
				printButton.Click += (s, e) =>
				{
					view3DWidget.PushToPrinterAndPrint();
				};
				this.AddChild(printButton);
			}

			this.AddChild(new ToolbarSeparator(theme));

			undoButton.Enabled = undoBuffer.UndoCount > 0;
			redoButton.Enabled = undoBuffer.RedoCount > 0;

			var buttonGroupA = new ObservableCollection<GuiWidget>();

			if (UserSettings.Instance.IsTouchScreen)
			{
				iconPath = Path.Combine("ViewTransformControls", "rotate.png");
				rotateButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, 32, 32, theme.InvertIcons), theme)
				{
					SiblingRadioButtonList = buttonGroupA,
					ToolTipText = "Rotate (Alt + Left Mouse)".Localize(),
					Margin = theme.ButtonSpacing
				};
				rotateButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Rotate;
				buttonGroupA.Add(rotateButton);
				AddChild(rotateButton);

				iconPath = Path.Combine("ViewTransformControls", "translate.png");
				translateButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, 32, 32, theme.InvertIcons), theme)
				{
					SiblingRadioButtonList = buttonGroupA,
					ToolTipText = "Move (Shift + Left Mouse)".Localize(),
					Margin = theme.ButtonSpacing
				};
				translateButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Translate;
				buttonGroupA.Add(translateButton);
				AddChild(translateButton);

				iconPath = Path.Combine("ViewTransformControls", "scale.png");
				scaleButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, 32, 32, theme.InvertIcons), theme)
				{
					SiblingRadioButtonList = buttonGroupA,
					ToolTipText = "Zoom (Ctrl + Left Mouse)".Localize(),
					Margin = theme.ButtonSpacing
				};
				scaleButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Scale;
				buttonGroupA.Add(scaleButton);
				AddChild(scaleButton);

				rotateButton.Checked = true;

				// Add vertical separator
				this.AddChild(new ToolbarSeparator(theme));

				iconPath = Path.Combine("ViewTransformControls", "partSelect.png");
				partSelectButton = new RadioIconButton(AggContext.StaticData.LoadIcon(iconPath, 32, 32, theme.InvertIcons), theme)
				{
					SiblingRadioButtonList = buttonGroupA,
					ToolTipText = "Select Part".Localize(),
					Margin = theme.ButtonSpacing
				};
				partSelectButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.PartSelect;
				buttonGroupA.Add(partSelectButton);
				AddChild(partSelectButton);
			}

			operationButtons = new Dictionary<GuiWidget, SceneSelectionOperation>();

			// Add Selected IObject3D -> Operations to toolbar
			foreach (var namedAction in ApplicationController.Instance.RegisteredSceneOperations)
			{
				if (namedAction is SceneSelectionSeparator)
				{
					this.AddChild(new ToolbarSeparator(theme));
					continue;
				}

				// add the create support before the align
				if (namedAction is OperationGroup group
					&& group.GroupName == "Align")
				{
					this.AddChild(CreateWipeTowerButton(theme));
					this.AddChild(CreateSupportButton(theme));
					this.AddChild(new ToolbarSeparator(theme));
				}

				GuiWidget button = null;

				if (namedAction is OperationGroup operationGroup)
				{
					if (operationGroup.Collapse)
					{
						var defaultOperation = operationGroup.GetDefaultOperation();

						PopupMenuButton groupButton = null;

						groupButton = theme.CreateSplitButton(
							new SplitButtonParams()
							{
								Icon = defaultOperation.Icon(theme.InvertIcons),
								DefaultAction = (menuButton) =>
								{
									defaultOperation.Action.Invoke(sceneContext);
								},
								DefaultActionTooltip = defaultOperation.HelpText ?? defaultOperation.Title,
								ButtonName = defaultOperation.Title,
								ExtendPopupMenu = (PopupMenu popupMenu) =>
								{
									foreach (var operation in operationGroup.Operations)
									{
										var operationMenu = popupMenu.CreateMenuItem(operation.Title, operation.Icon?.Invoke(theme.InvertIcons));

										operationMenu.ToolTipText = operation.HelpText;
										operationMenu.Enabled = operation.IsEnabled(sceneContext);
										operationMenu.Click += (s, e) => UiThread.RunOnIdle(() =>
										{
											if (operationGroup.StickySelection
												&& defaultOperation != operation)
											{
												// Update button
												var iconButton = groupButton.Children.OfType<IconButton>().First();
												iconButton.SetIcon(operation.Icon(theme.InvertIcons));
												iconButton.ToolTipText = operation.HelpText ?? operation.Title;

												UserSettings.Instance.set(operationGroup.GroupRecordId, operationGroup.Operations.IndexOf(operation).ToString());

												defaultOperation = operation;

												iconButton.Invalidate();
											}

											operation.Action?.Invoke(sceneContext);
										});
									}
								}
							},
							operationGroup);

						button = groupButton;
					}
					else
					{
						if (!(this.ActionArea.Children.LastOrDefault() is ToolbarSeparator))
						{
							this.AddChild(new ToolbarSeparator(theme));
						}

						foreach (var operation in operationGroup.Operations)
						{
							var operationButton = new OperationIconButton(operation, sceneContext, theme);
							operationButtons.Add(operationButton, operation);

							this.AddChild(operationButton);
						}

						this.AddChild(new ToolbarSeparator(theme));
					}
				}
				else if (namedAction.Icon != null)
				{
					button = new IconButton(namedAction.Icon(theme.InvertIcons), theme)
					{
						Name = namedAction.Title + " Button",
						ToolTipText = namedAction.Title,
						Margin = theme.ButtonSpacing,
						BackgroundColor = theme.ToolbarButtonBackground,
						HoverColor = theme.ToolbarButtonHover,
						MouseDownColor = theme.ToolbarButtonDown,
					};
				}
				else
				{
					button = new TextButton(namedAction.Title, theme)
					{
						Name = namedAction.Title + " Button",
						Margin = theme.ButtonSpacing,
						BackgroundColor = theme.ToolbarButtonBackground,
						HoverColor = theme.ToolbarButtonHover,
						MouseDownColor = theme.ToolbarButtonDown,
					};
				}

				if (button != null)
				{
					operationButtons.Add(button, namedAction);

					// Only bind Click event if not a SplitButton
					if (!(button is PopupMenuButton))
					{
						button.Click += (s, e) => UiThread.RunOnIdle(() =>
						{
							namedAction.Action.Invoke(sceneContext);
							var partTab = button.Parents<PartTabPage>().FirstOrDefault();
							var view3D = partTab.Descendants<View3DWidget>().FirstOrDefault();
							view3D.InteractionLayer.Focus();
						});
					}

					this.AddChild(button);
				}
			}

			// Register listeners
			undoBuffer.Changed += UndoBuffer_Changed;
			sceneContext.Scene.SelectionChanged += UpdateToolbarButtons;
			sceneContext.Scene.ItemsModified += UpdateToolbarButtons;

			// Run on load
			UpdateToolbarButtons(null, null);
		}

		internal void NotifyResetView()
		{
			this.ResetView.Invoke(this, null);
		}

		public bool IsPrinterMode { get; }

		public ViewControls3DButtons ActiveButton
		{
			get => activeTransformState;
			set
			{
				this.activeTransformState = value;
				switch (this.activeTransformState)
				{
					case ViewControls3DButtons.Rotate:
						if (rotateButton != null)
						{
							rotateButton.Checked = true;
						}

						break;

					case ViewControls3DButtons.Translate:
						if (translateButton != null)
						{
							translateButton.Checked = true;
						}

						break;

					case ViewControls3DButtons.Scale:
						if (scaleButton != null)
						{
							scaleButton.Checked = true;
						}

						break;

					case ViewControls3DButtons.PartSelect:
						if (partSelectButton != null)
						{
							partSelectButton.Checked = true;
						}

						break;
				}

				TransformStateChanged?.Invoke(this, new TransformStateChangedEventArgs()
				{
					TransformMode = activeTransformState
				});
			}
		}

		internal void SetView3DWidget(View3DWidget view3DWidget)
		{
			this.view3DWidget = view3DWidget;

			bedMenuButton.DynamicPopupContent = () =>
			{
				var workspaceActions = ApplicationController.Instance.GetWorkspaceActions(view3DWidget);
				var menuTheme = ApplicationController.Instance.MenuTheme;
				var popupMenu = new PopupMenu(menuTheme);

				int thumbWidth = 45;
				var gutterWidth = thumbWidth + 7;

				popupMenu.CreateSubMenu("Open Recent".Localize(), menuTheme, (subMenu) =>
				{
					int maxItemWidth = 0;

					var recentFiles = new DirectoryInfo(ApplicationDataStorage.Instance.PlatingDirectory).GetFiles("*.mcx").OrderByDescending(f => f.LastWriteTime);
					foreach (var item in recentFiles.Where(f => f.Length > 215).Select(f => new SceneReplacementFileItem(f.FullName)).Take(12))
					{
						var imageBuffer = new ImageBuffer(thumbWidth, thumbWidth);

						var title = new FileInfo(item.Path).LastWriteTime.ToString("MMMM d h:mm tt");

						var bedHistory = subMenu.CreateMenuItem(title, imageBuffer);
						bedHistory.GutterWidth = gutterWidth;
						bedHistory.HAnchor = HAnchor.Fit;
						bedHistory.VAnchor = VAnchor.Absolute;
						bedHistory.Padding = new BorderDouble(gutterWidth + 3, 2, 12, 2);
						bedHistory.Height = thumbWidth + 3;
						bedHistory.Click += (s, e) =>
						{
							UiThread.RunOnIdle(async () =>
							{
								await ApplicationController.Instance.Tasks.Execute("Saving changes".Localize() + "...", sceneContext.Printer, sceneContext.SaveChanges);

								await sceneContext.LoadLibraryContent(item);

								if (sceneContext.Printer != null)
								{
									sceneContext.Printer.ViewState.ViewMode = PartViewMode.Model;
								}
							});
						};

						maxItemWidth = (int)Math.Max(maxItemWidth, bedHistory.Width);

						void UpdateImageBuffer(ImageBuffer thumbnail)
						{
							// Dump OpenGL texture
							ImageGlPlugin.Remove(imageBuffer);

							// Copy updated thumbnail into original image
							imageBuffer.CopyFrom(thumbnail);

							bedHistory.Invalidate();
						}

						ApplicationController.Instance.Library.LoadItemThumbnail(
							UpdateImageBuffer,
							(contentProvider) =>
							{
								if (contentProvider is MeshContentProvider meshContentProvider)
								{
									ApplicationController.Instance.Thumbnails.QueueForGeneration(async () =>
									{
										// Ask the MeshContentProvider to RayTrace the image
										var thumbnail = await meshContentProvider.GetThumbnail(item, thumbWidth, thumbWidth);
										if (thumbnail != null)
										{
											UpdateImageBuffer(thumbnail);
										}
									});
								}
							},
							item,
							ApplicationController.Instance.Library.PlatingHistory,
							thumbWidth,
							thumbWidth,
							menuTheme).ConfigureAwait(false);
					}

					// Resize menu items to max item width
					foreach (var menuItem in subMenu.Children)
					{
						menuItem.HAnchor = HAnchor.Left | HAnchor.Absolute;
						menuItem.Width = maxItemWidth;
					}
				});

				var actions = new NamedAction[]
				{
					new ActionSeparator(),
					workspaceActions["Cut"],
					workspaceActions["Copy"],
					workspaceActions["Paste"],
					new ActionSeparator(),
					workspaceActions["Print"],
					new ActionSeparator(),
					new NamedAction()
					{
						ID = "Export",
						Title = "Export".Localize(),
						Icon = AggContext.StaticData.LoadIcon("cube_export.png", 16, 16, menuTheme.InvertIcons),
						Action = () =>
						{
							ApplicationController.Instance.ExportLibraryItems(
								new[] { new InMemoryLibraryItem(sceneContext.Scene) },
								centerOnBed: false,
								printer: view3DWidget.Printer);
						},
						IsEnabled = () => sceneContext.EditableScene
							|| (sceneContext.EditContext.SourceItem is ILibraryAsset libraryAsset
								&& string.Equals(Path.GetExtension(libraryAsset.FileName), ".gcode", StringComparison.OrdinalIgnoreCase))
					},
					new ActionSeparator(),
					new NamedAction()
					{
						ID = "ClearBed",
						Title = "Clear Bed".Localize(),
						Action = () =>
						{
							UiThread.RunOnIdle(() =>
							{
								view3DWidget.ClearPlate();
							});
						}
					}
				};

				menuTheme.CreateMenuItems(popupMenu, actions);

				return popupMenu;
			};
		}

		private void UndoBuffer_Changed(object sender, EventArgs e)
		{
			undoButton.Enabled = undoBuffer.UndoCount > 0;
			redoButton.Enabled = undoBuffer.RedoCount > 0;
		}

		private IconButton CreateOpenButton(ThemeConfig theme)
		{
			var openButton = new IconButton(AggContext.StaticData.LoadIcon("fa-folder-open_16.png", 16, 16, theme.InvertIcons), theme)
			{
				Margin = theme.ButtonSpacing,
				ToolTipText = "Open File".Localize(),
				Name = "Open File Button"
			};
			openButton.Click += (s, e) =>
			{
				var extensionsWithoutPeriod = new HashSet<string>(ApplicationSettings.OpenDesignFileParams.Split('|').First().Split(',').Select(t => t.Trim().Trim('.')));

				foreach (var extension in ApplicationController.Instance.Library.ContentProviders.Keys)
				{
					extensionsWithoutPeriod.Add(extension.ToUpper());
				}

				var extensionsArray = extensionsWithoutPeriod.OrderBy(t => t).ToArray();

				string filter = string.Format(
					"{0}|{1}",
					string.Join(",", extensionsArray),
					string.Join("", extensionsArray.Select(t => $"*.{t.ToLower()};").ToArray()));

				UiThread.RunOnIdle(() =>
				{
					AggContext.FileDialogs.OpenFileDialog(
						new OpenFileDialogParams(filter, multiSelect: true),
						(openParams) =>
						{
							sceneContext.AddToPlate(openParams.FileNames);
						});
				}, .1);
			};
			return openButton;
		}

		private void UpdateToolbarButtons(object sender, EventArgs e)
		{
			// Set enabled level based on operation rules
			foreach (var (button, operation) in operationButtons.Select(kvp => (kvp.Key, kvp.Value)))
			{
				button.Enabled = operation.IsEnabled?.Invoke(sceneContext) ?? false;

				if (operation is OperationGroup operationGroup
					&& button is PopupMenuButton splitButton
					&& button.Descendants<IconButton>().FirstOrDefault() is IconButton iconButton)
				{
					iconButton.Enabled = operationGroup.GetDefaultOperation().IsEnabled(sceneContext);
				}
			}
		}

		private GuiWidget CreateAddButton(ISceneContext sceneContext, ThemeConfig theme)
		{
			var buttonView = new TextIconButton(
				"",
				AggContext.StaticData.LoadIcon("cube_add.png", 16, 16, theme.InvertIcons),
				theme);

			// Remove right Padding for drop style
			buttonView.Padding = buttonView.Padding.Clone(right: 0);

			var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
			};

			var openColor = theme.ResolveColor(theme.BackgroundColor, theme.SlightShade);

			PopupMenuButton libraryPopup = null;
			libraryPopup = new PopupMenuButton(buttonView, theme)
			{
				MakeScrollable = false,
				Name = "Add Content Menu",
				ToolTipText = "Add Content".Localize(),
				AlwaysKeepOpen = true,
				DynamicPopupContent = () =>
				{
					if (mainViewWidget == null)
					{
						mainViewWidget = this.Parents<MainViewWidget>().FirstOrDefault();
					}

					var verticalResizeContainer = new VerticalResizeContainer(theme, GrabBarSide.Right)
					{
						BackgroundColor = openColor,
						MinimumSize = new Vector2(120, 50),
						Height = libraryPopup.TransformToScreenSpace(libraryPopup.Position).Y,
						SplitterBarColor = theme.SlightShade,
					};

					double.TryParse(UserSettings.Instance.get(UserSettingsKey.PopupLibraryWidth), out double controlWidth);
					if (controlWidth == 0)
					{
						controlWidth = 400;
					}

					verticalResizeContainer.Width = controlWidth;

					verticalResizeContainer.BoundsChanged += (s2, e2) =>
					{
						UserSettings.Instance.set(UserSettingsKey.PopupLibraryWidth, verticalResizeContainer.Width.ToString());
					};

					var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();

					// Compute slight highlight of openColor for use as listView background color
					var slightHighlight = theme.ResolveColor(openColor, Color.White.WithAlpha(theme.IsDarkTheme ? 10 : 50));

					var printLibraryWidget = new PrintLibraryWidget(mainViewWidget, workspace, theme, slightHighlight, libraryPopup)
					{
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Absolute,
						Height = libraryPopup.TransformToScreenSpace(libraryPopup.Position).Y,
						Margin = new BorderDouble(left: verticalResizeContainer.SplitterWidth)
					};

					systemWindow.SizeChanged += (s, e) =>
					{
						printLibraryWidget.Height = libraryPopup.TransformToScreenSpace(libraryPopup.Position).Y;
					};

					verticalResizeContainer.AddChild(printLibraryWidget);

					systemWindow.MouseDown += SystemWindownMouseDown;

					void SystemWindownMouseDown(object s2, MouseEventArgs mouseEvent)
					{
						if (verticalResizeContainer.Parent != null)
						{
							// MouseUp on our SystemWindow outside of our bounds should call close
							var resizeContainerMousePosition = verticalResizeContainer.TransformFromScreenSpace(mouseEvent.Position);
							bool mouseUpOnWidget = resizeContainerMousePosition.X >= 0 && resizeContainerMousePosition.X <= verticalResizeContainer.Width
								&& resizeContainerMousePosition.Y >= 0 && resizeContainerMousePosition.Y <= verticalResizeContainer.Height;

							if (!mouseUpOnWidget)
							{
								libraryPopup.CloseMenu();
								systemWindow.MouseDown -= SystemWindownMouseDown;
							}
						}
						else
						{
							systemWindow.MouseDown -= SystemWindownMouseDown;
						}
					}

					return verticalResizeContainer;
				},
				BackgroundColor = theme.ToolbarButtonBackground,
				HoverColor = theme.ToolbarButtonHover,
				MouseDownColor = theme.ToolbarButtonDown,
				OpenColor = openColor,
				DrawArrow = true,
				Margin = theme.ButtonSpacing,
				PopupBorderColor = Color.Transparent,
				PopupHAnchor = HAnchor.Fit,
				PopupVAnchor = VAnchor.Fit
			};

			return libraryPopup;
		}

		private GuiWidget CreateWipeTowerButton(ThemeConfig theme)
		{
			var iconButton = new IconButton(
				AggContext.StaticData.LoadIcon("wipe_tower.png", 16, 16, theme.InvertIcons),
				theme)
			{
				ToolTipText = "Toggle Wipe Tower".Localize(),
			};

			iconButton.Click += (s, e) =>
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
			};

			sceneContext.Scene.SelectionChanged += (s, e) =>
			{
				iconButton.Enabled = sceneContext.Scene.SelectedItem != null;
			};

			return iconButton;
		}

		private GuiWidget CreateSupportButton(ThemeConfig theme)
		{
			PopupMenuButton toggleSupportButton = null;

			var iconButton = new IconButton(
				AggContext.StaticData.LoadIcon("support.png", 16, 16, theme.InvertIcons),
				theme)
			{
				ToolTipText = "Toggle Support".Localize(),
			};

			iconButton.Click += (s, e) =>
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
			};

			sceneContext.Scene.SelectionChanged += (s, e) =>
			{
				iconButton.Selectable = sceneContext.Scene.SelectedItem != null;
			};

			// Remove right Padding for drop style
			iconButton.Padding = iconButton.Padding.Clone(right: 0);

			var minimumSupportHeight = .05;
			if (sceneContext.Printer != null)
			{
				minimumSupportHeight = sceneContext.Printer.Settings.GetValue<double>(SettingsKey.layer_height) / 2;
			}

			toggleSupportButton = new PopupMenuButton(iconButton, theme)
			{
				Name = "Support SplitButton",
				ToolTipText = "Generate Support".Localize(),
				DynamicPopupContent = () => new GenerateSupportPanel(AppContext.MenuTheme, sceneContext.Scene, minimumSupportHeight),
				PopupHAnchor = HAnchor.Fit,
				PopupVAnchor = VAnchor.Fit,
				MakeScrollable = false,
				BackgroundColor = theme.ToolbarButtonBackground,
				HoverColor = theme.ToolbarButtonHover,
				MouseDownColor = theme.ToolbarButtonDown,
				DrawArrow = true,
				Margin = theme.ButtonSpacing,
			};

			iconButton.Selectable = true;

			return toggleSupportButton;
		}

		private GuiWidget CreateSaveButton(ThemeConfig theme)
		{
			return theme.CreateSplitButton(new SplitButtonParams()
			{
				ButtonName = "Save",
				Icon = AggContext.StaticData.LoadIcon("save_grey_16x.png", 16, 16, theme.InvertIcons),
				DefaultAction = (menuButton) =>
				{
					ApplicationController.Instance.Tasks.Execute("Saving".Localize(), sceneContext.Printer, async (progress, cancellationToken) =>
					{
						menuButton.Enabled = false;

						try
						{
							await sceneContext.SaveChanges(progress, cancellationToken);
						}
						catch (Exception ex)
						{
							ApplicationController.Instance.LogError("Error saving file".Localize() + ": " + ex.Message);
						}

						menuButton.Enabled = true;
					}).ConfigureAwait(false);
				},
				DefaultActionTooltip = "Save".Localize(),
				ExtendPopupMenu = (PopupMenu popupMenu) =>
				{
					var saveAs = popupMenu.CreateMenuItem("Save As".Localize());
					saveAs.Click += (s, e) => UiThread.RunOnIdle(() =>
					{
						DialogWindow.Show(
							new SaveAsPage(
								(newName, destinationContainer) =>
								{
									// Save to the destination provider
									if (destinationContainer is ILibraryWritableContainer writableContainer)
									{
										// Wrap stream with ReadOnlyStream library item and add to container
										writableContainer.Add(new[]
										{
											new InMemoryLibraryItem(sceneContext.Scene)
											{
												Name = newName
											}
										});

										destinationContainer.Dispose();
									}
								}));
					});
				}
			});
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			undoBuffer.Changed -= UndoBuffer_Changed;
			sceneContext.Scene.SelectionChanged -= UpdateToolbarButtons;
			sceneContext.Scene.Children.ItemsModified -= UpdateToolbarButtons;

			base.OnClosed(e);
		}
	}
}
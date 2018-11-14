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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using MatterHackers.VectorMath.TrackBall;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class View3DWidget : GuiWidget
	{
		private bool deferEditorTillMouseUp = false;

		public readonly int EditButtonHeight = 44;

		private Color[] SelectionColors = new Color[] { new Color(131, 4, 66), new Color(227, 31, 61), new Color(255, 148, 1), new Color(247, 224, 23), new Color(143, 212, 1) };
		private Stopwatch timeSinceLastSpin = new Stopwatch();
		private Stopwatch timeSinceReported = new Stopwatch();
		public Matrix4X4 TransformOnMouseDown { get; private set; } = Matrix4X4.Identity;

		private TreeView treeView;

		private ViewStyleButton modelViewStyleButton;

		private PrinterConfig printer;

		private ThemeConfig theme;

		public Vector3 BedCenter
		{
			get
			{
				return new Vector3(sceneContext.BedCenter);
			}
		}

		private WorldView World => sceneContext.World;

		public TrackballTumbleWidget TrackballTumbleWidget { get; private set;}

		public InteractionLayer InteractionLayer { get; }

		public BedConfig sceneContext;

		public PrinterConfig Printer { get; private set; }

		private PrinterTabPage printerTabPage;

		public View3DWidget(PrinterConfig printer, BedConfig sceneContext, ViewControls3D viewControls3D, ThemeConfig theme, PartTabPage printerTabBase, MeshViewerWidget.EditorType editorType = MeshViewerWidget.EditorType.Part)
		{
			this.sceneContext = sceneContext;
			this.printerTabPage = printerTabBase as PrinterTabPage;
			this.Printer = printer;

			this.InteractionLayer = new InteractionLayer(this.World, Scene.UndoBuffer, Scene)
			{
				Name = "InteractionLayer",
			};
			this.InteractionLayer.AnchorAll();

			this.viewControls3D = viewControls3D;
			this.printer = printer;
			this.theme = theme;
			this.Name = "View3DWidget";
			this.BackgroundColor = theme.BedBackgroundColor;
			this.HAnchor = HAnchor.Stretch; //	HAnchor.MaxFitOrStretch,
			this.VAnchor = VAnchor.Stretch; //  VAnchor.MaxFitOrStretch

			viewControls3D.TransformStateChanged += ViewControls3D_TransformStateChanged;

			// MeshViewer
			meshViewerWidget = new MeshViewerWidget(sceneContext, this.InteractionLayer, theme, editorType: editorType);
			meshViewerWidget.AnchorAll();
			this.AddChild(meshViewerWidget);

			TrackballTumbleWidget = new TrackballTumbleWidget(sceneContext.World, meshViewerWidget)
			{
				TransformState = TrackBallTransformType.Rotation
			};
			TrackballTumbleWidget.AnchorAll();

			this.BoundsChanged += UpdateRenderView;

			// TumbleWidget
			this.InteractionLayer.AddChild(TrackballTumbleWidget);

			this.InteractionLayer.SetRenderTarget(this.meshViewerWidget);

			// Add splitter support with the InteractionLayer on the left and resize containers on the right
			var splitContainer = new FlowLayoutWidget()
			{
				Name = "SplitContainer",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			splitContainer.AddChild(this.InteractionLayer);
			this.AddChild(splitContainer);

			Scene.SelectionChanged += Scene_SelectionChanged;

			this.Scene.Invalidated += Scene_Invalidated;

			this.AnchorAll();

			TrackballTumbleWidget.TransformState = TrackBallTransformType.Rotation;

			selectedObjectPanel = new SelectedObjectPanel(this, sceneContext, theme)
			{
				VAnchor = VAnchor.Stretch,
			};

			modelViewSidePanel = new VerticalResizeContainer(theme, GrabBarSide.Left)
			{
				Width = printer?.ViewState.SelectedObjectPanelWidth ?? 250,
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Absolute,
				BackgroundColor = theme.InteractionLayerOverlayColor,
				SplitterBarColor = theme.SplitterBackground,
				SplitterWidth = theme.SplitterWidth,
				MinimumSize = new Vector2(theme.SplitterWidth, 0)
			};
			modelViewSidePanel.BoundsChanged += UpdateRenderView;

			modelViewSidePanel.Resized += ModelViewSidePanel_Resized;

			// add the tree view
			treeView = new TreeView(theme)
			{
				Margin = new BorderDouble(left: theme.DefaultContainerPadding + 12),
			};
			treeView.NodeMouseClick += (s, e) =>
			{
				if (e is MouseEventArgs sourceEvent
					&& s is GuiWidget clickedWidget)
				{
					// Ignore AfterSelect events if they're being driven by a SelectionChanged event
					if (!assigningTreeNode)
					{
						Scene.SelectedItem = (IObject3D)treeView.SelectedNode.Tag;
					}

					if (sourceEvent.Button == MouseButtons.Right)
					{
						UiThread.RunOnIdle(() =>
						{
							var menu = ApplicationController.Instance.GetActionMenuForSceneItem((IObject3D)treeView.SelectedNode.Tag, Scene, true);

							var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
							systemWindow.ShowPopup(
								new MatePoint(clickedWidget)
								{
									Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
									AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
								},
								new MatePoint(menu)
								{
									Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
									AltMate = new MateOptions(MateEdge.Right, MateEdge.Top)
								},
								altBounds: new RectangleDouble(sourceEvent.X + 1, sourceEvent.Y + 1, sourceEvent.X + 1, sourceEvent.Y + 1));
						});
					}
				}
			};
			treeView.ScrollArea.ChildAdded += (s, e) =>
			{
				if (e is GuiWidgetEventArgs childEventArgs
					&& childEventArgs.Child is TreeNode treeNode)
				{
					treeNode.AlwaysExpandable = true;
				}
			};

			treeView.ScrollArea.HAnchor = HAnchor.Stretch;

			treeNodeContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(12, 3)
			};
			treeView.AddChild(treeNodeContainer);

			var historyAndProperties = new Splitter()
			{
				Orientation = Orientation.Horizontal,
				Panel1Ratio = sceneContext.ViewState.SceneTreeRatio,
				SplitterSize = theme.SplitterWidth,
				SplitterBackground = theme.SplitterBackground
			};
			historyAndProperties.Panel1.MinimumSize = new Vector2(0, 120);
			historyAndProperties.Panel2.MinimumSize = new Vector2(0, 120);

			modelViewSidePanel.AddChild(historyAndProperties);

			var titleAndTreeView = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			titleAndTreeView.AddChild(workspaceName = new InlineStringEdit(sceneContext.Scene.Name ?? "", theme, "WorkspaceName", editable: false)
			{
				Border = new BorderDouble(top: 1),
				BorderColor = theme.SplitterBackground
			});
			titleAndTreeView.AddChild(treeView);

			workspaceName.ActionArea.AddChild(
				new IconButton(AggContext.StaticData.LoadIcon("fa-angle-right_12.png", theme.InvertIcons), theme)
				{
					Enabled = false
				},
				indexInChildrenList: 0);

			// Remove left margin
			workspaceName.ActionArea.Children<TextWidget>().First().Margin = 0;

			// Resize buttons
			foreach (var iconButton in workspaceName.Descendants<IconButton>())
			{
				iconButton.Height = 26;
				iconButton.Width = 26;
			}

			workspaceName.Margin = workspaceName.Margin.Clone(bottom: 0);

			historyAndProperties.Panel1.AddChild(titleAndTreeView);

			historyAndProperties.DistanceChanged += (s, e) =>
			{
				sceneContext.ViewState.SceneTreeRatio = historyAndProperties.Panel1Ratio;
			};

			historyAndProperties.Panel2.AddChild(selectedObjectPanel);
			splitContainer.AddChild(modelViewSidePanel);

			var tumbleCubeControl = new TumbleCubeControl(this.InteractionLayer, theme)
			{
				Margin = new BorderDouble(0, 0, 10, 35),
				VAnchor = VAnchor.Top,
				HAnchor = HAnchor.Right,
			};

			this.InteractionLayer.AddChild(tumbleCubeControl);

			var viewOptionsBar = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Right | HAnchor.Fit,
				VAnchor = VAnchor.Top | VAnchor.Fit,
				//Margin = new BorderDouble(top: tumbleCubeControl.Height + tumbleCubeControl.Margin.Height + 2),
				BackgroundColor = theme.MinimalShade
			};
			this.InteractionLayer.AddChild(viewOptionsBar);

			var homeButton = new IconButton(AggContext.StaticData.LoadIcon("fa-home_16.png", theme.InvertIcons), theme)
			{
				VAnchor = VAnchor.Absolute,
				ToolTipText = "Reset View".Localize(),
				Margin = theme.ButtonSpacing
			};
			homeButton.Click += (s, e) => viewControls3D.NotifyResetView();
			viewOptionsBar.AddChild(homeButton);

			modelViewStyleButton = new ViewStyleButton(sceneContext, theme)
			{
				ToolTipText = "Model View Style".Localize(),
				PopupMate = new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top)
				}
			};
			modelViewStyleButton.AnchorMate.Mate.VerticalEdge = MateEdge.Bottom;
			modelViewStyleButton.AnchorMate.Mate.HorizontalEdge = MateEdge.Left;

			viewOptionsBar.AddChild(modelViewStyleButton);

			if (printer?.ViewState != null)
			{
				printer.ViewState.ViewModeChanged += this.ViewState_ViewModeChanged;
			}

			ApplicationController.Instance.GetViewOptionButtons(viewOptionsBar, sceneContext, printer, theme);

			// now add the grid snap button
			var gridSnapButton = new GridOptionsPanel(InteractionLayer, theme)
			{
				ToolTipText = "Snap Grid".Localize(),
				PopupMate = new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Right, MateEdge.Top)
				}
			};
			gridSnapButton.AnchorMate.Mate.VerticalEdge = MateEdge.Bottom;
			gridSnapButton.AnchorMate.Mate.HorizontalEdge = MateEdge.Right;

			viewOptionsBar.AddChild(gridSnapButton);

			var interactionVolumes = this.InteractionLayer.InteractionVolumes;
			interactionVolumes.Add(new MoveInZControl(this.InteractionLayer));
			interactionVolumes.Add(new SelectionShadow(this.InteractionLayer));
			interactionVolumes.Add(new SnappingIndicators(this.InteractionLayer, this.CurrentSelectInfo));

			var interactionVolumePlugins = PluginFinder.CreateInstancesOf<InteractionVolumePlugin>();
			foreach (InteractionVolumePlugin plugin in interactionVolumePlugins)
			{
				interactionVolumes.Add(plugin.CreateInteractionVolume(this.InteractionLayer));
			}

			meshViewerWidget.AfterDraw += AfterDraw3DContent;

			Scene.SelectFirstChild();

			viewControls3D.ActiveButton = ViewControls3DButtons.PartSelect;

			this.InteractionLayer.DrawGlOpaqueContent += Draw_GlOpaqueContent;

			sceneContext.SceneLoaded += SceneContext_SceneLoaded;

			// Construct a dictionary of menu actions accessible at the workspace level
			WorkspaceActions = this.InitWorkspaceActions();

			if (!AppContext.IsLoading)
			{
				this.RebuildTree();
			}
		}

		private Dictionary<IObject3D, TreeNode> keyValues = new Dictionary<IObject3D, TreeNode>();

		private void RebuildTree()
		{
			workspaceName.Text = sceneContext.Scene.Name ?? "";

			// Top level selection only - rebuild tree
			treeNodeContainer.CloseAllChildren();

			keyValues.Clear();

			foreach (var child in sceneContext.Scene.Children)
			{
				var rootNode = Object3DTreeBuilder.BuildTree(child, keyValues, theme);
				treeNodeContainer.AddChild(rootNode);
				rootNode.TreeView = treeView;
			}

			// Ensure selectedItem is selected
			var selectedItem = sceneContext.Scene.SelectedItem;
			if (selectedItem != null
				&& keyValues.TryGetValue(selectedItem, out TreeNode treeNode))
			{
				treeView.SelectedNode = treeNode;
			}

			Invalidate();
		}

		private Dictionary<string, NamedAction> InitWorkspaceActions()
		{
			bool invertIcons = ApplicationController.Instance.MenuTheme.InvertIcons;

			return new Dictionary<string, NamedAction>()
			{
				{
					"Insert",
					new NamedAction()
					{
						ID = "Insert",
						Title = "Insert".Localize(),
						Icon = AggContext.StaticData.LoadIcon("cube.png", 16, 16, invertIcons),
						Action = () =>
						{
							var extensionsWithoutPeriod = new HashSet<string>(ApplicationSettings.OpenDesignFileParams.Split('|').First().Split(',').Select(s => s.Trim().Trim('.')));

							foreach(var extension in ApplicationController.Instance.Library.ContentProviders.Keys)
							{
								extensionsWithoutPeriod.Add(extension.ToUpper());
							}

							var extensionsArray = extensionsWithoutPeriod.OrderBy(t => t).ToArray();

							string filter = string.Format(
								"{0}|{1}",
								string.Join(",", extensionsArray),
								string.Join("", extensionsArray.Select(e => $"*.{e.ToLower()};").ToArray()));

							UiThread.RunOnIdle(() =>
							{
								AggContext.FileDialogs.OpenFileDialog(
									new OpenFileDialogParams(filter, multiSelect: true),
									(openParams) =>
									{
										ViewControls3D.LoadAndAddPartsToPlate(this, openParams.FileNames, sceneContext);
									});
							});
						}
					}
				},
				{
					"Print",
					new NamedAction()
					{
						ID = "Print",
						Title = "Print".Localize(),
						Shortcut = "Ctrl+P",
						Action = this.PushToPrinterAndPrint,
						IsEnabled = () => sceneContext.EditableScene
							|| (sceneContext.EditContext.SourceItem is ILibraryAsset libraryAsset
								&& string.Equals(Path.GetExtension(libraryAsset.FileName) ,".gcode" ,StringComparison.OrdinalIgnoreCase))
					}
				},
				{
					"Cut",
					new NamedAction()
					{
						ID = "Cut",
						Title = "Cut".Localize(),
						Shortcut = "Ctrl+X",
						Action = () =>
						{
							sceneContext.Scene.Cut();
						},
						IsEnabled = () => sceneContext.Scene.SelectedItem != null
					}
				},
				{
					"Copy",
					new NamedAction()
					{
						ID = "Copy",
						Title = "Copy".Localize(),
						Shortcut = "Ctrl+C",
						Action = () =>
						{
							sceneContext.Scene.Copy();
						},
						IsEnabled = () => sceneContext.Scene.SelectedItem != null
					}
				},
				{
					"Paste",
					new NamedAction()
					{
						ID = "Paste",
						Title = "Paste".Localize(),
						Shortcut = "Ctrl+V",
						Action = () =>
						{
							sceneContext.Paste();
						},
						IsEnabled = () => Clipboard.Instance.ContainsImage || Clipboard.Instance.GetText() == "!--IObjectSelection--!"
					}
				},
				{
					"Delete",
					new NamedAction()
					{
						ID = "Delete",
						Icon = AggContext.StaticData.LoadIcon("remove.png").SetPreMultiply(),
						Title = "Remove".Localize(),
						Action = sceneContext.Scene.DeleteSelection,
						IsEnabled = () =>  sceneContext.Scene.SelectedItem != null
					}
				},
				{
					"Export",
					new NamedAction()
					{
						ID = "Export",
						Title = "Export".Localize(),
						Icon = AggContext.StaticData.LoadIcon("cube_export.png", 16, 16, invertIcons),
						Action = () =>
						{
							ApplicationController.Instance.ExportLibraryItems(
								new[] { new InMemoryLibraryItem(sceneContext.Scene)},
								centerOnBed: false,
								printer: printer);
						},
						IsEnabled = () => sceneContext.EditableScene
							|| (sceneContext.EditContext.SourceItem is ILibraryAsset libraryAsset
								&& string.Equals(Path.GetExtension(libraryAsset.FileName) ,".gcode" ,StringComparison.OrdinalIgnoreCase))
					}
				},
				{
					"Save",
					new NamedAction()
					{
						ID = "Save",
						Title = "Save".Localize(),
						Shortcut = "Ctrl+S",
						Action = () =>
						{
							ApplicationController.Instance.Tasks.Execute("Saving".Localize(), sceneContext.SaveChanges).ConfigureAwait(false);
						},
						IsEnabled = () => sceneContext.EditableScene
					}
				},
				{
					"SaveAs",
					new NamedAction()
					{
						ID = "SaveAs",
						Title = "Save As".Localize(),
						Action = () => UiThread.RunOnIdle(() =>
						{
							DialogWindow.Show(
								new SaveAsPage(
									async (newName, destinationContainer) =>
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
						}),
						IsEnabled = () => sceneContext.EditableScene
					}
				},
				{
					"ArrangeAll",
					new NamedAction()
					{
						ID = "ArrangeAll",
						Title = "Arrange All Parts".Localize(),
						Action = () =>
						{
							sceneContext.Scene.AutoArrangeChildren(this.BedCenter);
						},
						IsEnabled = () => sceneContext.EditableScene
					}
				},
				{
					"ClearBed",
					new NamedAction()
					{
						ID = "ClearBed",
						Title = "Clear Bed".Localize(),
						Action = () =>
						{
							UiThread.RunOnIdle(() =>
							{
								this.ClearPlate();
							});
						}
					}
				}
			};
		}

		private void ViewState_ViewModeChanged(object sender, ViewModeChangedEventArgs e)
		{
			this.modelViewStyleButton.Visible = e.ViewMode == PartViewMode.Model;
		}

		public Dictionary<string, NamedAction> WorkspaceActions { get; }

		private void ModelViewSidePanel_Resized(object sender, EventArgs e)
		{
			if (this.Printer !=null)
			{
				this.Printer.ViewState.SelectedObjectPanelWidth = selectedObjectPanel.Width;
			}
		}

		private void UpdateRenderView(object sender, EventArgs e)
		{
			TrackballTumbleWidget.CenterOffsetX  = -modelViewSidePanel.Width;
		}

		private void SceneContext_SceneLoaded(object sender, EventArgs e)
		{
			if (AppContext.IsLoading)
			{
				return;
			}

			if (printerTabPage?.printerActionsBar?.sliceButton is GuiWidget sliceButton)
			{
				sliceButton.Enabled = sceneContext.EditableScene;
			}

			if (printerTabPage?.printerActionsBar?.modelViewButton is GuiWidget button)
			{
				button.Enabled = sceneContext.EditableScene;

				if (sceneContext.ContentType == "gcode"
					&& printerTabPage?.printerActionsBar?.layers3DButton is GuiWidget gcodeButton)
				{
					gcodeButton.InvokeClick();
				}
			}

			this.RebuildTree();

			this.Invalidate();
		}

		public void PushToPrinterAndPrint()
		{
			// If invoked from a printer tab, simply start the print
			if (this.Printer != null)
			{
				// Save any pending changes before starting print operation
				ApplicationController.Instance.Tasks.Execute("Saving Changes".Localize(), printer.Bed.SaveChanges).ContinueWith(task =>
				{
					ApplicationController.Instance.PrintPart(
						printer.Bed.EditContext,
						printer,
						null,
						CancellationToken.None).ConfigureAwait(false);
				});
			}
			else if (ProfileManager.Instance.ActiveProfiles.Count() <= 0)
			{
				// If no printer profiles exist, show the printer setup wizard
				UiThread.RunOnIdle(() =>
				{
					var window = DialogWindow.Show(new SetupStepMakeModelName());
					window.Closed += (s2, e2) =>
					{
						if (ApplicationController.Instance.ActivePrinters.FirstOrDefault() is PrinterConfig printer
							&& printer.Settings.PrinterSelected)
						{
							CopyPlateToPrinter(sceneContext, printer);
						}
					};
				});
			}
			else if (ApplicationController.Instance.ActivePrinters.Count() is int printerCount && printerCount > 0)
			{
				if (printerCount == 1
					&& ApplicationController.Instance.ActivePrinters.FirstOrDefault() is PrinterConfig firstPrinter)
				{
					// If one printer exists, stash plate with undo operation, then load this scene onto the printer bed
					CopyPlateToPrinter(sceneContext, firstPrinter);
				}
				else
				{
					// If multiple active printers exist, show select printer dialog
					UiThread.RunOnIdle(() =>
					{
						DialogWindow.Show(
							new OpenPrinterPage(
								"Next".Localize(),
								(selectedPrinter) =>
								{
									if (selectedPrinter?.Settings.PrinterSelected == true)
									{
										CopyPlateToPrinter(sceneContext, selectedPrinter);
									}
								}));
					});
				}
			}
			else if (ProfileManager.Instance.ActiveProfiles.Any())
			{
				// If no active printer but profiles exist, show select printer
				UiThread.RunOnIdle(() =>
				{
					DialogWindow.Show(
						new OpenPrinterPage(
							"Next".Localize(),
							(loadedPrinter) =>
							{
								if (loadedPrinter is PrinterConfig activePrinter
									&& activePrinter.Settings.PrinterSelected)
								{
									CopyPlateToPrinter(sceneContext, activePrinter);
								}
							}));
				});
			}
		}

		private static void CopyPlateToPrinter(BedConfig sceneContext, PrinterConfig printer)
		{
			Task.Run(async () =>
			{
				await ApplicationController.Instance.Tasks.Execute("Saving".Localize(), sceneContext.SaveChanges);

				// Clear bed to get new MCX on disk for this item
				printer.Bed.ClearPlate();

				// Load current scene into new printer scene
				await printer.Bed.LoadIntoCurrent(sceneContext.EditContext);

				bool allInBounds = true;
				foreach (var item in printer.Bed.Scene.VisibleMeshes())
				{
					allInBounds &= printer.InsideBuildVolume(item);
				}

				if (!allInBounds)
				{
					var bounds = printer.Bed.Scene.GetAxisAlignedBoundingBox();
					var boundsCenter = bounds.Center;
					// don't move the z of our stuff
					boundsCenter.Z = 0;

					if (bounds.XSize <= printer.Bed.ViewerVolume.X
						&& bounds.YSize <= printer.Bed.ViewerVolume.Y)
					{
						// center the collection of stuff
						var bedCenter = new Vector3(printer.Bed.BedCenter);

						foreach (var item in printer.Bed.Scene.Children)
						{
							item.Matrix *= Matrix4X4.CreateTranslation(-boundsCenter + bedCenter);
						}
					}
					else
					{
						// arrange the stuff the best we can
						await printer.Bed.Scene.AutoArrangeChildren(new Vector3(printer.Bed.BedCenter));
					}
				}

				// Switch to printer
				ApplicationController.Instance.MainView.TabControl.SelectedTabKey = printer.Settings.GetValue(SettingsKey.printer_name);

				// Save any pending changes before starting print operation
				await ApplicationController.Instance.Tasks.Execute("Saving Changes".Localize(), printer.Bed.SaveChanges);

				// Slice and print
				await ApplicationController.Instance.PrintPart(
					printer.Bed.EditContext,
					printer,
					null,
					CancellationToken.None);
			});
		}

		private void ViewControls3D_TransformStateChanged(object sender, TransformStateChangedEventArgs e)
		{
			switch (e.TransformMode)
			{
				case ViewControls3DButtons.Rotate:
					TrackballTumbleWidget.TransformState = TrackBallTransformType.Rotation;
					break;

				case ViewControls3DButtons.Translate:
					TrackballTumbleWidget.TransformState = TrackBallTransformType.Translation;
					break;

				case ViewControls3DButtons.Scale:
					TrackballTumbleWidget.TransformState = TrackBallTransformType.Scale;
					break;

				case ViewControls3DButtons.PartSelect:
					TrackballTumbleWidget.TransformState = TrackBallTransformType.None;
					break;
			}
		}

		public void SelectAll()
		{
			Scene.ClearSelection();

			// Select All - set selection to all scene children
			Scene.SetSelection(Scene.Children.ToList());
		}

		private void Draw_GlOpaqueContent(object sender, DrawEventArgs e)
		{
			if (CurrentSelectInfo.DownOnPart
				&& TrackballTumbleWidget.TransformState == TrackBallTransformType.None
				&& Keyboard.IsKeyDown(Keys.ShiftKey))
			{
				// draw marks on the bed to show that the part is constrained to x and y
				AxisAlignedBoundingBox selectedBounds = Scene.SelectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

				var drawCenter = CurrentSelectInfo.PlaneDownHitPos;
				var drawColor = new Color(Color.Red, 20);
				bool zBuffer = false;

				for (int i = 0; i < 2; i++)
				{
					World.Render3DLine(
						drawCenter - new Vector3(-50, 0, 0),
						drawCenter - new Vector3(50, 0, 0), drawColor, zBuffer, 2);

					World.Render3DLine(
						drawCenter - new Vector3(0, -50, 0),
						drawCenter - new Vector3(0, 50, 0), drawColor, zBuffer, 2);

					drawColor = Color.Black;
					drawCenter.Z = 0;
					zBuffer = true;
				}

				GL.Enable(EnableCap.Lighting);
			}

			// Render 3D GCode if applicable
			if (sceneContext.LoadedGCode != null
				&& sceneContext.GCodeRenderer != null
				&& printerTabPage?.printer.ViewState.ViewMode == PartViewMode.Layers3D)
			{
				sceneContext.RenderGCode3D(e);
			}

			// This shows the BVH as rects around the scene items
			//Scene?.TraceData().RenderBvhRecursive(0, 3);
		}

		public void AddUndoOperation(IUndoRedoCommand operation)
		{
			Scene.UndoBuffer.Add(operation);
		}

		public bool DisplayAllValueData { get; set; }

		public override void OnClosed(EventArgs e)
		{
			viewControls3D.TransformStateChanged -= ViewControls3D_TransformStateChanged;

			// Release events
			this.Scene.SelectionChanged -= Scene_SelectionChanged;
			this.Scene.Invalidated -= Scene_Invalidated;
			this.InteractionLayer.DrawGlOpaqueContent -= Draw_GlOpaqueContent;

			sceneContext.SceneLoaded -= SceneContext_SceneLoaded;
			modelViewSidePanel.Resized -= ModelViewSidePanel_Resized;

			if (printer?.ViewState != null)
			{
				printer.ViewState.ViewModeChanged -= this.ViewState_ViewModeChanged;
			}

			if (meshViewerWidget != null)
			{
				meshViewerWidget.AfterDraw -= AfterDraw3DContent;
			}

			base.OnClosed(e);
		}

		private GuiWidget topMostParent;

		private PlaneShape bedPlane = new PlaneShape(Vector3.UnitZ, 0, null);

		public bool DragOperationActive { get; private set; }

		public InsertionGroupObject3D DragDropObject { get; private set; }

		public ILibraryAssetStream SceneReplacement { get; private set; }

		/// <summary>
		/// Provides a View3DWidget specific drag implementation
		/// </summary>
		/// <param name="screenSpaceMousePosition">The screen space mouse position.</param>
		public void ExternalDragOver(Vector2 screenSpaceMousePosition, GuiWidget sourceWidget)
		{
			if (this.HasBeenClosed)
			{
				return;
			}

			// If the mouse is within the MeshViewer process the Drag move
			var meshViewerScreenBounds = this.meshViewerWidget.TransformToScreenSpace(meshViewerWidget.LocalBounds);
			if (meshViewerScreenBounds.Contains(screenSpaceMousePosition))
			{
				// If already started, process drag move
				if (this.DragOperationActive)
				{
					this.DragOver(screenSpaceMousePosition);
				}
				else
				{
					if (this.Printer != null
						&& this.Printer.ViewState.ViewMode != PartViewMode.Model)
					{
						this.Printer.ViewState.ViewMode = PartViewMode.Model;
					}

					IEnumerable<ILibraryItem> selectedItems;

					if (sourceWidget is LibraryListView listView)
					{
						// Project from ListViewItem to ILibraryItem
						selectedItems = listView.SelectedItems.Select(l => l.Model);
					}
					else// Project from ListViewItem to ILibraryItem
					{
						selectedItems = Enumerable.Empty<ILibraryItem>();
					}

					// Otherwise begin an externally started DragDropOperation hard-coded to use LibraryView->SelectedItems
					this.StartDragDrop(selectedItems, screenSpaceMousePosition);
				}
			}
		}

		private void DragOver(Vector2 screenSpaceMousePosition)
		{
			IObject3D selectedItem = Scene.SelectedItem;
			// Move the object being dragged
			if (this.DragOperationActive
				&& this.DragDropObject != null
				&& selectedItem != null)
			{
				// Move the DropDropObject the target item
				DragSelectedObject(selectedItem, localMousePosition: meshViewerWidget.TransformFromScreenSpace(screenSpaceMousePosition));
			}
		}

		private void StartDragDrop(IEnumerable<ILibraryItem> items, Vector2 screenSpaceMousePosition, bool trackSourceFiles = false)
		{
			this.DragOperationActive = true;

			// ContentStore is null for plated gcode, call ClearPlate to exit mode and return to bed mcx
			if (sceneContext.EditContext.ContentStore == null)
			{
				this.ClearPlate();
			}

			var firstItem = items.FirstOrDefault();

			if ((firstItem is ILibraryAssetStream contentStream
				&& contentStream.ContentType == "gcode")
				|| firstItem is SceneReplacementFileItem)
			{
				DragDropObject = null;
				this.SceneReplacement = firstItem as ILibraryAssetStream;

				// TODO: Figure out a mechanism to disable View3DWidget with dark overlay, displaying something like "Switch to xxx.gcode", make disappear on mouseLeaveBounds and dragfinish
				this.InteractionLayer.BackgroundColor = new Color(Color.Black, 200);

				return;
			}

			// Set the hitplane to the bed plane
			CurrentSelectInfo.HitPlane = bedPlane;

			var insertionGroup = new InsertionGroupObject3D(
				items,
				this,
				Scene,
				Vector2.Zero,
				null,
				trackSourceFiles);

			// Find intersection position of the mouse with the bed plane
			var intersectInfo = GetIntersectPosition(screenSpaceMousePosition);
			if (intersectInfo != null)
			{
				CalculateDragStartPosition(insertionGroup, intersectInfo);
			}
			else
			{
				CurrentSelectInfo.LastMoveDelta = Vector3.PositiveInfinity;
			}

			this.deferEditorTillMouseUp = true;

			// Add item to scene and select it
			Scene.Children.Modify(list =>
			{
				list.Add(insertionGroup);
			});
			Scene.SelectedItem = insertionGroup;

			this.DragDropObject = insertionGroup;
		}

		private void CalculateDragStartPosition(IObject3D insertionGroup, IntersectInfo intersectInfo)
		{
			// Set the initial transform on the inject part to the current transform mouse position
			var sourceItemBounds = insertionGroup.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
			var center = sourceItemBounds.Center;

			insertionGroup.Matrix *= Matrix4X4.CreateTranslation(-center.X, -center.Y, -sourceItemBounds.minXYZ.Z);
			insertionGroup.Matrix *= Matrix4X4.CreateTranslation(new Vector3(intersectInfo.HitPosition));

			CurrentSelectInfo.PlaneDownHitPos = intersectInfo.HitPosition;
			CurrentSelectInfo.LastMoveDelta = Vector3.Zero;
		}

		internal void FinishDrop(bool mouseUpInBounds)
		{
			if (this.DragOperationActive)
			{
				this.InteractionLayer.BackgroundColor = Color.Transparent;
				this.DragOperationActive = false;

				if (mouseUpInBounds)
				{
					if (this.DragDropObject == null
						&& this.SceneReplacement != null)
					{
						// Drop handler for special case of GCode or similar (change loaded scene to new context)
						sceneContext.LoadContent(
							new EditContext()
							{
								SourceItem = this.SceneReplacement,
								// No content store for GCode
								ContentStore = null
							}).ConfigureAwait(false);

						this.SceneReplacement = null;
					}
				}
				else
				{
					Scene.Children.Modify(list => list.Remove(this.DragDropObject));
					Scene.ClearSelection();
				}

				this.DragDropObject = null;

				this.deferEditorTillMouseUp = false;
				Scene_SelectionChanged(null, null);

				Scene.Invalidate(new InvalidateArgs(null, InvalidateType.Content, null));

				// Set focus to View3DWidget after drag-drop
				UiThread.RunOnIdle(this.Focus);

			}
		}

		public override void OnLoad(EventArgs args)
		{
			topMostParent = this.TopmostParent();

			// Set reference on show
			var dragDropData = ApplicationController.Instance.DragDropData;
			dragDropData.View3DWidget = this;
			dragDropData.SceneContext = sceneContext;

			base.OnLoad(args);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var selectedItem = Scene.SelectedItem;

			if (selectedItem != null)
			{

				foreach (InteractionVolume volume in this.InteractionLayer.InteractionVolumes)
				{
					volume.SetPosition(selectedItem);
				}
			}

			base.OnDraw(graphics2D);

			if (selectedItem != null)
			{
				//DrawTestToGl(graphics2D, selectedItem);
			}
		}

		private void AfterDraw3DContent(object sender, DrawEventArgs e)
		{
			if (DragSelectionInProgress)
			{
				var selectionRectangle = new RectangleDouble(DragSelectionStartPosition, DragSelectionEndPosition);
				e.Graphics2D.Rectangle(selectionRectangle, Color.Red);
			}
		}

		bool foundTriangleInSelectionBounds;
		private void DoRectangleSelection(DrawEventArgs e)
		{
			var allResults = new List<BvhIterator>();

			var matchingSceneChildren = Scene.Children.Where(item =>
			{
				foundTriangleInSelectionBounds = false;

				// Filter the IPrimitive trace data finding matches as defined in InSelectionBounds
				var filteredResults = item.TraceData().Filter(InSelectionBounds);

				// Accumulate all matching BvhIterator results for debug rendering
				allResults.AddRange(filteredResults);

				return foundTriangleInSelectionBounds;
			});

			// Apply selection
			if (matchingSceneChildren.Any())
			{
				// If we are actually doing the selection rather than debugging the data
				if (e == null)
				{
					Scene.ClearSelection();
					Scene.SetSelection(matchingSceneChildren.ToList());
				}
				else
				{
					InteractionLayer.RenderBounds(e, World, allResults);
				}
			}
		}

		private bool InSelectionBounds(BvhIterator x)
		{
			var selectionRectangle = new RectangleDouble(DragSelectionStartPosition, DragSelectionEndPosition);

			Vector2[] traceBottoms = new Vector2[4];
			Vector2[] traceTops = new Vector2[4];

			if (foundTriangleInSelectionBounds)
			{
				return false;
			}
			if (x.Bvh is TriangleShape tri)
			{
				// check if any vertex in screen rect
				// calculate all the top and bottom screen positions
				for (int i = 0; i < 3; i++)
				{
					Vector3 bottomStartPosition = Vector3.Transform(tri.GetVertex(i), x.TransformToWorld);
					traceBottoms[i] = this.World.GetScreenPosition(bottomStartPosition);
				}

				for (int i = 0; i < 3; i++)
				{
					if (selectionRectangle.ClipLine(traceBottoms[i], traceBottoms[(i + 1) % 3]))
					{
						foundTriangleInSelectionBounds = true;
						return true;
					}
				}
			}
			else
			{
				// calculate all the top and bottom screen positions
				for (int i = 0; i < 4; i++)
				{
					Vector3 bottomStartPosition = Vector3.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetBottomCorner(i), x.TransformToWorld);
					traceBottoms[i] = this.World.GetScreenPosition(bottomStartPosition);

					Vector3 topStartPosition = Vector3.Transform(x.Bvh.GetAxisAlignedBoundingBox().GetTopCorner(i), x.TransformToWorld);
					traceTops[i] = this.World.GetScreenPosition(topStartPosition);
				}

				RectangleDouble.OutCode allPoints = RectangleDouble.OutCode.Inside;
				// check if we are inside all the points
				for (int i = 0; i < 4; i++)
				{
					allPoints |= selectionRectangle.ComputeOutCode(traceBottoms[i]);
					allPoints |= selectionRectangle.ComputeOutCode(traceTops[i]);
				}

				if (allPoints == RectangleDouble.OutCode.Surrounded)
				{
					return true;
				}

				for (int i = 0; i < 4; i++)
				{
					if (selectionRectangle.ClipLine(traceBottoms[i], traceBottoms[(i + 1) % 4])
						|| selectionRectangle.ClipLine(traceTops[i], traceTops[(i + 1) % 4])
						|| selectionRectangle.ClipLine(traceTops[i], traceBottoms[i]))
					{
						return true;
					}
				}
			}

			return false;
		}

		private ViewControls3DButtons? activeButtonBeforeMouseOverride = null;

		Vector2 lastMouseMove;
		Vector2 mouseDownPositon = Vector2.Zero;
		Matrix4X4 worldMatrixOnMouseDown;
		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			var selectedItem = Scene.SelectedItem;
			mouseDownPositon = mouseEvent.Position;
			worldMatrixOnMouseDown = World.GetTransform4X4();
			// Show transform override
			if (activeButtonBeforeMouseOverride == null
				&& (mouseEvent.Button == MouseButtons.Right || Keyboard.IsKeyDown(Keys.Control)))
			{
				if (Keyboard.IsKeyDown(Keys.Shift))
				{
					activeButtonBeforeMouseOverride = viewControls3D.ActiveButton;
					viewControls3D.ActiveButton = ViewControls3DButtons.Translate;
				}
				else if (Keyboard.IsKeyDown(Keys.Alt))
				{
					activeButtonBeforeMouseOverride = viewControls3D.ActiveButton;
					viewControls3D.ActiveButton = ViewControls3DButtons.Scale;
				}
				else
				{
					activeButtonBeforeMouseOverride = viewControls3D.ActiveButton;
					viewControls3D.ActiveButton = ViewControls3DButtons.Rotate;
				}
			}
			else if (activeButtonBeforeMouseOverride == null && mouseEvent.Button == MouseButtons.Middle)
			{
				activeButtonBeforeMouseOverride = viewControls3D.ActiveButton;
				viewControls3D.ActiveButton = ViewControls3DButtons.Translate;
			}

			if (mouseEvent.Button == MouseButtons.Right ||
				mouseEvent.Button == MouseButtons.Middle)
			{
				meshViewerWidget.SuppressUiVolumes = true;
			}

			base.OnMouseDown(mouseEvent);

			if (TrackballTumbleWidget.UnderMouseState == UnderMouseState.FirstUnderMouse
				&& sceneContext.ViewState.ModelView)
			{
				if (mouseEvent.Button == MouseButtons.Left
					&& viewControls3D.ActiveButton == ViewControls3DButtons.PartSelect
					&& ModifierKeys == Keys.Shift
					|| (
						TrackballTumbleWidget.TransformState == TrackBallTransformType.None
						&& ModifierKeys != Keys.Control
						&& ModifierKeys != Keys.Alt))
				{
					if (!this.InteractionLayer.MouseDownOnInteractionVolume)
					{
						meshViewerWidget.SuppressUiVolumes = true;

						IntersectInfo info = new IntersectInfo();

						IObject3D hitObject = FindHitObject3D(mouseEvent.Position, ref info);
						if (hitObject == null)
						{
							if (selectedItem != null)
							{
								Scene.ClearSelection();
								selectedItem = null;
							}

							// start a selection rect
							DragSelectionStartPosition = mouseEvent.Position - OffsetToMeshViewerWidget();
							DragSelectionEndPosition = DragSelectionStartPosition;
							DragSelectionInProgress = true;
						}
						else
						{
							CurrentSelectInfo.HitPlane = new PlaneShape(Vector3.UnitZ, CurrentSelectInfo.PlaneDownHitPos.Z, null);

							if (hitObject != selectedItem)
							{
								if (selectedItem == null)
								{
									// No selection exists
									Scene.SelectedItem = hitObject;
								}
								else if ((ModifierKeys == Keys.Shift || ModifierKeys == Keys.Control)
									&& !selectedItem.Children.Contains(hitObject))
								{
									Scene.AddToSelection(hitObject);
								}
								else if (selectedItem == hitObject || selectedItem.Children.Contains(hitObject))
								{
									// Selection should not be cleared and drag should occur
								}
								else if (ModifierKeys != Keys.Shift)
								{
									Scene.SelectedItem = hitObject;
								}

								// Selection may have changed, update local reference to current value
								selectedItem = Scene.SelectedItem;

								Invalidate();
							}

							TransformOnMouseDown = selectedItem.Matrix;

							Invalidate();
							CurrentSelectInfo.DownOnPart = true;

							AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

							if (info.HitPosition.X < selectedBounds.Center.X)
							{
								if (info.HitPosition.Y < selectedBounds.Center.Y)
								{
									CurrentSelectInfo.HitQuadrant = HitQuadrant.LB;
								}
								else
								{
									CurrentSelectInfo.HitQuadrant = HitQuadrant.LT;
								}
							}
							else
							{
								if (info.HitPosition.Y < selectedBounds.Center.Y)
								{
									CurrentSelectInfo.HitQuadrant = HitQuadrant.RB;
								}
								else
								{
									CurrentSelectInfo.HitQuadrant = HitQuadrant.RT;
								}
							}
						}
					}
				}
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			IObject3D selectedItem = Scene.SelectedItem;

			lastMouseMove = mouseEvent.Position;
			if(lastMouseMove != mouseDownPositon)
			{
				mouseDownPositon = Vector2.Zero;
			}

			// File system Drop validation
			mouseEvent.AcceptDrop = this.AllowDragDrop()
					&& mouseEvent.DragFiles?.Count > 0
					&& mouseEvent.DragFiles.TrueForAll(filePath =>
					{
						return ApplicationController.Instance.IsLoadableFile(filePath)
							// Disallow GCode drop in part view
							&& (this.Printer != null || !string.Equals(System.IO.Path.GetExtension(filePath), ".gcode", StringComparison.OrdinalIgnoreCase));
					});

			// View3DWidgets Filesystem DropDrop handler
			if (mouseEvent.AcceptDrop
				&& this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				if (this.DragOperationActive)
				{
					DragOver(screenSpaceMousePosition: this.TransformToScreenSpace(mouseEvent.Position));
				}
				else
				{
					// Project DragFiles to IEnumerable<FileSystemFileItem>
					this.StartDragDrop(
						mouseEvent.DragFiles.Select(path => new FileSystemFileItem(path)),
						screenSpaceMousePosition: this.TransformToScreenSpace(mouseEvent.Position),
						trackSourceFiles: true);
				}
			}

			if (CurrentSelectInfo.DownOnPart
				&& TrackballTumbleWidget.TransformState == TrackBallTransformType.None
				&& selectedItem != null)
			{
				DragSelectedObject(selectedItem, new Vector2(mouseEvent.X, mouseEvent.Y));
			}

			if (DragSelectionInProgress)
			{
				DragSelectionEndPosition = mouseEvent.Position - OffsetToMeshViewerWidget();
				DragSelectionEndPosition = new Vector2(
					Math.Max(Math.Min((double)DragSelectionEndPosition.X, meshViewerWidget.LocalBounds.Right), meshViewerWidget.LocalBounds.Left),
					Math.Max(Math.Min((double)DragSelectionEndPosition.Y, meshViewerWidget.LocalBounds.Top), meshViewerWidget.LocalBounds.Bottom));
				Invalidate();
			}

			base.OnMouseMove(mouseEvent);
		}

		public IntersectInfo GetIntersectPosition(Vector2 screenSpacePosition)
		{
			//Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, new Vector2(mouseEvent.X, mouseEvent.Y));

			// Translate to local
			Vector2 localPosition = meshViewerWidget.TransformFromScreenSpace(screenSpacePosition);

			Ray ray = this.World.GetRayForLocalBounds(localPosition);

			return CurrentSelectInfo.HitPlane.GetClosestIntersection(ray);
		}

		public void DragSelectedObject(IObject3D selectedItem, Vector2 localMousePosition)
		{
			Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, localMousePosition);
			Ray ray = this.World.GetRayForLocalBounds(meshViewerWidgetScreenPosition);

			if (!PositionWithinLocalBounds(localMousePosition.X, localMousePosition.Y))
			{
				Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(-CurrentSelectInfo.LastMoveDelta));
				selectedItem.Matrix *= totalTransform;
				CurrentSelectInfo.LastMoveDelta = Vector3.Zero;
				Invalidate();
				return;
			}

			IntersectInfo info = CurrentSelectInfo.HitPlane.GetClosestIntersection(ray);
			if (info != null)
			{
				if (CurrentSelectInfo.LastMoveDelta == Vector3.PositiveInfinity)
				{
					CalculateDragStartPosition(selectedItem, info);
				}

				// move the mesh back to the start position
				{
					Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(-CurrentSelectInfo.LastMoveDelta));
					selectedItem.Matrix *= totalTransform;
				}

				Vector3 delta = info.HitPosition - CurrentSelectInfo.PlaneDownHitPos;

				double snapGridDistance = this.InteractionLayer.SnapGridDistance;
				if (snapGridDistance > 0)
				{
					// snap this position to the grid
					AxisAlignedBoundingBox selectedBounds = selectedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

					double xSnapOffset = selectedBounds.minXYZ.X;
					// snap the x position
					if (CurrentSelectInfo.HitQuadrant == HitQuadrant.RB
						|| CurrentSelectInfo.HitQuadrant == HitQuadrant.RT)
					{
						// switch to the other side
						xSnapOffset = selectedBounds.maxXYZ.X;
					}
					double xToSnap = xSnapOffset + delta.X;

					double snappedX = ((int)((xToSnap / snapGridDistance) + .5)) * snapGridDistance;
					delta.X = snappedX - xSnapOffset;

					double ySnapOffset = selectedBounds.minXYZ.Y;
					// snap the y position
					if (CurrentSelectInfo.HitQuadrant == HitQuadrant.LT
						|| CurrentSelectInfo.HitQuadrant == HitQuadrant.RT)
					{
						// switch to the other side
						ySnapOffset = selectedBounds.maxXYZ.Y;
					}
					double yToSnap = ySnapOffset + delta.Y;

					double snappedY = ((int)((yToSnap / snapGridDistance) + .5)) * snapGridDistance;
					delta.Y = snappedY - ySnapOffset;
				}

				// if the shift key is down only move on the major axis of x or y
				if (Keyboard.IsKeyDown(Keys.ShiftKey))
				{
					if (Math.Abs(delta.X) < Math.Abs(delta.Y))
					{
						delta.X = 0;
					}
					else
					{
						delta.Y = 0;
					}
				}

				// move the mesh back to the new position
				{
					Matrix4X4 totalTransform = Matrix4X4.CreateTranslation(new Vector3(delta));

					selectedItem.Matrix *= totalTransform;

					CurrentSelectInfo.LastMoveDelta = delta;
				}

				Invalidate();
			}
		}

		Vector2 OffsetToMeshViewerWidget()
		{
			List<GuiWidget> parents = new List<GuiWidget>();
			GuiWidget parent = meshViewerWidget.Parent;
			while (parent != this)
			{
				parents.Add(parent);
				parent = parent.Parent;
			}
			Vector2 offset = new Vector2();
			for (int i = parents.Count - 1; i >= 0; i--)
			{
				offset += parents[i].OriginRelativeParent;
			}
			return offset;
		}

		public void ResetView()
		{
			TrackballTumbleWidget.ZeroVelocity();

			var world = this.World;

			world.Reset();
			world.Scale = .03;
			world.Translate(-new Vector3(sceneContext.BedCenter));
			world.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, -MathHelper.Tau / 16)));
			world.Rotate(Quaternion.FromEulerAngles(new Vector3(MathHelper.Tau * .19, 0, 0)));

			Invalidate();
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			var selectedItem = Scene.SelectedItem;
			if (this.DragOperationActive)
			{
				this.FinishDrop(mouseUpInBounds: true);
			}

			if (TrackballTumbleWidget.TransformState == TrackBallTransformType.None)
			{
				if (selectedItem != null
					&& CurrentSelectInfo.DownOnPart
					&& CurrentSelectInfo.LastMoveDelta != Vector3.Zero)
				{
					InteractionLayer.AddTransformSnapshot(TransformOnMouseDown);
				}
				else if (DragSelectionInProgress)
				{
					DoRectangleSelection(null);
					DragSelectionInProgress = false;
				}
			}

			meshViewerWidget.SuppressUiVolumes = false;

			CurrentSelectInfo.DownOnPart = false;

			if (activeButtonBeforeMouseOverride != null)
			{
				viewControls3D.ActiveButton = (ViewControls3DButtons)activeButtonBeforeMouseOverride;
				activeButtonBeforeMouseOverride = null;
			}

			// if we had a down and an up that did not move the view
			if (worldMatrixOnMouseDown == World.GetTransform4X4())
			{
				// and we are the first under mouse
				if (TrackballTumbleWidget.UnderMouseState == UnderMouseState.FirstUnderMouse)
				{
					// and the control key is pressed
					if (ModifierKeys == Keys.Control)
					{
						// find the think we clicked on
						IntersectInfo info = new IntersectInfo();
						var hitObject = FindHitObject3D(mouseEvent.Position, ref info);
						if (hitObject != null)
						{
							if (selectedItem == hitObject
								&& !(selectedItem is SelectionGroupObject3D))
							{
								Scene.SelectedItem = null;
							}
							else
							{
								IObject3D selectedHitItem = null;
								if (selectedItem != null)
								{
									foreach (Object3D object3D in selectedItem.Children)
									{
										if (object3D.TraceData().Contains(info.HitPosition))
										{
											CurrentSelectInfo.PlaneDownHitPos = info.HitPosition;
											CurrentSelectInfo.LastMoveDelta = new Vector3();
											selectedHitItem = object3D;
											break;
										}
									}
								}

								if (selectedHitItem != null)
								{
									selectedItem.Children.Remove(selectedHitItem);
									if(selectedItem.Children.Count == 0)
									{
										Scene.SelectedItem = null;
									}
									Scene.Children.Add(selectedHitItem);
								}
								else
								{
									Scene.AddToSelection(hitObject);
								}

								// Selection may have changed, update local reference to current value
								selectedItem = Scene.SelectedItem;
							}
						}
					}
				}
			}

			if (mouseEvent.Button == MouseButtons.Right
				&& mouseDownPositon == mouseEvent.Position
				&& this.TrackballTumbleWidget.FirstWidgetUnderMouse)
			{
				var info = new IntersectInfo();

				if (FindHitObject3D(mouseEvent.Position, ref info) is IObject3D hitObject
					&& (this.Printer == null // Allow Model -> Right Click in Part view
						|| this.Printer?.ViewState.ViewMode == PartViewMode.Model)) // Disallow Model -> Right Click in GCode views
				{
					// Object3D/hit item context menu
					if (hitObject != selectedItem)
					{
						Scene.SelectedItem = null;
						Scene.SelectedItem = hitObject;
						selectedItem = hitObject;
					}

					this.ShowPartContextMenu(mouseEvent, selectedItem);
				}
				else // Allow right click on bed in all modes
				{
					this.ShowBedContextMenu(mouseEvent.Position);
				}
			}

			base.OnMouseUp(mouseEvent);

			if (deferEditorTillMouseUp)
			{
				this.deferEditorTillMouseUp = false;
				Scene_SelectionChanged(null, null);
			}
		}

		private void ShowPartContextMenu(MouseEventArgs mouseEvent, IObject3D selectedItem)
		{
			UiThread.RunOnIdle(() =>
			{
				var menu = ApplicationController.Instance.GetActionMenuForSceneItem(selectedItem, Scene, true);

				var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
				systemWindow.ShowPopup(
					new MatePoint(this)
					{
						Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
						AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
					},
					new MatePoint(menu)
					{
						Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
						AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
					},
					altBounds: new RectangleDouble(mouseEvent.X + 1, mouseEvent.Y + 1, mouseEvent.X + 1, mouseEvent.Y + 1));

				var actions = new[] {
							new ActionSeparator(),
							WorkspaceActions["Cut"],
							WorkspaceActions["Copy"],
							WorkspaceActions["Paste"],
							new ActionSeparator(),
							new NamedAction()
							{
			 					Title = "Save As".Localize(),
								Action = () => UiThread.RunOnIdle(() =>
								{
									DialogWindow.Show(
										new SaveAsPage(
											async (newName, destinationContainer) =>
											{
												// Save to the destination provider
												if (destinationContainer is ILibraryWritableContainer writableContainer)
												{
													// Wrap stream with ReadOnlyStream library item and add to container
													writableContainer.Add(new[]
													{
														new InMemoryLibraryItem(selectedItem)
														{
															Name = newName
														}
													});

													destinationContainer.Dispose();
												}
											}));
								}),
								IsEnabled = () => sceneContext.EditableScene
							},
							new NamedAction()
							{
								ID = "Export",
								Title = "Export".Localize(),
								Icon = AggContext.StaticData.LoadIcon("cube_export.png", 16, 16, AppContext.MenuTheme.InvertIcons),
								Action = () =>
								{
									ApplicationController.Instance.ExportLibraryItems(
										new[]{ new InMemoryLibraryItem(selectedItem)},
										centerOnBed: false,
										printer: printer);
								}
							},
							new ActionSeparator(),
							WorkspaceActions["Delete"]
				};

				theme.CreateMenuItems(menu, actions, emptyMenu: false);

				menu.CreateSeparator();

				string componentID = (selectedItem as ComponentObject3D)?.ComponentID;

				var helpItem = menu.CreateMenuItem("Help".Localize());
				helpItem.Enabled = !string.IsNullOrEmpty(componentID) && ApplicationController.Instance.HelpArticlesByID.ContainsKey(componentID);
				helpItem.Click += (s, e) =>
				{
					DialogWindow.Show(new HelpPage(componentID));
				};
			});
		}

		public void ShowBedContextMenu(Vector2 position)
		{
			// Workspace/plate context menu
			UiThread.RunOnIdle(() =>
			{
				var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

				var actions = new[] {
					new ActionSeparator(),
					new NamedAction()
					{
						Title = "Paste".Localize(),
						Action = () =>
						{
							sceneContext.Paste();
						},
						IsEnabled = () => Clipboard.Instance.ContainsImage || Clipboard.Instance.GetText() == "!--IObjectSelection--!"
					},
					WorkspaceActions["Save"],
					WorkspaceActions["SaveAs"],
					WorkspaceActions["Export"],
					new ActionSeparator(),
					WorkspaceActions["Print"],
					new ActionSeparator(),
					WorkspaceActions["ArrangeAll"],
					WorkspaceActions["ClearBed"],
				};

				theme.CreateMenuItems(popupMenu, actions, emptyMenu: false);

				var popupBounds = new RectangleDouble(position.X + 1, position.Y + 1, position.X + 1, position.Y + 1);

				var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
				systemWindow.ShowPopup(
					new MatePoint(this)
					{
						Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
						AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
					},
					new MatePoint(popupMenu)
					{
						Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
						AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
					},
					altBounds: popupBounds);
			});
		}

		// TODO: Consider if we should always allow DragDrop or if we should prevent during printer or other scenarios
		private bool AllowDragDrop() => true;

		private void Scene_Invalidated(object sender, InvalidateArgs e)
		{
			if (e.InvalidateType == InvalidateType.Content)
			{
				UiThread.RunOnIdle(this.RebuildTree);
			}

			// Invalidate widget on scene invalidate
			this.Invalidate();
		}

		private void Scene_SelectionChanged(object sender, EventArgs e)
		{
			if (deferEditorTillMouseUp)
			{
				selectedObjectPanel.SetActiveItem(null);
			}
			else
			{
				var selectedItem = Scene.SelectedItem;

				// Change tree selection to current node
				if (selectedItem != null
					&& keyValues.TryGetValue(selectedItem, out TreeNode treeNode))
				{
					treeView.SelectedNode = treeNode;
				}
				else
				{
					// Clear the TreeView and release node references when no item is selected
					treeView.SelectedNode = null;
				}

				selectedObjectPanel.SetActiveItem(selectedItem);
			}
		}

		public void ClearPlate()
		{
			selectedObjectPanel.SetActiveItem(null);
			sceneContext.ClearPlate();
			sceneContext.Scene.UndoBuffer.ClearHistory();

			this.Invalidate();
		}

		public static Regex fileNameNumberMatch = new Regex("\\(\\d+\\)", RegexOptions.Compiled);

		private SelectedObjectPanel selectedObjectPanel;

		internal VerticalResizeContainer modelViewSidePanel;

		public Vector2 DragSelectionStartPosition { get; private set; }
		public bool DragSelectionInProgress { get; private set; }
		public Vector2 DragSelectionEndPosition { get; private set; }

		internal GuiWidget ShowOverflowMenu(PopupMenu popupMenu)
		{
			this.ShowBedViewOptions(popupMenu);

			return popupMenu;
		}

		internal void ShowBedViewOptions(PopupMenu popupMenu)
		{
			// TODO: Extend popup menu if applicable
			// popupMenu.CreateHorizontalLine();
		}

		public MeshViewerWidget meshViewerWidget;
		private bool assigningTreeNode;
		private FlowLayoutWidget treeNodeContainer;
		private InlineStringEdit workspaceName;

		public InteractiveScene Scene => sceneContext.Scene;

		protected ViewControls3D viewControls3D { get; }

		public MeshSelectInfo CurrentSelectInfo { get; } = new MeshSelectInfo();

		protected IObject3D FindHitObject3D(Vector2 screenPosition, ref IntersectInfo intersectionInfo)
		{
			Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, screenPosition);
			Ray ray = this.World.GetRayForLocalBounds(meshViewerWidgetScreenPosition);

			intersectionInfo = Scene.TraceData().GetClosestIntersection(ray);
			if (intersectionInfo != null)
			{
				foreach (Object3D object3D in Scene.Children)
				{
					if (object3D.TraceData().Contains(intersectionInfo.HitPosition))
					{
						CurrentSelectInfo.PlaneDownHitPos = intersectionInfo.HitPosition;
						CurrentSelectInfo.LastMoveDelta = new Vector3();
						return object3D;
					}
				}
			}

			return null;
		}

		public void Save()
		{
			ApplicationController.Instance.Tasks.Execute("Saving".Localize(), sceneContext.SaveChanges);
		}
	}

	public enum HitQuadrant { LB, LT, RB, RT }

	public class MeshSelectInfo
	{
		public HitQuadrant HitQuadrant;
		public bool DownOnPart;
		public PlaneShape HitPlane;
		public Vector3 LastMoveDelta;
		public Vector3 PlaneDownHitPos;
	}
}

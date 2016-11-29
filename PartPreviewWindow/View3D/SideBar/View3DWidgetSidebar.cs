/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class View3DWidgetSidebar : FlowLayoutWidget
	{
		private static string iconPath = Path.Combine("Icons", "3D Icons");
		private static string applicationUserDataPath = ApplicationDataStorage.ApplicationUserDataPath;
		private static string createdIconPath = Path.Combine(applicationUserDataPath, "data", "temp", "shape thumbnails");

		private View3DWidget view3DWidget;

		public CheckBox expandMaterialOptions { get; private set; }
		public CheckBox expandRotateOptions { get; private set; }
		public CheckBox expandViewOptions { get; private set; }

		public FlowLayoutWidget rotateOptionContainer;
		private FlowLayoutWidget viewOptionContainer;
		private FlowLayoutWidget materialOptionContainer;

		// TODO: Remove debugging variables and draw functions once drag items are positioning correctly
		private Vector2 mouseMovePosition;
		private RectangleDouble meshViewerPosition;
		private FlowLayoutWidget buttonPanel;

		public View3DWidgetSidebar(View3DWidget view3DWidget, double buildHeight, UndoBuffer undoBuffer)
			: base(FlowDirection.TopToBottom)
		{
			this.view3DWidget = view3DWidget;
			this.Width = 200;

			var ExpandMenuOptionFactory = view3DWidget.ExpandMenuOptionFactory;
			// put in undo redo
			{
				FlowLayoutWidget undoRedoButtons = new FlowLayoutWidget()
				{
					VAnchor = VAnchor.FitToChildren | VAnchor.ParentTop,
					HAnchor = HAnchor.FitToChildren | HAnchor.ParentCenter,
				};

				var WhiteButtonFactory = view3DWidget.WhiteButtonFactory;

				double oldWidth = WhiteButtonFactory.FixedWidth;
				WhiteButtonFactory.FixedWidth = WhiteButtonFactory.FixedWidth / 2;
				Button undoButton = WhiteButtonFactory.Generate("Undo".Localize(), centerText: true);
				undoButton.Name = "3D View Undo";
				undoButton.Enabled = false;
				undoButton.Click += (sender, e) =>
				{
					undoBuffer.Undo();
				};
				undoRedoButtons.AddChild(undoButton);

				Button redoButton = WhiteButtonFactory.Generate("Redo".Localize(), centerText: true);
				redoButton.Name = "3D View Redo";
				redoButton.Enabled = false;
				redoButton.Click += (sender, e) =>
				{
					undoBuffer.Redo();
				};
				undoRedoButtons.AddChild(redoButton);
				this.AddChild(undoRedoButtons);

				undoBuffer.Changed += (sender, e) =>
				{
					undoButton.Enabled = undoBuffer.UndoCount > 0;
					redoButton.Enabled = undoBuffer.RedoCount > 0;
				};
				WhiteButtonFactory.FixedWidth = oldWidth;
			}

			buttonPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.FitToChildren
			};
			this.AddChild(buttonPanel);

			{
				BorderDouble buttonMargin = new BorderDouble(top: 3);

				expandRotateOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton("Rotate".Localize().ToUpper(),
					View3DWidget.ArrowRight,
					View3DWidget.ArrowDown);
				expandRotateOptions.Margin = new BorderDouble(bottom: 2);
				expandRotateOptions.CheckedStateChanged += expandRotateOptions_CheckedStateChanged;

				buttonPanel.AddChild(expandRotateOptions);

				rotateOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
				rotateOptionContainer.HAnchor = HAnchor.ParentLeftRight;
				rotateOptionContainer.Visible = false;
				buttonPanel.AddChild(rotateOptionContainer);

				buttonPanel.AddChild(new ScaleControls(view3DWidget));

				buttonPanel.AddChild(new MirrorControls(view3DWidget));

				// put in the material options
				int numberOfExtruders = ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count);

				expandMaterialOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton(
					"Materials".Localize().ToUpper(),
					View3DWidget.ArrowRight,
					View3DWidget.ArrowDown);
				expandMaterialOptions.Margin = new BorderDouble(bottom: 2);
				expandMaterialOptions.CheckedStateChanged += expandMaterialOptions_CheckedStateChanged;

				if (numberOfExtruders > 1)
				{
					buttonPanel.AddChild(expandMaterialOptions);

					materialOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
					materialOptionContainer.HAnchor = HAnchor.ParentLeftRight;
					materialOptionContainer.Visible = false;

					buttonPanel.AddChild(materialOptionContainer);
					view3DWidget.AddMaterialControls(materialOptionContainer);
				}

				// put in the view options
				{
					expandViewOptions = ExpandMenuOptionFactory.GenerateCheckBoxButton("Display".Localize().ToUpper(),
					View3DWidget.ArrowRight,
					View3DWidget.ArrowDown);
					expandViewOptions.Margin = new BorderDouble(bottom: 2);
					buttonPanel.AddChild(expandViewOptions);
					expandViewOptions.CheckedStateChanged += expandViewOptions_CheckedStateChanged;

					viewOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
					viewOptionContainer.HAnchor = HAnchor.ParentLeftRight;
					viewOptionContainer.Padding = new BorderDouble(left: 4);
					viewOptionContainer.Visible = false;
					{
						CheckBox showBedCheckBox = new CheckBox("Show Print Bed".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
						showBedCheckBox.Checked = true;
						showBedCheckBox.CheckedStateChanged += (sender, e) =>
						{
							view3DWidget.meshViewerWidget.RenderBed = showBedCheckBox.Checked;
						};
						viewOptionContainer.AddChild(showBedCheckBox);

						if (buildHeight > 0)
						{
							CheckBox showBuildVolumeCheckBox = new CheckBox("Show Print Area".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
							showBuildVolumeCheckBox.Checked = false;
							showBuildVolumeCheckBox.Margin = new BorderDouble(bottom: 5);
							showBuildVolumeCheckBox.CheckedStateChanged += (sender, e) =>
							{
								view3DWidget.meshViewerWidget.RenderBuildVolume = showBuildVolumeCheckBox.Checked;
							};
							viewOptionContainer.AddChild(showBuildVolumeCheckBox);
						}

						if (UserSettings.Instance.IsTouchScreen)
						{
							UserSettings.Instance.set("defaultRenderSetting", RenderTypes.Shaded.ToString());
						}
						else
						{
							view3DWidget.CreateRenderTypeRadioButtons(viewOptionContainer);
						}
					}
					buttonPanel.AddChild(viewOptionContainer);
				}

				// Add vertical spacer
				this.AddChild(new GuiWidget(vAnchor: VAnchor.ParentBottomTop));

				AddGridSnapSettings(this);
			}

			this.Padding = new BorderDouble(6, 6);
			this.Margin = new BorderDouble(0, 1);
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.VAnchor = VAnchor.ParentBottomTop;
		}


		// InitializeComponent is called after the Sidebar property has been assigned as SidebarPlugins
		// are passed an instance of the View3DWidget and expect to be able to access the Sidebar to
		// call create button methods
		public void InitializeComponents()
		{
			PluginFinder<SideBarPlugin> SideBarPlugins = new PluginFinder<SideBarPlugin>();
			foreach (SideBarPlugin plugin in SideBarPlugins.Plugins)
			{
				buttonPanel.AddChild(plugin.CreateSideBarTool(view3DWidget));
			}

			HashSet<IObject3DEditor> mappedEditors;

			var objectEditorsByType = new Dictionary<Type, HashSet<IObject3DEditor>>();

			// TODO: Consider only loading once into a static
			var objectEditors = new PluginFinder<IObject3DEditor>().Plugins;
			foreach (IObject3DEditor editor in objectEditors)
			{
				foreach (Type type in editor.SupportedTypes())
				{
					if (!objectEditorsByType.TryGetValue(type, out mappedEditors))
					{
						mappedEditors = new HashSet<IObject3DEditor>();
						objectEditorsByType.Add(type, mappedEditors);
					}

					mappedEditors.Add(editor);
				}
			}

			view3DWidget.objectEditors = objectEditors;
			view3DWidget.objectEditorsByType = objectEditorsByType;
		}

		private void expandMaterialOptions_CheckedStateChanged(object sender, EventArgs e)
		{
			if (expandMaterialOptions.Checked == true)
			{
				expandRotateOptions.Checked = false;
				expandViewOptions.Checked = false;
			}
			materialOptionContainer.Visible = expandMaterialOptions.Checked;
		}

		private void expandRotateOptions_CheckedStateChanged(object sender, EventArgs e)
		{
			if (rotateOptionContainer.Visible != expandRotateOptions.Checked)
			{
				if (expandRotateOptions.Checked == true)
				{
					expandViewOptions.Checked = false;
					expandMaterialOptions.Checked = false;
				}
				rotateOptionContainer.Visible = expandRotateOptions.Checked;
			}
		}

		private void expandViewOptions_CheckedStateChanged(object sender, EventArgs e)
		{
			if (viewOptionContainer.Visible != expandViewOptions.Checked)
			{
				if (expandViewOptions.Checked == true)
				{
					expandRotateOptions.Checked = false;
					expandMaterialOptions.Checked = false;
				}
				viewOptionContainer.Visible = expandViewOptions.Checked;
			}
		}

		private void AddGridSnapSettings(GuiWidget widgetToAddTo)
		{
			FlowLayoutWidget container = new FlowLayoutWidget()
			{
				Margin = new BorderDouble(5, 0) * GuiWidget.DeviceScale,
			};

			TextWidget snapGridLabel = new TextWidget("Snap Grid".Localize())
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(3, 0, 0, 0) * GuiWidget.DeviceScale,
			};

			container.AddChild(snapGridLabel);

			var selectableOptions = new DropDownList("Custom", Direction.Up)
			{
				VAnchor = VAnchor.ParentCenter | VAnchor.FitToChildren,
			};

			Dictionary<double, string> snapSettings = new Dictionary<double, string>()
			{
				{ 0, "Off" },
				{ .1, "0.1" },
				{ .25, "0.25" },
				{ .5, "0.5" },
				{ 1, "1" },
				{ 2, "2" },
				{ 5, "5" },
			};

			foreach (KeyValuePair<double, string> snapSetting in snapSettings)
			{
				double valueLocal = snapSetting.Key;

				MenuItem newItem = selectableOptions.AddItem(snapSetting.Value);
				if (view3DWidget.meshViewerWidget.SnapGridDistance == valueLocal)
				{
					selectableOptions.SelectedLabel = snapSetting.Value;
				}

				newItem.Selected += (sender, e) =>
				{
					view3DWidget.meshViewerWidget.SnapGridDistance = snapSetting.Key;
				};
			}

			container.AddChild(selectableOptions);

			widgetToAddTo.AddChild(container);
		}

		public GuiWidget CreateAddButton(string buttonLable, string iconFileName, Func<IObject3D> createMeshFunction)
		{
			string iconPathAndFileName = Path.Combine(iconPath, iconFileName);
			ImageBuffer buttonImage = new ImageBuffer(64, 64, 32, new BlenderBGRA());
			if (StaticData.Instance.FileExists(iconPathAndFileName))
			{
				buttonImage = StaticData.Instance.LoadImage(iconPathAndFileName);
				buttonImage.SetRecieveBlender(new BlenderPreMultBGRA());
				buttonImage = ImageBuffer.CreateScaledImage(buttonImage, 64, 64);
			}
			else
			{
				iconPathAndFileName = Path.Combine(createdIconPath, iconFileName);
				if (File.Exists(iconPathAndFileName))
				{
					ImageIO.LoadImageData(iconPathAndFileName, buttonImage);
					buttonImage.SetRecieveBlender(new BlenderPreMultBGRA());
				}
				else
				{
					Task.Run(() =>
					{
						IObject3D item = createMeshFunction();

						// If the item has an empty mesh but children, flatten them into a mesh and swap the new item into place
						if (item.Mesh == null && item.HasChildren)
						{
							item = new Object3D()
							{
								ItemType = Object3DTypes.Model,
								Mesh = MeshFileIo.DoMerge(item.ToMeshGroupList(), new MeshOutputSettings())
							};
						}

						if (item.Mesh != null)
						{
							item.Mesh.Triangulate();

							ThumbnailTracer tracer = new ThumbnailTracer(item, 64, 64);
							tracer.DoTrace();

							buttonImage.SetRecieveBlender(new BlenderPreMultBGRA());
							//buttonImage = ImageBuffer.CreateScaledImage(tracer.destImage, 64, 64);
							buttonImage.CopyFrom(tracer.destImage);
							UiThread.RunOnIdle(() =>
							{
								if (!Directory.Exists(createdIconPath))
								{
									Directory.CreateDirectory(createdIconPath);
								}
								ImageIO.SaveImageData(iconPathAndFileName, buttonImage);
							});
						}
					});
				}
			}

			var textColor = ActiveTheme.Instance.PrimaryTextColor;
			GuiWidget addItemButton = CreateButtonState(buttonLable, buttonImage, ActiveTheme.Instance.PrimaryBackgroundColor, textColor);

			addItemButton.Margin = new BorderDouble(3);
			addItemButton.MouseDown += (sender, e) =>
			{
				view3DWidget.DragDropSource = createMeshFunction();
			};

			addItemButton.MouseMove += (sender, mouseArgs) =>
			{
				var screenSpaceMousePosition = addItemButton.TransformToScreenSpace(mouseArgs.Position);
				view3DWidget.AltDragOver(screenSpaceMousePosition);
			};

			addItemButton.MouseUp += (sender, mouseArgs) =>
			{
				if (addItemButton.LocalBounds.Contains(mouseArgs.Position) && view3DWidget.DragDropSource != null)
				{
					// Button click within the bounds of this control - Insert item at the best open position
					PlatingHelper.MoveToOpenPosition(view3DWidget.DragDropSource, view3DWidget.Scene);
					view3DWidget.InsertNewItem(view3DWidget.DragDropSource);
				}
				else if (view3DWidget.DragDropSource != null && view3DWidget.Scene.Children.Contains(view3DWidget.DragDropSource))
				{
					// Drag release outside the bounds of this control and not within the scene - Remove inserted item
					//
					// Mouse and widget positions
					var screenSpaceMousePosition = addItemButton.TransformToScreenSpace(mouseArgs.Position);
					meshViewerPosition = this.view3DWidget.meshViewerWidget.TransformToScreenSpace(view3DWidget.meshViewerWidget.LocalBounds);

					// If the mouse is not within the meshViewer, remove the inserted drag item
					if (!meshViewerPosition.Contains(screenSpaceMousePosition))
					{
						view3DWidget.Scene.ModifyChildren(children => children.Remove(view3DWidget.DragDropSource));
						view3DWidget.Scene.ClearSelection();
					}
					else
					{
						// Create and push the undo operation
						view3DWidget.AddUndoOperation(
							new InsertCommand(view3DWidget, view3DWidget.DragDropSource));
					}
				}

				view3DWidget.DragDropSource = null;
			};

			return addItemButton;
		}

		private static FlowLayoutWidget CreateButtonState(string buttonLable, ImageBuffer buttonImage, RGBA_Bytes color, RGBA_Bytes textColor)
		{
			FlowLayoutWidget flowLayout = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				BackgroundColor = color,
			};
			flowLayout.AddChild(new ImageWidget(buttonImage)
			{
				Margin = new BorderDouble(0, 5, 0, 0),
				BackgroundColor = RGBA_Bytes.Gray,
				HAnchor = HAnchor.ParentCenter,
				Selectable = false,
			});
			flowLayout.AddChild(new TextWidget(buttonLable, 0, 0, 9, Agg.Font.Justification.Center, textColor)
			{
				HAnchor = HAnchor.ParentCenter,
				Selectable = false,
			});
			return flowLayout;
		}
	}
}
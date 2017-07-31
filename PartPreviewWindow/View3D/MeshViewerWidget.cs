/*
Copyright (c) 2014, Lars Brubaker
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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MeshVisualizer
{
	public enum BedShape { Rectangular, Circular };

	public class DrawGlContentEventArgs : EventArgs
	{
		public bool ZBuffered { get; }

		public DrawGlContentEventArgs(bool zBuffered)
		{
			ZBuffered = zBuffered;
		}
	}

	public class MeshViewerWidget : GuiWidget
	{
		static public ImageBuffer BedImage = null;

		public GuiWidget ParentSurface { get; set; }

		public PartProcessingInfo partProcessingInfo;
		private static ImageBuffer lastCreatedBedImage = new ImageBuffer();

		private static Dictionary<int, RGBA_Bytes> extruderlColors = new Dictionary<int, RGBA_Bytes>();
		private RGBA_Bytes bedBaseColor = new RGBA_Bytes(245, 245, 255);
		static public Vector2 BedCenter { get; private set; }
		private RGBA_Bytes bedMarkingsColor = RGBA_Bytes.Black;
		private static BedShape bedShape = BedShape.Rectangular;
		private static Mesh buildVolume = null;
		private static Vector3 displayVolume;
		private static Mesh printerBed = null;
		private RenderTypes renderType = RenderTypes.Shaded;

		private InteractionLayer interactionLayer;

		public MeshViewerWidget(Vector3 displayVolume, Vector2 bedCenter, BedShape bedShape, TrackballTumbleWidget trackballTumbleWidget, InteractionLayer interactionLayer, string startingTextMessage = "")
		{
			interactionLayer.Scene = Scene;

			var activePrintItem = ApplicationController.Instance.ActivePrintItem;

			if (activePrintItem != null 
				&& File.Exists(activePrintItem.FileLocation))
			{
				Scene.Load(activePrintItem.FileLocation);
			}

			this.interactionLayer = interactionLayer;
			this.World = interactionLayer.World;
			
			Scene.SelectionChanged += (sender, e) =>
			{
				Invalidate();
			};
			RenderType = RenderTypes.Shaded;
			RenderBed = true;
			RenderBuildVolume = false;
			BedColor = new RGBA_Floats(.8, .8, .8, .7).GetAsRGBA_Bytes();
			BuildVolumeColor = new RGBA_Floats(.2, .8, .3, .2).GetAsRGBA_Bytes();

			CreatePrintBed(displayVolume, bedCenter, bedShape);

			partProcessingInfo = new PartProcessingInfo(startingTextMessage);

			GuiWidget labelContainer = new GuiWidget();
			labelContainer.AnchorAll();
			labelContainer.AddChild(partProcessingInfo);
			labelContainer.Selectable = false;

			SetExtruderColor(0, ActiveTheme.Instance.PrimaryAccentColor);

			this.AddChild(labelContainer);

			this.trackballTumbleWidget = trackballTumbleWidget;
			this.trackballTumbleWidget.DrawGlContent += this.trackballTumbleWidget_DrawGlContent;
		}

		public override void OnParentChanged(EventArgs e)
		{
			this.ParentSurface = this.Parent;
			base.OnParentChanged(e);
		}

		public WorldView World { get; }

		public event EventHandler LoadDone;

		public bool AllowBedRenderingWhenEmpty { get; set; }

		public RGBA_Bytes BedColor { get; set; }

		public RGBA_Bytes BuildVolumeColor { get; set; }

		public Vector3 DisplayVolume { get { return displayVolume; } }

		public static AxisAlignedBoundingBox GetAxisAlignedBoundingBox(List<MeshGroup> meshGroups)
		{
			AxisAlignedBoundingBox totalMeshBounds = AxisAlignedBoundingBox.Empty;
			bool first = true;
			foreach (MeshGroup meshGroup in meshGroups)
			{
				AxisAlignedBoundingBox meshBounds = meshGroup.GetAxisAlignedBoundingBox();
				if (first)
				{
					totalMeshBounds = meshBounds;
					first = false;
				}
				else
				{
					totalMeshBounds = AxisAlignedBoundingBox.Union(totalMeshBounds, meshBounds);
				}
			}

			return totalMeshBounds;
		}

		public override void OnLoad(EventArgs args)
		{
			// some debug code to be able to click on parts
			if (false)
			{
				AfterDraw += (sender, e) =>
				{
					foreach (var child in Scene.Children)
					{
						this.World.RenderDebugAABB(e.graphics2D, child.TraceData().GetAxisAlignedBoundingBox());
						this.World.RenderDebugAABB(e.graphics2D, child.GetAxisAlignedBoundingBox(Matrix4X4.Identity));
					}
				};
			}

			base.OnLoad(args);
		}

		public override void FindNamedChildrenRecursive(string nameToSearchFor, List<WidgetAndPosition> foundChildren, RectangleDouble touchingBounds, SearchType seachType, bool allowInvalidItems = true)
		{
			foreach (var child in Scene.Children)
			{
				string object3DName = child.Name;
				if (object3DName == null && child.MeshPath != null)
				{
					object3DName = Path.GetFileName(child.MeshPath);
				}

				bool nameFound = false;

				if (seachType == SearchType.Exact)
				{
					if (object3DName == nameToSearchFor)
					{
						nameFound = true;
					}
				}
				else
				{
					if (nameToSearchFor == ""
						|| object3DName.Contains(nameToSearchFor))
					{
						nameFound = true;
					}
				}

				if (nameFound)
				{
					AxisAlignedBoundingBox bounds = child.TraceData().GetAxisAlignedBoundingBox();

					RectangleDouble screenBoundsOfObject3D = RectangleDouble.ZeroIntersection;
					for(int i=0; i<4; i++)
					{
						screenBoundsOfObject3D.ExpandToInclude(this.World.GetScreenPosition(bounds.GetTopCorner(i)));
						screenBoundsOfObject3D.ExpandToInclude(this.World.GetScreenPosition(bounds.GetBottomCorner(i)));
					}

					if (touchingBounds.IsTouching(screenBoundsOfObject3D))
					{
						Vector3 renderPosition = bounds.Center;
						Vector2 objectCenterScreenSpace = this.World.GetScreenPosition(renderPosition);
						Point2D screenPositionOfObject3D = new Point2D((int)objectCenterScreenSpace.x, (int)objectCenterScreenSpace.y);

						foundChildren.Add(new WidgetAndPosition(this, screenPositionOfObject3D, object3DName));
					}
				}
			}

			base.FindNamedChildrenRecursive(nameToSearchFor, foundChildren, touchingBounds, seachType, allowInvalidItems);
		}

		public InteractiveScene Scene { get; } = new InteractiveScene();

		public Mesh PrinterBed { get { return printerBed; } }

		public bool RenderBed { get; set; }

		public bool RenderBuildVolume { get; set; }

		public RenderTypes RenderType
		{
			get => this.IsActive ? renderType : RenderTypes.Wireframe;
			set
			{
				if (renderType != value)
				{
					renderType = value;
					foreach(var renderTransfrom in Scene.VisibleMeshes(Matrix4X4.Identity))
					{
						renderTransfrom.MeshData.MarkAsChanged();
					}
				}
			}
		}

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}

		public static RGBA_Bytes GetExtruderColor(int extruderIndex)
		{
			lock (extruderlColors)
			{
				if (extruderlColors.ContainsKey(extruderIndex))
				{
					return extruderlColors[extruderIndex];
				}
			}

			// we currently expect at most 4 extruders
			return RGBA_Floats.FromHSL((extruderIndex % 4) / 4.0, .5, .5).GetAsRGBA_Bytes();
		}

		public static RGBA_Bytes GetSelectedExtruderColor(int extruderIndex)
		{
			double hue0To1;
			double saturation0To1;
			double lightness0To1;
			GetExtruderColor(extruderIndex).GetAsRGBA_Floats().GetHSL(out hue0To1, out saturation0To1, out lightness0To1);

			// now make it a bit lighter and less saturated
			saturation0To1 = Math.Min(1, saturation0To1 * 2);
			lightness0To1 = Math.Min(1, lightness0To1 * 1.2);

			// we sort of expect at most 4 extruders
			return RGBA_Floats.FromHSL(hue0To1, saturation0To1, lightness0To1).GetAsRGBA_Bytes();
		}

		public static void SetExtruderColor(int extruderIndex, RGBA_Bytes color)
		{
			lock (extruderlColors)
			{
				if (!extruderlColors.ContainsKey(extruderIndex))
				{
					extruderlColors.Add(extruderIndex, color);
				}
				else
				{
					extruderlColors[extruderIndex] = color;
				}
			}
		}

		public void CreateGlDataObject(IObject3D item)
		{
			if(item.Mesh != null)
			{
				GLMeshTrianglePlugin.Get(item.Mesh);
			}

			foreach (IObject3D child in item.Children.Where(o => o.Mesh != null))
			{
				GLMeshTrianglePlugin.Get(child.Mesh);
			}
		}

		public void CreatePrintBed(Vector3 displayVolume, Vector2 bedCenter, BedShape bedShape)
		{
			if (MeshViewerWidget.BedCenter == bedCenter
				&& MeshViewerWidget.bedShape == bedShape
				&& MeshViewerWidget.displayVolume == displayVolume)
			{
				return;
			}

			MeshViewerWidget.BedCenter = bedCenter;
			MeshViewerWidget.bedShape = bedShape;
			MeshViewerWidget.displayVolume = displayVolume;
			Vector3 displayVolumeToBuild = Vector3.ComponentMax(displayVolume, new Vector3(1, 1, 1));

			double sizeForMarking = Math.Max(displayVolumeToBuild.x, displayVolumeToBuild.y);
			double divisor = 10;
			int skip = 1;
			if (sizeForMarking > 1000)
			{
				divisor = 100;
				skip = 10;
			}
			else if (sizeForMarking > 300)
			{
				divisor = 50;
				skip = 5;
			}

			switch (bedShape)
			{
				case BedShape.Rectangular:
					if (displayVolumeToBuild.z > 0)
					{
						buildVolume = PlatonicSolids.CreateCube(displayVolumeToBuild);
						foreach (Vertex vertex in buildVolume.Vertices)
						{
							vertex.Position = vertex.Position + new Vector3(0, 0, displayVolumeToBuild.z / 2);
						}
					}
					CreateRectangularBedGridImage(displayVolumeToBuild, bedCenter, divisor, skip);
					printerBed = PlatonicSolids.CreateCube(displayVolumeToBuild.x, displayVolumeToBuild.y, 1.8);
					{
						Face face = printerBed.Faces[0];
						MeshHelper.PlaceTextureOnFace(face, BedImage);
					}
					break;

				case BedShape.Circular:
					{
						if (displayVolumeToBuild.z > 0)
						{
							buildVolume = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolumeToBuild.x / 2, displayVolumeToBuild.y / 2), displayVolumeToBuild.z);
							foreach (Vertex vertex in buildVolume.Vertices)
							{
								vertex.Position = vertex.Position + new Vector3(0, 0, .2);
							}
						}
						CreateCircularBedGridImage((int)(displayVolumeToBuild.x / divisor), (int)(displayVolumeToBuild.y / divisor), skip);
						printerBed = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolumeToBuild.x / 2, displayVolumeToBuild.y / 2), 2);
						{
							foreach (Face face in printerBed.Faces)
							{
								if (face.normal.z > 0)
								{
									face.SetTexture(0, BedImage);
									foreach (FaceEdge faceEdge in face.FaceEdges())
									{
										faceEdge.SetUv(0, new Vector2((displayVolumeToBuild.x / 2 + faceEdge.FirstVertex.Position.x) / displayVolumeToBuild.x,
											(displayVolumeToBuild.y / 2 + faceEdge.FirstVertex.Position.y) / displayVolumeToBuild.y));
									}
								}
							}
						}
					}
					break;

				default:
					throw new NotImplementedException();
			}

			var zHeight = printerBed.GetAxisAlignedBoundingBox().ZSize;
			foreach (Vertex vertex in printerBed.Vertices)
			{
				vertex.Position = vertex.Position - new Vector3(-bedCenter, zHeight/2);
			}

			if (buildVolume != null)
			{
				foreach (Vertex vertex in buildVolume.Vertices)
				{
					vertex.Position = vertex.Position - new Vector3(-bedCenter, 2.2);
				}
			}

			Invalidate();
		}

		public bool SuppressUiVolumes { get; set; } = false;

		private CancellationTokenSource fileLoadCancellationTokenSource;

		public async Task LoadItemIntoScene(string itemPath, Vector2 bedCenter = new Vector2(), string itemName = null)
		{
			if (File.Exists(itemPath))
			{
				BeginProgressReporting("Loading Mesh");

				fileLoadCancellationTokenSource = new CancellationTokenSource();

				// TODO: How to we handle mesh load errors? How do we report success?
				IObject3D loadedItem = await Task.Run(() => Object3D.Load(itemPath, fileLoadCancellationTokenSource.Token, progress: ReportProgress0to100));
				if (loadedItem != null)
				{
					if (itemName != null)
					{
						loadedItem.Name = itemName;
					}

					// SetMeshAfterLoad
					Scene.ModifyChildren(children =>
					{
						if (loadedItem.Mesh != null)
						{
							// STLs currently load directly into the mesh rather than as a group like AMF
							children.Add(loadedItem);
						}
						else
						{
							children.AddRange(loadedItem.Children);
						}
					});

					CreateGlDataObject(loadedItem);
				}
				else
				{
					partProcessingInfo.centeredInfoText.Text = string.Format("Sorry! No 3D view available\nfor this file.");
				}

				EndProgressReporting();

				// Invoke LoadDone event
				LoadDone?.Invoke(this, null);
			}
			else
			{
				partProcessingInfo.centeredInfoText.Text = string.Format("{0}\n'{1}'", "File not found on disk.", Path.GetFileName(itemPath));
			}

			fileLoadCancellationTokenSource = null;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			fileLoadCancellationTokenSource?.Cancel();
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			//if (!SuppressUiVolumes)
			{
				foreach (InteractionVolume interactionVolume in interactionLayer.InteractionVolumes)
				{
					interactionVolume.Draw2DContent(graphics2D);
				}
			}
		}

		private void CreateCircularBedGridImage(int linesInX, int linesInY, int increment = 1)
		{
			Vector2 bedImageCentimeters = new Vector2(linesInX, linesInY);
			BedImage = new ImageBuffer(1024, 1024);
			Graphics2D graphics2D = BedImage.NewGraphics2D();
			graphics2D.Clear(bedBaseColor);
			{
				double lineDist = BedImage.Width / (double)linesInX;

				int count = 1;
				int pointSize = 16;
				graphics2D.DrawString(count.ToString(), 4, 4, pointSize, color: bedMarkingsColor);
				double currentRadius = lineDist;
				Vector2 bedCenter = new Vector2(BedImage.Width / 2, BedImage.Height / 2);
				for (double linePos = lineDist + BedImage.Width / 2; linePos < BedImage.Width; linePos += lineDist)
				{
					int linePosInt = (int)linePos;
					graphics2D.DrawString((count * increment).ToString(), linePos + 2, BedImage.Height / 2, pointSize, color: bedMarkingsColor);

					Ellipse circle = new Ellipse(bedCenter, currentRadius);
					Stroke outline = new Stroke(circle);
					graphics2D.Render(outline, bedMarkingsColor);
					currentRadius += lineDist;
					count++;
				}

				graphics2D.Line(0, BedImage.Height / 2, BedImage.Width, BedImage.Height / 2, bedMarkingsColor);
				graphics2D.Line(BedImage.Width / 2, 0, BedImage.Width / 2, BedImage.Height, bedMarkingsColor);
			}
		}

		private void CreateRectangularBedGridImage(Vector3 displayVolumeToBuild, Vector2 bedCenter, double divisor, double skip)
		{
			lock (lastCreatedBedImage)
			{
				BedImage = new ImageBuffer(1024, 1024);
				Graphics2D graphics2D = BedImage.NewGraphics2D();
				graphics2D.Clear(bedBaseColor);
				{
					double lineDist = BedImage.Width / (displayVolumeToBuild.x / divisor);

					double xPositionCm = (-(displayVolume.x / 2.0) + bedCenter.x) / divisor;
					int xPositionCmInt = (int)Math.Round(xPositionCm);
					double fraction = xPositionCm - xPositionCmInt;
					int pointSize = 20;
					graphics2D.DrawString((xPositionCmInt * skip).ToString(), 4, 4, pointSize, color: bedMarkingsColor);
					for (double linePos = lineDist * (1 - fraction); linePos < BedImage.Width; linePos += lineDist)
					{
						xPositionCmInt++;
						int linePosInt = (int)linePos;
						int lineWidth = 1;
						if (xPositionCmInt == 0)
						{
							lineWidth = 2;
						}
						graphics2D.Line(linePosInt, 0, linePosInt, BedImage.Height, bedMarkingsColor, lineWidth);
						graphics2D.DrawString((xPositionCmInt * skip).ToString(), linePos + 4, 4, pointSize, color: bedMarkingsColor);
					}
				}
				{
					double lineDist = BedImage.Height / (displayVolumeToBuild.y / divisor);

					double yPositionCm = (-(displayVolume.y / 2.0) + bedCenter.y) / divisor;
					int yPositionCmInt = (int)Math.Round(yPositionCm);
					double fraction = yPositionCm - yPositionCmInt;
					int pointSize = 20;
					for (double linePos = lineDist * (1 - fraction); linePos < BedImage.Height; linePos += lineDist)
					{
						yPositionCmInt++;
						int linePosInt = (int)linePos;
						int lineWidth = 1;
						if (yPositionCmInt == 0)
						{
							lineWidth = 2;
						}
						graphics2D.Line(0, linePosInt, BedImage.Height, linePosInt, bedMarkingsColor, lineWidth);

						graphics2D.DrawString((yPositionCmInt * skip).ToString(), 4, linePos + 4, pointSize, color: bedMarkingsColor);
					}
				}

				lastCreatedBedImage = BedImage;
			}
		}

		private string progressReportingPrimaryTask = "";
		private TrackballTumbleWidget trackballTumbleWidget;

		public void BeginProgressReporting(string taskDescription)
		{
			progressReportingPrimaryTask = taskDescription;

			partProcessingInfo.Visible = true;
			partProcessingInfo.progressControl.PercentComplete = 0;
			partProcessingInfo.centeredInfoText.Text = taskDescription + "...";
		}

		public void EndProgressReporting()
		{
			progressReportingPrimaryTask = "";
			partProcessingInfo.Visible = false;
		}

		public void ReportProgress0to100(double progress0To1, string processingState)
		{
			UiThread.RunOnIdle(() =>
			{
				int percentComplete = (int)(progress0To1 * 100);
				partProcessingInfo.centeredInfoText.Text =  "{0} {1}%...".FormatWith(progressReportingPrimaryTask, percentComplete);
				partProcessingInfo.progressControl.PercentComplete = percentComplete;

				// Only assign to textbox if value passed through
				if (processingState != null)
				{
					partProcessingInfo.centeredInfoDescription.Text = processingState;
				}
			});
		}

		public bool IsActive { get; set; } = true;

		private void DrawObject(IObject3D object3D, Matrix4X4 transform, bool parentSelected)
		{
			foreach(MeshAndTransform meshAndTransform in object3D.VisibleMeshes(transform))
			{
				bool isSelected = parentSelected ||
					Scene.HasSelection && (object3D == Scene.SelectedItem || Scene.SelectedItem.Children.Contains(object3D));

				RGBA_Bytes drawColor = object3D.Color;
				if(object3D.OutputType == PrintOutputTypes.Support)
				{
					drawColor = new RGBA_Bytes(RGBA_Bytes.Yellow, 120);
				}
				else if(object3D.OutputType == PrintOutputTypes.Hole)
				{
					drawColor = new RGBA_Bytes(RGBA_Bytes.Gray, 120);
				}

				if (drawColor.Alpha0To1 == 0)
				{
					int extruderIndex = Math.Max(0, object3D.ExtruderIndex);
					drawColor = isSelected ? GetSelectedExtruderColor(extruderIndex) : GetExtruderColor(extruderIndex);
				}
				
				GLHelper.Render(meshAndTransform.MeshData, drawColor, meshAndTransform.Matrix, RenderType);
			}
		}

		private void trackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
		{
			foreach(var object3D in Scene.Children)
			{
				DrawObject(object3D, Matrix4X4.Identity, false);
			}

			if (RenderBed)
			{
				GLHelper.Render(printerBed, this.BedColor);
			}

			if (buildVolume != null && RenderBuildVolume)
			{
				GLHelper.Render(buildVolume, this.BuildVolumeColor);
			}

			// we don't want to render the bed or build volume before we load a model.
			if (Scene.HasChildren || AllowBedRenderingWhenEmpty)
			{
				if (false) // this is code to draw a small axis indicator
				{
					double big = 10;
					double small = 1;
					Mesh xAxis = PlatonicSolids.CreateCube(big, small, small);
					GLHelper.Render(xAxis, RGBA_Bytes.Red);
					Mesh yAxis = PlatonicSolids.CreateCube(small, big, small);
					GLHelper.Render(yAxis, RGBA_Bytes.Green);
					Mesh zAxis = PlatonicSolids.CreateCube(small, small, big);
					GLHelper.Render(zAxis, RGBA_Bytes.Blue);
				}
			}

			DrawInteractionVolumes(e);
		}

		private void DrawInteractionVolumes(EventArgs e)
		{
			if(SuppressUiVolumes)
			{
				return;
			}

			// draw on top of anything that is already drawn
			foreach (InteractionVolume interactionVolume in interactionLayer.InteractionVolumes)
			{
				if (interactionVolume.DrawOnTop)
				{
					GL.Disable(EnableCap.DepthTest);
					interactionVolume.DrawGlContent(new DrawGlContentEventArgs(false));
					GL.Enable(EnableCap.DepthTest);
				}
			}

			// Draw again setting the depth buffer and ensuring that all the interaction objects are sorted as well as we can
			foreach (InteractionVolume interactionVolume in interactionLayer.InteractionVolumes)
			{
				interactionVolume.DrawGlContent(new DrawGlContentEventArgs(true));
			}
		}

		public class PartProcessingInfo : FlowLayoutWidget
		{
			internal TextWidget centeredInfoDescription;
			internal TextWidget centeredInfoText;
			internal ProgressControl progressControl;

			internal PartProcessingInfo(string startingTextMessage)
				: base(FlowDirection.TopToBottom)
			{
				progressControl = new ProgressControl("", RGBA_Bytes.Black, RGBA_Bytes.Black);
				progressControl.HAnchor = HAnchor.ParentCenter;
				AddChild(progressControl);
				progressControl.Visible = false;
				progressControl.ProgressChanged += (sender, e) =>
				{
					progressControl.Visible = true;
				};

				centeredInfoText = new TextWidget(startingTextMessage);
				centeredInfoText.HAnchor = HAnchor.ParentCenter;
				centeredInfoText.AutoExpandBoundsToText = true;
				AddChild(centeredInfoText);

				centeredInfoDescription = new TextWidget("");
				centeredInfoDescription.HAnchor = HAnchor.ParentCenter;
				centeredInfoDescription.AutoExpandBoundsToText = true;
				AddChild(centeredInfoDescription);

				VAnchor |= VAnchor.ParentCenter;
				HAnchor |= HAnchor.ParentCenter;
			}
		}
	}
}
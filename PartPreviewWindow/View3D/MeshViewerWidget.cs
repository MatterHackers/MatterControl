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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
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

	public static class MatterialRendering
	{
		public static RGBA_Bytes Color(int materialIndex)
		{
			return RGBA_Floats.FromHSL(Math.Max(materialIndex, 0) / 10.0, .99, .49).GetAsRGBA_Bytes();
		}
	}

	public class MeshViewerWidget : GuiWidget
	{
		// TODO: Need to be instance based for multi-printer
		public GuiWidget ParentSurface { get; set; }

		private RenderTypes renderType = RenderTypes.Shaded;

		private InteractionLayer interactionLayer;

		private PrinterConfig printer;

		public MeshViewerWidget(PrinterConfig printer, TrackballTumbleWidget trackballTumbleWidget, InteractionLayer interactionLayer, string startingTextMessage = "", EditorType editorType = EditorType.Part)
		{
			this.EditorMode = editorType;
			this.scene = printer.Bed.Scene;
			this.printer = printer;

			var activePrintItem = ApplicationController.Instance.ActivePrintItem;

			if (activePrintItem != null 
				&& File.Exists(activePrintItem.FileLocation))
			{
				scene.Load(activePrintItem.FileLocation);
			}

			this.interactionLayer = interactionLayer;
			this.World = interactionLayer.World;
			
			scene.SelectionChanged += (sender, e) =>
			{
				Invalidate();
			};
			RenderType = RenderTypes.Shaded;
			RenderBed = true;
			RenderBuildVolume = false;
			BedColor = new RGBA_Floats(.8, .8, .8, .7).GetAsRGBA_Bytes();
			BuildVolumeColor = new RGBA_Floats(.2, .8, .3, .2).GetAsRGBA_Bytes();

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
					foreach (var child in scene.Children)
					{
						this.World.RenderDebugAABB(e.graphics2D, child.TraceData().GetAxisAlignedBoundingBox());
						this.World.RenderDebugAABB(e.graphics2D, child.GetAxisAlignedBoundingBox(Matrix4X4.Identity));
					}
				};
			}

			UiThread.RunOnIdle(() =>
			{
				Task.Run(() =>
				{
					try
					{
						string url = printer.Settings.GetValue("PrinterShapeUrl");
						string extension = printer.Settings.GetValue("PrinterShapeExtension");

						using (var stream = ApplicationController.Instance.LoadHttpAsset(url))
						{
							var mesh = MeshFileIo.Load(stream, extension, CancellationToken.None).Mesh;
							UiThread.RunOnIdle(() =>
							{
								printerShape = mesh;
								this.Invalidate();
							});
						}
					}
					catch { }

				});
			});

			base.OnLoad(args);
		}

		public override void FindNamedChildrenRecursive(string nameToSearchFor, List<WidgetAndPosition> foundChildren, RectangleDouble touchingBounds, SearchType seachType, bool allowInvalidItems = true)
		{
			foreach (var child in scene.Children)
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

		protected InteractiveScene scene { get; }

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
					foreach(var renderTransfrom in scene.VisibleMeshes(Matrix4X4.Identity))
					{
						renderTransfrom.Mesh.MarkAsChanged();
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
			return MatterialRendering.Color(extruderIndex);
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

		public bool SuppressUiVolumes { get; set; } = false;

		private CancellationTokenSource fileLoadCancellationTokenSource;

		public async Task LoadItemIntoScene(string itemPath, Vector2 bedCenter = new Vector2(), string itemName = null)
		{
			if (File.Exists(itemPath))
			{
				interactionLayer.BeginProgressReporting("Loading Mesh");

				fileLoadCancellationTokenSource = new CancellationTokenSource();

				// TODO: How to we handle mesh load errors? How do we report success?
				IObject3D loadedItem = await Task.Run(() => Object3D.Load(itemPath, fileLoadCancellationTokenSource.Token, progress: interactionLayer.ReportProgress0to100));
				if (loadedItem != null)
				{
					if (itemName != null)
					{
						loadedItem.Name = itemName;
					}

					// SetMeshAfterLoad
					scene.ModifyChildren(children =>
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
					// TODO: Error message container moved to Interaction Layer - how could we support this type of error for a loaded scene item?
					//partProcessingInfo.centeredInfoText.Text = string.Format("Sorry! No 3D view available\nfor this file.");
				}

				interactionLayer.EndProgressReporting();

				// Invoke LoadDone event
				LoadDone?.Invoke(this, null);
			}
			else
			{
				// TODO: Error message container moved to Interaction Layer - how could we support this type of error for a loaded scene item?
				//partProcessingInfo.centeredInfoText.Text = string.Format("{0}\n'{1}'", "File not found on disk.", Path.GetFileName(itemPath));
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


		private TrackballTumbleWidget trackballTumbleWidget;

		public bool IsActive { get; set; } = true;

		private void DrawObject(IObject3D object3D, Matrix4X4 transform, bool parentSelected)
		{
			foreach(MeshRenderData renderData in object3D.VisibleMeshes(transform))
			{
				bool isSelected = parentSelected ||
					scene.HasSelection && (object3D == scene.SelectedItem || scene.SelectedItem.Children.Contains(object3D));

				RGBA_Bytes drawColor = renderData.Color;
				if(renderData.OutputType == PrintOutputTypes.Support)
				{
					drawColor = new RGBA_Bytes(RGBA_Bytes.Yellow, 120);
				}
				else if(renderData.OutputType == PrintOutputTypes.Hole)
				{
					drawColor = new RGBA_Bytes(RGBA_Bytes.Gray, 120);
				}

				// check if we should be rendering materials
				if (this.RenderType == RenderTypes.Materials)
				{
					drawColor = MatterialRendering.Color(renderData.MaterialIndex);
				}

				GLHelper.Render(renderData.Mesh, drawColor, renderData.Matrix, RenderType);

				if(isSelected)
				{
					var screenPosition = new Vector3[3];
					var frustum = World.GetClippingFrustum();
					GLHelper.PrepareFor3DLineRender(true);

					double selectionHighlightWidth = 5;

					if (renderData.Mesh.Vertices.Count < 1000)
					{
						foreach (MeshEdge meshEdge in renderData.Mesh.MeshEdges)
						{
							if (meshEdge.GetNumFacesSharingEdge() == 2)
							{
								FaceEdge firstFaceEdge = meshEdge.firstFaceEdge;
								FaceEdge nextFaceEdge = meshEdge.firstFaceEdge.radialNextFaceEdge;
								// find out if one face is facing the camera and one is facing away
								var vertexPosition = World.GetScreenSpace(Vector3.Transform(firstFaceEdge.FirstVertex.Position, renderData.Matrix));
								var firstNormal = World.GetScreenSpace(Vector3.Transform(firstFaceEdge.FirstVertex.Position + firstFaceEdge.ContainingFace.normal, renderData.Matrix));
								var nextNormal = World.GetScreenSpace(Vector3.Transform(firstFaceEdge.FirstVertex.Position + nextFaceEdge.ContainingFace.normal, renderData.Matrix));

								var firstTowards = (firstNormal - vertexPosition).z < 0;
								var nextTowards = (nextNormal - vertexPosition).z < 0;

								if (firstTowards != nextTowards)
								{
									var transformed1 = Vector3.Transform(meshEdge.VertexOnEnd[0].Position, renderData.Matrix);
									var transformed2 = Vector3.Transform(meshEdge.VertexOnEnd[1].Position, renderData.Matrix);

									for (int i = 0; i < 3; i++)
									{
										GLHelper.Render3DLineNoPrep(frustum, World, transformed1, transformed2, RGBA_Bytes.White, selectionHighlightWidth);
									}
								}
							}
						}
					}
					else // just render the bounding box
					{
						RenderAABB(frustum, renderData.Mesh.GetAxisAlignedBoundingBox(), renderData.Matrix, RGBA_Bytes.White, selectionHighlightWidth);
					}

					// turn lighting back on after rendering selection outlines
					GL.Enable(EnableCap.Lighting);
				}
			}
		}

		void RenderAABB(Frustum frustum, AxisAlignedBoundingBox bounds, Matrix4X4 matrix, RGBA_Bytes color, double width)
		{
			for (int i = 0; i < 4; i++)
			{
				Vector3 bottomStartPosition = Vector3.Transform(bounds.GetBottomCorner(i), matrix);
				Vector3 bottomEndPosition = Vector3.Transform(bounds.GetBottomCorner((i + 1) % 4), matrix);
				Vector3 topStartPosition = Vector3.Transform(bounds.GetTopCorner(i), matrix);
				Vector3 topEndPosition = Vector3.Transform(bounds.GetTopCorner((i + 1) % 4), matrix);

				GLHelper.Render3DLineNoPrep(frustum, World, bottomStartPosition, bottomEndPosition, color, width);
				GLHelper.Render3DLineNoPrep(frustum, World, topStartPosition, topEndPosition, color, width);
				GLHelper.Render3DLineNoPrep(frustum, World, topStartPosition, bottomStartPosition, color, width);
			}
		}

		private Mesh printerShape;

		public enum EditorType { Printer, Part }

		public EditorType EditorMode { get; set; } = EditorType.Part;

		private void trackballTumbleWidget_DrawGlContent(object sender, EventArgs e)
		{
			foreach(var object3D in scene.Children)
			{
				DrawObject(object3D, Matrix4X4.Identity, false);
			}

			if (this.EditorMode == EditorType.Printer)
			{
				if (RenderBed)
				{
					GLHelper.Render(printer.Bed.Mesh, this.BedColor);
					if (printerShape != null)
					{
						GLHelper.Render(printerShape, this.BedColor);
					}
				}

				if (printer.Bed.BuildVolumeMesh != null && RenderBuildVolume)
				{
					GLHelper.Render(printer.Bed.BuildVolumeMesh, this.BuildVolumeColor);
				}
			}
			else
			{
				GL.Disable(EnableCap.Texture2D);
				GL.Disable(EnableCap.Blend);

				int width = 600;

				GL.Begin(BeginMode.Lines);
				{
					for (int i = -width; i <= width; i += 50)
					{
						GL.Color4(240, 240, 240, 255);
						GL.Vertex3(i, width, 0);
						GL.Vertex3(i, -width, 0);

						GL.Vertex3(width, i, 0);
						GL.Vertex3(-width, i, 0);

					}

					GL.Color4(255, 0, 0, 255);
					GL.Vertex3(width, 0, 0);
					GL.Vertex3(-width, 0, 0);

					GL.Color4(0, 255, 0, 255);
					GL.Vertex3(0, width, 0);
					GL.Vertex3(0, -width, 0);

					GL.Color4(0, 0, 255, 255);
					GL.Vertex3(0, 0, 10);
					GL.Vertex3(0, 0, -10);
				}
				GL.End();
			}

			// we don't want to render the bed or build volume before we load a model.
			if (scene.HasChildren || AllowBedRenderingWhenEmpty)
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
				progressControl = new ProgressControl("", RGBA_Bytes.Black, RGBA_Bytes.Black)
				{
					HAnchor = HAnchor.Center,
					Visible = false
				};
				progressControl.ProgressChanged += (sender, e) =>
				{
					progressControl.Visible = true;
				};
				AddChild(progressControl);

				centeredInfoText = new TextWidget(startingTextMessage)
				{
					HAnchor = HAnchor.Center,
					AutoExpandBoundsToText = true
				};
				AddChild(centeredInfoText);

				centeredInfoDescription = new TextWidget("")
				{
					HAnchor = HAnchor.Center,
					AutoExpandBoundsToText = true
				};
				AddChild(centeredInfoDescription);

				VAnchor |= VAnchor.Center;
				HAnchor |= HAnchor.Center;
			}
		}
	}
}
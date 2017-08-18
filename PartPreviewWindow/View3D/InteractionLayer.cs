/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

#define DO_LIGHTING

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MeshVisualizer;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using static MatterHackers.MeshVisualizer.MeshViewerWidget;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class InteractionLayer : GuiWidget, IInteractionVolumeContext
	{
		private int volumeIndexWithMouseDown = -1;

		public WorldView World { get; }

		public InteractiveScene Scene { get; }

		public event EventHandler DrawGlContent;

		public bool DoOpenGlDrawing { get; set; } = true;

		// TODO: Collapse into auto-property
		private List<InteractionVolume> interactionVolumes = new List<InteractionVolume>();
		public List<InteractionVolume> InteractionVolumes { get; }

		private UndoBuffer undoBuffer;

		private Action notifyPartChanged;

		public PartProcessingInfo partProcessingInfo;

		private string progressReportingPrimaryTask = "";

		private float[] ambientLight = { 0.2f, 0.2f, 0.2f, 1.0f };

		private float[] diffuseLight0 = { 0.7f, 0.7f, 0.7f, 1.0f };
		private float[] specularLight0 = { 0.5f, 0.5f, 0.5f, 1.0f };
		private float[] lightDirection0 = { -1, -1, 1, 0.0f };

		private float[] diffuseLight1 = { 0.5f, 0.5f, 0.5f, 1.0f };
		private float[] specularLight1 = { 0.3f, 0.3f, 0.3f, 1.0f };
		private float[] lightDirection1 = { 1, 1, 1, 0.0f };

		public InteractionLayer(WorldView world, UndoBuffer undoBuffer, Action notifyPartChanged, InteractiveScene scene)
		{
			this.Scene = scene;
			this.World = world;
			this.InteractionVolumes = interactionVolumes;
			this.undoBuffer = undoBuffer;
			this.notifyPartChanged = notifyPartChanged;

			var labelContainer = new GuiWidget();
			labelContainer.Selectable = false;
			labelContainer.AnchorAll();
			this.AddChild(labelContainer);

			partProcessingInfo = new PartProcessingInfo("");
			labelContainer.AddChild(partProcessingInfo);
		}

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
				partProcessingInfo.centeredInfoText.Text = $"{progressReportingPrimaryTask} {percentComplete}%...";
				partProcessingInfo.progressControl.PercentComplete = percentComplete;

				// Only assign to textbox if value passed through
				if (processingState != null)
				{
					partProcessingInfo.centeredInfoDescription.Text = processingState;
				}
			});
		}

		internal void SetRenderTarget(GuiWidget renderSource)
		{
			renderSource.AfterDraw += RenderSource_DrawExtra;
		}

		private void RenderSource_DrawExtra(object sender, DrawEventArgs e)
		{
			if (DoOpenGlDrawing)
			{
				SetGlContext();
				OnDrawGlContent();
				UnsetGlContext();
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			int volumeHitIndex;
			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			IntersectInfo info;
			if (this.Scene.HasSelection
				&& !SuppressUiVolumes
				&& FindInteractionVolumeHit(ray, out volumeHitIndex, out info))
			{
				MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);
				volumeIndexWithMouseDown = volumeHitIndex;
				interactionVolumes[volumeHitIndex].OnMouseDown(mouseEvent3D);
				SelectedInteractionVolume = interactionVolumes[volumeHitIndex];
			}
			else
			{
				SelectedInteractionVolume = null;
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			if (SuppressUiVolumes)
			{
				return;
			}

			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			IntersectInfo info = null;
			if (MouseDownOnInteractionVolume && volumeIndexWithMouseDown != -1)
			{
				MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);
				interactionVolumes[volumeIndexWithMouseDown].OnMouseMove(mouseEvent3D);
			}
			else
			{
				MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);

				int volumeHitIndex;
				FindInteractionVolumeHit(ray, out volumeHitIndex, out info);

				for (int i = 0; i < interactionVolumes.Count; i++)
				{
					if (i == volumeHitIndex)
					{
						interactionVolumes[i].MouseOver = true;
						interactionVolumes[i].MouseMoveInfo = info;

						HoveredInteractionVolume = interactionVolumes[i];
					}
					else
					{
						interactionVolumes[i].MouseOver = false;
						interactionVolumes[i].MouseMoveInfo = null;
					}

					interactionVolumes[i].OnMouseMove(mouseEvent3D);
				}
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			Invalidate();

			if (SuppressUiVolumes)
			{
				return;
			}

			int volumeHitIndex;
			Ray ray = this.World.GetRayForLocalBounds(mouseEvent.Position);
			IntersectInfo info;
			bool anyInteractionVolumeHit = FindInteractionVolumeHit(ray, out volumeHitIndex, out info);
			MouseEvent3DArgs mouseEvent3D = new MouseEvent3DArgs(mouseEvent, ray, info);

			if (MouseDownOnInteractionVolume && volumeIndexWithMouseDown != -1)
			{
				interactionVolumes[volumeIndexWithMouseDown].OnMouseUp(mouseEvent3D);
				SelectedInteractionVolume = null;

				volumeIndexWithMouseDown = -1;
			}
			else
			{
				volumeIndexWithMouseDown = -1;

				if (anyInteractionVolumeHit)
				{
					interactionVolumes[volumeHitIndex].OnMouseUp(mouseEvent3D);
				}
				SelectedInteractionVolume = null;
			}

			base.OnMouseUp(mouseEvent);
		}

		private bool FindInteractionVolumeHit(Ray ray, out int interactionVolumeHitIndex, out IntersectInfo info)
		{
			interactionVolumeHitIndex = -1;
			if (interactionVolumes.Count == 0 || interactionVolumes[0].CollisionVolume == null)
			{
				info = null;
				return false;
			}

			// TODO: Rewrite as projection without extra list
			List<IPrimitive> uiTraceables = new List<IPrimitive>();
			foreach (InteractionVolume interactionVolume in interactionVolumes)
			{
				if (interactionVolume.CollisionVolume != null)
				{
					IPrimitive traceData = interactionVolume.CollisionVolume;
					uiTraceables.Add(new Transform(traceData, interactionVolume.TotalTransform));
				}
			}

			IPrimitive allUiObjects = BoundingVolumeHierarchy.CreateNewHierachy(uiTraceables);

			info = allUiObjects.GetClosestIntersection(ray);
			if (info != null)
			{
				for (int i = 0; i < interactionVolumes.Count; i++)
				{
					List<IBvhItem> insideBounds = new List<IBvhItem>();
					if (interactionVolumes[i].CollisionVolume != null)
					{
						interactionVolumes[i].CollisionVolume.GetContained(insideBounds, info.closestHitObject.GetAxisAlignedBoundingBox());
						if (insideBounds.Contains(info.closestHitObject))
						{
							interactionVolumeHitIndex = i;
							return true;
						}
					}
				}
			}

			return false;
		}

		public void AddTransformSnapshot(Matrix4X4 originalTransform)
		{
			if (this.Scene.HasSelection && this.Scene.SelectedItem.Matrix != originalTransform)
			{
				this.undoBuffer.Add(new TransformUndoCommand(Scene.SelectedItem, originalTransform, Scene.SelectedItem.Matrix));
				this.notifyPartChanged?.Invoke();
			}
		}

		public bool SuppressUiVolumes { get; set; } = false;

		public bool MouseDownOnInteractionVolume => SelectedInteractionVolume != null;

		public InteractionVolume SelectedInteractionVolume { get; set; } = null;
		public InteractionVolume HoveredInteractionVolume { get; set; } = null;

		public double SnapGridDistance { get; set; } = 1;

		public GuiWidget GuiSurface => this;

		private void OnDrawGlContent()
		{
			DrawGlContent?.Invoke(this, null);
		}

		private void SetGlContext()
		{
			GL.ClearDepth(1.0);
			GL.Clear(ClearBufferMask.DepthBufferBit);   // Clear the Depth Buffer

			GL.PushAttrib(AttribMask.ViewportBit);
			RectangleDouble screenRect = this.TransformToScreenSpace(LocalBounds);
			GL.Viewport((int)screenRect.Left, (int)screenRect.Bottom, (int)screenRect.Width, (int)screenRect.Height);

			GL.ShadeModel(ShadingModel.Smooth);

			GL.FrontFace(FrontFaceDirection.Ccw);
			GL.CullFace(CullFaceMode.Back);

			GL.DepthFunc(DepthFunction.Lequal);

			GL.Disable(EnableCap.DepthTest);
			//ClearToGradient();

#if DO_LIGHTING
			GL.Light(LightName.Light0, LightParameter.Ambient, ambientLight);

			GL.Light(LightName.Light0, LightParameter.Diffuse, diffuseLight0);
			GL.Light(LightName.Light0, LightParameter.Specular, specularLight0);

			GL.Light(LightName.Light0, LightParameter.Ambient, new float[] { 0, 0, 0, 0 });
			GL.Light(LightName.Light1, LightParameter.Diffuse, diffuseLight1);
			GL.Light(LightName.Light1, LightParameter.Specular, specularLight1);

			GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);

			GL.Enable(EnableCap.Light0);
			GL.Enable(EnableCap.Light1);
			GL.Enable(EnableCap.DepthTest);
			GL.Enable(EnableCap.Blend);
			GL.Enable(EnableCap.Normalize);
			GL.Enable(EnableCap.Lighting);
			GL.Enable(EnableCap.ColorMaterial);

			Vector3 lightDirectionVector = new Vector3(lightDirection0[0], lightDirection0[1], lightDirection0[2]);
			lightDirectionVector.Normalize();
			lightDirection0[0] = (float)lightDirectionVector.x;
			lightDirection0[1] = (float)lightDirectionVector.y;
			lightDirection0[2] = (float)lightDirectionVector.z;
			GL.Light(LightName.Light0, LightParameter.Position, lightDirection0);
			GL.Light(LightName.Light1, LightParameter.Position, lightDirection1);
#endif

			// set the projection matrix
			GL.MatrixMode(MatrixMode.Projection);
			GL.PushMatrix();
			GL.LoadMatrix(this.World.ProjectionMatrix.GetAsDoubleArray());

			// set the modelview matrix
			GL.MatrixMode(MatrixMode.Modelview);
			GL.PushMatrix();
			GL.LoadMatrix(this.World.ModelviewMatrix.GetAsDoubleArray());
		}

		private void UnsetGlContext()
		{
			GL.MatrixMode(MatrixMode.Projection);
			GL.PopMatrix();

			GL.MatrixMode(MatrixMode.Modelview);
			GL.PopMatrix();

#if DO_LIGHTING
			GL.Disable(EnableCap.ColorMaterial);
			GL.Disable(EnableCap.Lighting);
			GL.Disable(EnableCap.Light0);
			GL.Disable(EnableCap.Light1);
#endif
			GL.Disable(EnableCap.Normalize);
			GL.Disable(EnableCap.Blend);
			GL.Disable(EnableCap.DepthTest);

			GL.PopAttrib();
		}
	}
}
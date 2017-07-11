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
using System;
using System.Collections.Generic;
using MatterHackers.Agg.UI;
using MatterHackers.MeshVisualizer;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class InteractionLayer : GuiWidget, IInteractionVolumeContext
	{
		private int volumeIndexWithMouseDown = -1;

		public WorldView World { get; }

		public InteractiveScene Scene { get; set; }

		// TODO: Collapse into auto-property
		private List<InteractionVolume> interactionVolumes = new List<InteractionVolume>();
		public List<InteractionVolume> InteractionVolumes { get; }

		public InteractionLayer(WorldView world)
		{
			this.World = world;
			this.InteractionVolumes = interactionVolumes;
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

		public bool SuppressUiVolumes { get; set; } = false;

		public bool MouseDownOnInteractionVolume => SelectedInteractionVolume != null;

		public InteractionVolume SelectedInteractionVolume { get; set; } = null;
		public InteractionVolume HoveredInteractionVolume { get; set; } = null;

		public double SnapGridDistance { get; set; }

		public GuiWidget GuiSurface => this;
	}
}
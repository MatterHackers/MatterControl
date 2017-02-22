﻿/*
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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System;
using System.IO;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public abstract class PartPreview3DWidget : PartPreviewWidget
	{
		protected static readonly int DefaultScrollBarWidth = 120;
		
		protected bool autoRotating = false;
		protected bool allowAutoRotate = false;

		public MeshViewerWidget meshViewerWidget;

		private EventHandler unregisterEvents;

		// Proxy to MeshViewerWidget
		public InteractiveScene Scene => meshViewerWidget.Scene;

		protected ViewControls3D viewControls3D;

		private bool needToRecretaeBed = false;

		public PartPreview3DWidget()
		{
			ActiveSliceSettings.SettingChanged.RegisterEvent(CheckSettingChanged, ref unregisterEvents);
			ApplicationController.Instance.AdvancedControlsPanelReloading.RegisterEvent(CheckSettingChanged, ref unregisterEvents);
		}

		public MeshSelectInfo CurrentSelectInfo { get; private set; } = new MeshSelectInfo();

		protected IObject3D FindHitObject3D(Vector2 screenPosition, ref IntersectInfo intersectionInfo)
		{
			Vector2 meshViewerWidgetScreenPosition = meshViewerWidget.TransformFromParentSpace(this, screenPosition);
			Ray ray = meshViewerWidget.TrackballTumbleWidget.GetRayForLocalBounds(meshViewerWidgetScreenPosition);

			intersectionInfo = Scene.TraceData().GetClosestIntersection(ray);
			if (intersectionInfo != null)
			{
				foreach (Object3D object3D in Scene.Children)
				{
					if (object3D.TraceData().Contains(intersectionInfo.closestHitObject))
					{
						CurrentSelectInfo.PlaneDownHitPos = intersectionInfo.hitPosition;
						CurrentSelectInfo.LastMoveDelta = new Vector3();
						return object3D;
					}
				}
			}

			return null;
		}

		public GuiWidget GenerateHorizontalRule()
		{
			return new GuiWidget()
			{
				Height = 1,
				Margin = new BorderDouble(0, 1, 0, 3),
				HAnchor = HAnchor.ParentLeftRight,
				BackgroundColor = new RGBA_Bytes(255, 255, 255, 200)
			};
		}

		private void CheckSettingChanged(object sender, EventArgs e)
		{
			StringEventArgs stringEvent = e as StringEventArgs;
			if (stringEvent != null)
			{
				if (stringEvent.Data == SettingsKey.bed_size
					|| stringEvent.Data == SettingsKey.print_center
					|| stringEvent.Data == SettingsKey.build_height
					|| stringEvent.Data == SettingsKey.bed_shape
					|| stringEvent.Data == SettingsKey.center_part_on_bed)
				{
					needToRecretaeBed = true;
				}
			}
		}

		public virtual bool InEditMode { get { return false; } }

		private void RecreateBed()
		{
			double buildHeight = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.build_height);

			UiThread.RunOnIdle((Action)(() =>
			{
				meshViewerWidget.CreatePrintBed(
					new Vector3(ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size), buildHeight),
					ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center),
					ActiveSliceSettings.Instance.GetValue<BedShape>(SettingsKey.bed_shape));
				PutOemImageOnBed();

				Vector2 bedCenter = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center);
				if(ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.center_part_on_bed)
					&& !InEditMode)
				{
				#if false
					if (meshViewerWidget.MeshGroups.Count > 0)
					{
						var bounds = meshViewerWidget.MeshGroups[0].GetAxisAlignedBoundingBox();
						Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
						for (int i = 0; i < meshViewerWidget.MeshGroups.Count; i++)
						{
							meshViewerWidget.MeshGroupTransforms[i] = Matrix4X4.CreateTranslation(-boundsCenter + new Vector3(0, 0, bounds.ZSize / 2) + new Vector3(bedCenter));
						}
					}
				#endif
				}
			}));
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (needToRecretaeBed)
			{
				needToRecretaeBed = false;
				RecreateBed();
			}
			base.OnDraw(graphics2D);
		}

		static ImageBuffer wattermarkImage = null;
		protected void PutOemImageOnBed()
		{
			// this is to add an image to the bed
			string imagePathAndFile = Path.Combine("OEMSettings", "bedimage.png");
			if (allowAutoRotate && StaticData.Instance.FileExists(imagePathAndFile))
			{
				if (wattermarkImage == null)
				{
					wattermarkImage = StaticData.Instance.LoadImage(imagePathAndFile);
				}

				ImageBuffer bedImage = MeshViewerWidget.BedImage;
				Graphics2D bedGraphics = bedImage.NewGraphics2D();
				bedGraphics.Render(wattermarkImage, new Vector2((bedImage.Width - wattermarkImage.Width) / 2, (bedImage.Height - wattermarkImage.Height) / 2));
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		public static SolidSlider InsertUiForSlider(GuiWidget wordOptionContainer, string header, double min = 0, double max = .5)
		{
			double scrollBarWidth = 10;
			if (UserSettings.Instance.IsTouchScreen)
			{
				scrollBarWidth = 20;
			}

			TextWidget spacingText = new TextWidget(header, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(10, 3, 3, 5),
				HAnchor = HAnchor.ParentLeft
			};
			wordOptionContainer.AddChild(spacingText);

			SolidSlider namedSlider = new SolidSlider(new Vector2(), scrollBarWidth, 0, 1)
			{
				TotalWidthInPixels = DefaultScrollBarWidth,
				Minimum = min,
				Maximum = max,
				Margin = new BorderDouble(3, 5, 3, 3),
				HAnchor = HAnchor.ParentCenter,
			};
			namedSlider.View.BackgroundColor = new RGBA_Bytes();

			wordOptionContainer.AddChild(namedSlider);

			return namedSlider;
		}
	}
}
using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class HardwareSettingsWidget : SettingsViewBase
	{
		public HardwareSettingsWidget(TextImageButtonFactory buttonFactory)
			: base("Hardware".Localize(), buttonFactory)
		{
			bool hasCamera = true || ApplicationSettings.Instance.get(ApplicationSettingsKey.HardwareHasCamera) == "true";

			var settingsRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(0, 4)
			};

			ImageBuffer cameraIconImage = StaticData.Instance.LoadIcon("camera-24x24.png", 24, 24).InvertLightness();
			cameraIconImage.SetRecieveBlender(new BlenderPreMultBGRA());

			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				cameraIconImage.InvertLightness();
			}

			var openCameraButton = buttonFactory.Generate("Preview".Localize().ToUpper());
			openCameraButton.Click += (s, e) =>
			{
				MatterControlApplication.Instance.OpenCameraPreview();
			};
			openCameraButton.Margin = new BorderDouble(left: 6);

			settingsRow.AddChild(new ImageWidget(cameraIconImage)
			{
				Margin = new BorderDouble(right: 6)
			});
			settingsRow.AddChild(new TextWidget("Camera Monitoring".Localize())
			{
				AutoExpandBoundsToText = true,
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				VAnchor = VAnchor.ParentCenter
			});
			settingsRow.AddChild(new HorizontalSpacer());
			settingsRow.AddChild(openCameraButton);

			if (hasCamera)
			{
				var publishImageSwitchContainer = new FlowLayoutWidget()
				{
					VAnchor = VAnchor.ParentCenter,
					Margin = new BorderDouble(left: 16)
				};

				CheckBox toggleSwitch = ImageButtonFactory.CreateToggleSwitch(ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.publish_bed_image));
				toggleSwitch.CheckedStateChanged += (sender, e) =>
				{
					ActiveSliceSettings.Instance.SetValue(SettingsKey.publish_bed_image, toggleSwitch.Checked ? "1" : "0");
				};
				publishImageSwitchContainer.AddChild(toggleSwitch);

				publishImageSwitchContainer.SetBoundsToEncloseChildren();

				settingsRow.AddChild(publishImageSwitchContainer);
			}

			if (hasCamera)
			{
				mainContainer.AddChild(new HorizontalLine(50));

				var cameraContainer = new DisableableWidget();
				cameraContainer.AddChild(settingsRow);
				mainContainer.AddChild(cameraContainer);
			}

			AddChild(mainContainer);
		}
	}
}
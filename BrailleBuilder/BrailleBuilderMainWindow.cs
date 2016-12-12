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

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Plugins.BrailleBuilder
{
	public class BrailleBuilderMainWindow : SystemWindow
	{
		private View3DBrailleBuilder part3DView;

		public BrailleBuilderMainWindow()
			: base(690, 340)
		{
			Title = "MatterControl: Braille Builder";

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			double buildHeight = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.build_height);

			part3DView = new View3DBrailleBuilder(
				new Vector3(ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size), buildHeight),
				ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center),
				ActiveSliceSettings.Instance.GetValue<BedShape>(SettingsKey.bed_shape));

			this.AddChild(part3DView);

			this.AnchorAll();

			part3DView.Closed += (sender, e) =>
			{
				Close();
			};

			Width = 640;
			Height = 480;

			ShowAsSystemWindow();
			MinimumSize = new Vector2(400, 300);
		}
	}
}
/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GenerateSupportPanel : FlowLayoutWidget
	{
		private SupportGenerator supportGenerator;
		private InteractiveScene scene;
		private ThemeConfig theme;

		public GenerateSupportPanel(ThemeConfig theme, InteractiveScene scene, double minimumSupportHeight)
			: base(FlowDirection.TopToBottom)
		{
			supportGenerator = new SupportGenerator(scene, minimumSupportHeight);

			this.scene = scene;
			this.theme = theme;

			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Absolute;
			this.Width = 300;
			this.BackgroundColor = theme.BackgroundColor;
			this.Padding = theme.DefaultContainerPadding;

			// Add an editor field for the SupportGenerator.SupportType
			PropertyInfo propertyInfo = typeof(SupportGenerator).GetProperty(nameof(SupportGenerator.SupportType));

			var editor = PublicPropertyEditor.CreatePropertyEditor(
				new EditableProperty(propertyInfo, supportGenerator),
				null,
				new PPEContext(),
				theme);

			if (editor != null)
			{
				this.AddChild(editor);
			}

			// put in support pillar size
			var pillarSizeField = new DoubleField(theme);
			pillarSizeField.Initialize(0);
			pillarSizeField.DoubleValue = supportGenerator.PillarSize;
			pillarSizeField.ValueChanged += (s, e) =>
			{
				supportGenerator.PillarSize = pillarSizeField.DoubleValue;
				// in case it was corrected set it back again
				if (pillarSizeField.DoubleValue != supportGenerator.PillarSize)
				{
					pillarSizeField.DoubleValue = supportGenerator.PillarSize;
				}
			};

			// pillar rows
			this.AddChild(
				new SettingsRow(
					"Pillar Size".Localize(),
					"The width and depth of the support pillars".Localize(),
					pillarSizeField.Content,
					theme));

			// put in the angle setting
			var overHangField = new DoubleField(theme);
			overHangField.Initialize(0);
			overHangField.DoubleValue = supportGenerator.MaxOverHangAngle;
			overHangField.ValueChanged += (s, e) =>
			{
				supportGenerator.MaxOverHangAngle = overHangField.DoubleValue;
				// in case it was corrected set it back again
				if (overHangField.DoubleValue != supportGenerator.MaxOverHangAngle)
				{
					overHangField.DoubleValue = supportGenerator.MaxOverHangAngle;
				}
			};

			// overhang row
			this.AddChild(
				new SettingsRow(
					"Overhang Angle".Localize(),
					"The angle to generate support for".Localize(),
					overHangField.Content,
					theme));

			// Button Row
			var buttonRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = new BorderDouble(top: 5)
			};
			this.AddChild(buttonRow);

			buttonRow.AddChild(new HorizontalSpacer());

			// add 'Remove Auto Supports' button
			var removeButton = theme.CreateDialogButton("Remove".Localize());
			removeButton.ToolTipText = "Remove all auto generated supports".Localize();
			removeButton.Click += (s, e) => supportGenerator.RemoveExisting();
			buttonRow.AddChild(removeButton);

			// add 'Generate Supports' button
			var generateButton = theme.CreateDialogButton("Generate".Localize());
			generateButton.ToolTipText = "Find and create supports where needed".Localize();
			generateButton.Click += (s, e) => Rebuild();
			buttonRow.AddChild(generateButton);
			theme.ApplyPrimaryActionStyle(generateButton);
		}

		private Task Rebuild()
		{
			return ApplicationController.Instance.Tasks.Execute(
				"Create Support".Localize(),
				null,
				supportGenerator.Create);
		}
	}
}
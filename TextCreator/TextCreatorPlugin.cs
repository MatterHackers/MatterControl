/*
Copyright (c) 2017, John Lewin
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

using System.IO;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.Plugins.BrailleBuilder;
using MatterHackers.MatterControl.Plugins.TextCreator;
using MatterHackers.MatterControl.PluginSystem;

namespace MatterHackers.Plugins.EditorTools
{
	public class TextCreatorPlugin : MatterControlPlugin
	{
		public override void Initialize(GuiWidget application)
		{
			string category = "Text Tools".Localize();
			var library = ApplicationController.Instance.Library;

			library.RegisterCreator(
				new GeneratorItem(
					"Text".Localize(), 
					() =>
					{
						var generator = new TextGenerator();
						return generator.CreateText(
							"Text".Localize(),
							1,
							.25,
							1,
							true);
					},
					category));

			library.RegisterCreator(
				new GeneratorItem(
					"Braille".Localize(), 
					() =>
					{
						string braille = "Braille".Localize();
						var generator = new BrailleGenerator();
						return generator.CreateText(
							braille,
							1,
							.25,
							braille);
					}, 
					category));

			// TODO: Filepath won't work on Android. Needs to load from/to stream via custom type
			library.RegisterCreator(
				new FileSystemFileItem(AggContext.StaticData.MapPath(Path.Combine("Images", "mh-logo.png")))
				{
					Name = "Image Converter".Localize(),
					Category = category
				});

			base.Initialize(application);
		}
	}
}
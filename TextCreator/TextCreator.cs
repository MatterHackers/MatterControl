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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.MatterControl.PluginSystem;
using System;

namespace MatterHackers.MatterControl.Plugins.TextCreator
{
	public class TextCreatorPlugin : MatterControlPlugin
	{
		public override void Initialize(GuiWidget application)
		{
			var information = new CreatorInformation(
				() => new TextCreatorMainWindow(), 
				"TC_32x32.png", 
				"Text Creator".Localize());

			RegisteredCreators.Instance.RegisterLaunchFunction(information);
		}

		public override string GetPluginInfoJSon()
		{
			return "{" +
				"\"Name\": \"Text Creator\"," +
				"\"UUID\": \"fbd06000-66c3-11e3-949a-0800200c9a66\"," +
				"\"About\": \"A Creator that allows you to type in text and have it turned into printable extrusions.\"," +
				"\"Developer\": \"MatterHackers, Inc.\"," +
				"\"URL\": \"https://www.matterhackers.com\"" +
				"}";
		}
	}
}
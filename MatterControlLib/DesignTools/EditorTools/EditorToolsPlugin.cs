/*
Copyright (c) 2019, John Lewin
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

using System.Linq;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.Extensibility;
using MatterHackers.MatterControl.Library;

namespace MatterHackers.Plugins.EditorTools
{
	public class EditorToolsPlugin : IApplicationPlugin
	{
		public void Initialize()
		{
			var applicationController = ApplicationController.Instance;

			var primitives = new PrimitivesContainer();
			primitives.Load();

			foreach (var item in primitives.Items.OfType<ILibraryObject3D>())
			{
				applicationController.Library.RegisterCreator(item);
			}

			applicationController.Extensions.Register(new ScaleCornersPlugin());
			applicationController.Extensions.Register(new RotateCornerPlugins());

			applicationController.Extensions.Register(new OpenSCADBuilder());
			//applicationController.Extensions.Register(new PrimitivesEditor());
		}

		public PluginInfo MetaData { get; } = new PluginInfo()
		{
			Name = "Editor Tools",
			UUID = "1A3C7BE4-EEC2-43BA-A7B0-035C3DB51875",
			About = "Editor Tools",
			Developer = "MatterHackers, Inc.",
			Url = "https://www.matterhackers.com"
		};
	}
}
/*
Copyright (c) 2018, John Lewin
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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Extensibility;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Plugins.Lithophane
{
	public class LithophanePlugin : IApplicationPlugin
	{
		public static void WrapWith(IObject3D originalItem, IObject3D wrapper, InteractiveScene scene)
		{
			using (originalItem.RebuildLock())
			{
				originalItem.Parent.Children.Modify(list =>
				{
					list.Remove(originalItem);

					wrapper.Matrix = originalItem.Matrix;

					originalItem.Matrix = Matrix4X4.Identity;
					wrapper.Children.Add(originalItem);

					list.Add(wrapper);
				});

				if (scene != null)
				{
					var topParent = wrapper.Ancestors().LastOrDefault(i => i.Parent != null);
					UiThread.RunOnIdle(() =>
					{
						scene.SelectedItem = topParent != null ? topParent : wrapper;
					});
				}
			}
		}

		public void Initialize()
		{
			ApplicationController.Instance.Graph.RegisterOperation(
				new Library.NodeOperation()
				{
					OperationID = "Lithophane".Localize(),
					Title = "Lithophane".Localize(),
					MappedTypes = new List<Type> { typeof(ImageObject3D) },
					ResultType = typeof(LithophaneObject3D),
					Operation = (sceneItem, scene) =>
					{
						if (sceneItem is IObject3D imageObject)
						{
							WrapWith(sceneItem, new LithophaneObject3D(), scene);
						}

						return Task.CompletedTask;
					},
					IsEnabled = (sceneItem) => true,
					IsVisible = (sceneItem) => true,
					IconCollector = (invertIcon) => AggContext.StaticData.LoadIcon("lithophane.png", 16, 16, invertIcon)
				});
		}

		public PluginInfo MetaData { get; } = new PluginInfo()
		{
			Name = "Lithophane Creator",
			UUID = "B07B4EB0-CAFD-4721-A04A-FD9C3E001D2B",
			About = "A Lithophane Creator.",
			Developer = "MatterHackers, Inc.",
			Url = "https://www.matterhackers.com"
		};
	}
}

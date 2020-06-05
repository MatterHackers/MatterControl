/*
Copyright (c) 2018, John Lewin
All rights reserved.
*/

using System;
using System.Threading.Tasks;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.Extensibility;
using MatterHackers.MatterControl.Library;

namespace MatterHackers.MatterControl.Plugins.VelocityPainting
{
	public class VelocityPaintingPlugin : IApplicationPlugin
	{
		public void Initialize()
		{
			ApplicationController.Instance.Library.RegisterCreator(
				new GeneratorItem(
					() => "Velocity Paint".Localize(),
					async () => await VelocityPaintObject3D.Create())
				{
					DateCreated = new DateTime(30)
				});
		}

		public PluginInfo MetaData { get; } = new PluginInfo()
		{
			Name = "Velocity Painting",
			UUID = "A7AE966F-DB49-436D-BE95-F675535177A3",
			Developer = "MatterHackers, Inc.",
			Url = "https://www.matterhackers.com"
		};
	}
}
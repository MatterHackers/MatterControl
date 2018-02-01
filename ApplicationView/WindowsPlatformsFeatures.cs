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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	using Agg.Image;
	using MatterHackers.Agg.Platform;
	using MatterHackers.MatterControl.PluginSystem;
	using MatterHackers.RenderOpenGl.OpenGl;

	public class WindowsPlatformsFeatures : INativePlatformFeatures
	{
		public bool CameraInUseByExternalProcess { get; set; } = false;

		public event EventHandler PictureTaken;

		public void TakePhoto(string imageFileName)
		{
			ImageBuffer noCameraImage = new ImageBuffer(640, 480);
			Graphics2D graphics = noCameraImage.NewGraphics2D();
			graphics.Clear(Color.White);
			graphics.DrawString("No Camera Detected", 320, 240, pointSize: 24, justification: Agg.Font.Justification.Center);
			graphics.DrawString(DateTime.Now.ToString(), 320, 200, pointSize: 12, justification: Agg.Font.Justification.Center);
			AggContext.ImageIO.SaveImageData(imageFileName, noCameraImage);

			PictureTaken?.Invoke(null, null);
		}

		public void OpenCameraPreview()
		{
			//Camera launcher placeholder (KP)
			if (ApplicationSettings.Instance.get(ApplicationSettingsKey.HardwareHasCamera) == "true")
			{
				//Do something
			}
			else
			{
				//Do something else (like show warning message)
			}
		}

		public void PlaySound(string fileName)
		{
			if (AggContext.OperatingSystem == OSType.Windows)
			{
				using (var mediaStream = AggContext.StaticData.OpenSteam(Path.Combine("Sounds", fileName)))
				{
					(new System.Media.SoundPlayer(mediaStream)).Play();
				}
			}
		}

		public bool IsNetworkConnected()
		{
			return true;
		}

		public void ConfigureWifi()
		{
		}

		public void FindAndInstantiatePlugins(SystemWindow systemWindow)
		{
			string oemName = ApplicationSettings.Instance.GetOEMName();
			foreach (MatterControlPlugin plugin in PluginFinder.CreateInstancesOf<MatterControlPlugin>())
			{
				string pluginInfo = plugin.GetPluginInfoJSon();
				Dictionary<string, string> nameValuePairs = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(pluginInfo);

				if (nameValuePairs != null && nameValuePairs.ContainsKey("OEM"))
				{
					if (nameValuePairs["OEM"] == oemName)
					{
						plugin.Initialize(systemWindow);
					}
				}
				else
				{
					plugin.Initialize(systemWindow);
				}
			}
		}

		public void ProcessCommandline()
		{
			var commandLineArgs = Environment.GetCommandLineArgs();
			
			for (int currentCommandIndex = 0; currentCommandIndex < commandLineArgs.Length; currentCommandIndex++)
			{
				string command = commandLineArgs[currentCommandIndex];
				string commandUpper = command.ToUpper();
				switch (commandUpper)
				{
					case "FORCE_SOFTWARE_RENDERING":
						GL.HardwareAvailable = false;
						break;

					case "CLEAR_CACHE":
						AboutWidget.DeleteCacheData(0);
						break;

					case "SHOW_MEMORY":
						RootSystemWindow.ShowMemoryUsed = true;
						break;
				}
			}
		}

		public void PlatformInit(Action<string> reporter)
		{
			if (AggContext.OperatingSystem == OSType.Mac && AggContext.StaticData == null)
			{
				// Set working directory - this duplicates functionality in Main but is necessary on OSX as Main fires much later (after the constructor in this case)
				// resulting in invalid paths due to path tests running before the working directory has been overridden. Setting the value before initializing StaticData
				// works around this architectural difference.
				Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));
			}

			// Initialize a standard file system backed StaticData provider
			if (AggContext.StaticData == null) // it may already be initialized by tests
			{
				AggContext.StaticData = new FileSystemStaticData();
			}

			ApplicationSettings.Instance.set("HardwareHasCamera", "false");
		}
	}
}
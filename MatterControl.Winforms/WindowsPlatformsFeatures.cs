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
using System.IO;
using System.Linq;
using System.Reflection;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;

namespace MatterHackers.MatterControl
{
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
				using (var mediaStream = AggContext.StaticData.OpenStream(Path.Combine("Sounds", fileName)))
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

		public void ProcessCommandline()
		{
			var commandLineArgs = Environment.GetCommandLineArgs();

#if DEBUG
			WinformsEventSink.AllowInspector = true;
#endif

			for (int currentCommandIndex = 0; currentCommandIndex < commandLineArgs.Length; currentCommandIndex++)
			{
				string command = commandLineArgs[currentCommandIndex];
				switch (command.ToUpper())
				{
					case "FORCE_SOFTWARE_RENDERING":
						RootSystemWindow.UseGl = false;
						break;

					case "CLEAR_CACHE":
						CacheDirectory.DeleteCacheData();
						break;

					case "SHOW_MEMORY":
						RootSystemWindow.ShowMemoryUsed = true;
						break;

					case "ALLOW_INSPECTOR":
						WinformsEventSink.AllowInspector = true;
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

			if (Clipboard.Instance == null)
			{
				Clipboard.SetSystemClipboard(new WindowsFormsClipboard());
			}

			WinformsSystemWindow.InspectorCreator = (inspectingWindow) =>
			{
				if (inspectingWindow == AppContext.RootSystemWindow)
				{
					// If this is MatterControlApplication, include Scene
					var partContext = ApplicationController.Instance.DragDropData;
					return new InspectForm(inspectingWindow, partContext.SceneContext?.Scene ?? null, partContext.View3DWidget);
				}
				else
				{
					// Otherwise, exclude Scene
					return new InspectForm(inspectingWindow);
				}
			};


			ApplicationSettings.Instance.set("HardwareHasCamera", "false");
		}

		public void GenerateLocalizationValidationFile()
		{
			if (AggContext.StaticData is FileSystemStaticData fileSystemStaticData)
			{
				char currentChar = 'A';

				// Note: Functionality only expected to work on Desktop/Debug builds and as such, is coupled to FileSystemStaticData
				string outputPath = fileSystemStaticData.MapPath(Path.Combine("Translations", "L10N", "Translation.txt"));
				string sourceFilePath = fileSystemStaticData.MapPath(Path.Combine("Translations", "Master.txt"));

				// Ensure the output directory exists
				Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

				using (var outstream = new StreamWriter(outputPath))
				{
					foreach (var line in File.ReadAllLines(sourceFilePath))
					{
						if (line.StartsWith("Translated:"))
						{
							var pos = line.IndexOf(':');
							var segments = new string[]
							{
							line.Substring(0, pos),
							line.Substring(pos + 1),
							};

							outstream.WriteLine("{0}:{1}", segments[0], new string(segments[1].ToCharArray().Select(c => c == ' ' ? ' ' : currentChar).ToArray()));

							if (currentChar++ == 'Z')
							{
								currentChar = 'A';
							}
						}
						else
						{
							outstream.WriteLine(line);
						}
					}
				}
			}
		}

		public void InitPluginFinder()
		{
			string searchPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

			// Load plugins from all dlls in the startup directory
			foreach (var file in Directory.GetFiles(searchPath, "*.dll"))
			{
				try
				{
					PluginFinder.LoadTypesFromAssembly(Assembly.LoadFile(file));
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine("Error loading assembly: " + ex.Message);
				}
			}
		}

		public GuiWidget GetConnectDevicePage(object printer)
		{
			return new SetupStepComPortOne(printer as PrinterConfig);
		}

		// Primarily required for Android, return true on non-Android platforms
		public bool HasPermissionToDevice(object printer) => true;
	}
}
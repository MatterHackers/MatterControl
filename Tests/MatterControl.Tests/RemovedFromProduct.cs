using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;
using MatterHackers.VectorMath;

namespace MatterControl.Tests
{
	class RemovedFromProduct
	{
		public static void WriteTestGCodeFile()
		{
			using (StreamWriter file = new StreamWriter("PerformanceTest.gcode"))
			{
				//int loops = 150000;
				int loops = 150;
				int steps = 200;
				double radius = 50;
				Vector2 center = new Vector2(150, 100);

				file.WriteLine("G28 ; home all axes");
				file.WriteLine("G90 ; use absolute coordinates");
				file.WriteLine("G21 ; set units to millimeters");
				file.WriteLine("G92 E0");
				file.WriteLine("G1 F7800");
				file.WriteLine("G1 Z" + (5).ToString());
				WriteMove(file, center);

				for (int loop = 0; loop < loops; loop++)
				{
					for (int step = 0; step < steps; step++)
					{
						Vector2 nextPosition = new Vector2(radius, 0);
						nextPosition.Rotate(MathHelper.Tau / steps * step);
						WriteMove(file, center + nextPosition);
					}
				}

				file.WriteLine("M84     ; disable motors");
			}
		}

		private static void WriteMove(StreamWriter file, Vector2 center)
		{
			file.WriteLine("G1 X" + center.x.ToString() + " Y" + center.y.ToString());
		}

		private static void HtmlWindowTest()
		{
			try
			{
				SystemWindow htmlTestWindow = new SystemWindow(640, 480);
				string htmlContent = "";
				if (true)
				{
					string releaseNotesFile = Path.Combine("C:\\Users\\lbrubaker\\Downloads", "test1.html");
					htmlContent = File.ReadAllText(releaseNotesFile);
				}
				else
				{
					WebClient webClient = new WebClient();
					htmlContent = webClient.DownloadString("http://www.matterhackers.com/s/store?q=pla");
				}

				HtmlWidget content = new HtmlWidget(htmlContent, Color.Black);
				content.AddChild(new GuiWidget()
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Stretch
				});
				content.VAnchor |= VAnchor.Top;
				content.BackgroundColor = Color.White;
				htmlTestWindow.AddChild(content);
				htmlTestWindow.BackgroundColor = Color.Cyan;
				UiThread.RunOnIdle((state) =>
				{
					htmlTestWindow.ShowAsSystemWindow();
				}, 1);
			}
			catch
			{
				int stop = 1;
			}
		}
	}
}

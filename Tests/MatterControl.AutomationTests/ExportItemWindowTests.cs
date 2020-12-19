using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.MatterControl.Library.Export;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterControl.Tests.MatterControl;
using NUnit.Framework;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.VectorMath;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("Agg.UI.Automation"), Apartment(ApartmentState.STA), RunInApplicationDomain]
	public class ExportGcodeFromExportWindow
	{
		[Test]
		public async Task ExportAsGcode()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.WaitForFirstDraw();

				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				//Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Print Queue Row Item Collection");
				testRunner.InvokeLibraryAddDialog();

				//Get parts to add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");
				testRunner.Delay()
					.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"))
					.Delay()
					.Type("{Enter}");

				//Get test results 
				testRunner.ClickByName("Row Item Batman.stl")
					.ClickByName("Print Library Overflow Menu")
					.ClickByName("Export Menu Item")
					.WaitForName("Export Item Window");

				testRunner.ClickByName("Machine File (G-Code) Button")
					.ClickByName("Export Button")
					.Delay();

				string gcodeOutputPath = MatterControlUtilities.PathToExportGcodeFolder;

				Directory.CreateDirectory(gcodeOutputPath);

				string fullPathToGcodeFile = Path.Combine(gcodeOutputPath, "Batman");
				testRunner.Type(fullPathToGcodeFile);
				testRunner.Type("{Enter}");

				testRunner.WaitFor(() => File.Exists(fullPathToGcodeFile + ".gcode"), 10);

				Assert.IsTrue(File.Exists(fullPathToGcodeFile + ".gcode"), "Exported file not found");

				return Task.FromResult(0);
			});
		}

		[Test]
		public async Task ExportStreamG92HandlingTest()
		{
			var startGCode = "G28\\nM109 S[Temperature]\\nG1 Y5 X5 Z0.8 F1800\\nG92 E0\\nG1 X100 Z0.3 E25 F900\\nG92 E0\\nG1 E-2 F2400\\nG92 E0\\nG1 E1 F900";

			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.WaitForFirstDraw();

				testRunner.CloneAndSelectPrinter("No Retraction after Purge");

				var printer = testRunner.FirstPrinter();
				printer.Settings.SetValue(SettingsKey.start_gcode, startGCode);

				//Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Print Queue Row Item Collection");
				testRunner.InvokeLibraryAddDialog();

				//Get parts to add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");
				testRunner.Delay()
					.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"))
					.Delay()
					.Type("{Enter}");

				//Get test results 
				testRunner.ClickByName("Row Item Batman.stl")
					.ClickByName("Print Library Overflow Menu")
					.ClickByName("Export Menu Item")
					.WaitForName("Export Item Window");

				testRunner.ClickByName("Machine File (G-Code) Button")
					.ClickByName("Export Button");

				string gcodeOutputPath = MatterControlUtilities.PathToExportGcodeFolder;

				Directory.CreateDirectory(gcodeOutputPath);

				string fullPathToGcodeFile = Path.Combine(gcodeOutputPath, "Batman");
				testRunner.Type(fullPathToGcodeFile);
				testRunner.Type("{Enter}");

				var filename = fullPathToGcodeFile + ".gcode";
				testRunner.WaitFor(() => File.Exists(filename), 10)
					.Delay(2);

				var inputLines = new string[]
				{
					"G28                    ; home all axes",
					"M109 S[temperature]",
					"",
					"G1 Y5 X5 Z0.8 F1800; Purge line",
					"G92 E0; Purge line",
					"G1 X100 Z0.3 E25 F900; Purge line",
					"G92 E0; Purge line",
					"G1 E-2 F2400; Purge line",
					"M75; start print timer"
				};

				var expectedLines = new string[]
				{
					"G28                    ; home all axes",
					"M280 P0 S160",
					"G4 P400",
					"M280 P0 S90",
					"M109 S205",
					"G1 X5 Y5 Z3.13 F1800",
					"G92 E0; Purge line",
					"G1 X100 Y5 Z2.28 E25 F900",
					"G92 E0; Purge line",
					"G1 E-2",
					"M75                    ; start print timer"
				};

				// validate that the gcode export stack has the right output
				var testStream = GCodeExport.GetExportStream(printer, new TestGCodeStream(printer, inputLines), true);
				ValidateStreamResponse(expectedLines, testStream);

				// validate that the actual printer output has the right lines
				var actualLines = File.ReadAllLines(filename);
				ValidateLinesStartingWithFirstExpected(expectedLines, actualLines);

				// make sure the file has the expected header

				return Task.FromResult(0);
			}, maxTimeToRun: 200);
		}

		private void ValidateLinesStartingWithFirstExpected(string[] expectedLines, string[] actualLines)
		{
			throw new System.NotImplementedException();
		}

		public static void ValidateStreamResponse(string[] expected, GCodeStream testStream, List<GCodeStream> streamList = null)
		{
			int lineIndex = 0;

			// Advance
			string actualLine = testStream.ReadLine();
			string expectedLine = expected[lineIndex++];

			while (actualLine != null)
			{
				if (actualLine.StartsWith("G92 E0"))
				{
					testStream.SetPrinterPosition(new PrinterMove(default(Vector3), 0, 300));
				}

				if (actualLine.StartsWith("G92 Z0"))
				{
					testStream.SetPrinterPosition(new PrinterMove(new Vector3(), 0, 0));
				}

				if (actualLine == "; do_resume")
				{
					PauseHandlingStream pauseStream = null;
					foreach (var stream in streamList)
					{
						if (stream as PauseHandlingStream != null)
						{
							pauseStream = (PauseHandlingStream)stream;
							pauseStream.Resume();
						}
					}
				}

				if (expectedLine != actualLine)
				{
					int a = 0;
				}

				Debug.WriteLine(actualLine);
				Assert.AreEqual(expectedLine, actualLine, "Unexpected response from testStream");

				// Advance
				actualLine = testStream.ReadLine();
				if (lineIndex < expected.Length)
				{
					expectedLine = expected[lineIndex++];
				}
			}
		}
	}

	public class TestGCodeStream : GCodeStream
	{
		private int index = 0;
		private string[] lines;

		public TestGCodeStream(PrinterConfig printer, string[] lines)
			: base(printer)
		{
			this.lines = lines;
		}

		public override void Dispose()
		{
		}

		public override string ReadLine()
		{
			return index < lines.Length ? lines[index++] : null;
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
		}

		public override GCodeStream InternalStream => null;

		public override string DebugInfo => "";
	}

}

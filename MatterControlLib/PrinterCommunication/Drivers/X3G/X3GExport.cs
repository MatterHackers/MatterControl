/*
Copyright (c) 2017, Matt Moening, John Lewin
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.Plugins.X3GDriver
{
	public class X3GExport : IExportPlugin
	{
		private PrinterConfig printer;

		public string ButtonText { get; } = "Machine File (X3G)";

		public string ExtensionFilter { get; } = "Export X3G|*.x3g";

		public string FileExtension { get; } = ".x3g";

		public ImageBuffer Icon { get; } = AggContext.StaticData.LoadIcon(Path.Combine("filetypes", "x3g.png"));

		public void Initialize(PrinterConfig printer)
		{
			this.printer = printer;
		}

		public bool Enabled
		{
			get => printer.Settings.PrinterSelected
				&& printer.Settings.GetValue<bool>("enable_sailfish_communication");
		}

		public bool ExportPossible(ILibraryAsset libraryItem) => true;

		public async Task<bool> Generate(IEnumerable<ILibraryItem> libraryItems, string outputPath, IProgress<ProgressStatus> progress, CancellationToken cancellationToken)
		{
			ILibraryAssetStream libraryContent = libraryItems.OfType<ILibraryAssetStream>().FirstOrDefault();

			if (libraryContent == null)
			{
				return false;
			}

			var result = await libraryContent.GetStream(null);
			StreamReader inputFile = new StreamReader(result.Stream);
			FileStream binaryFileStream = new FileStream(outputPath, FileMode.OpenOrCreate);
			BinaryWriter outputFile = new BinaryWriter(binaryFileStream);

			var x3gConverter = new X3GWriter(new X3GPrinterDetails(), printer);

			List<byte[]> x3gLines = new List<byte[]>();
			byte[] emptyByteArray = { 0 };
			string line;

			//Makes sure steps per mm and bed offset is set
			string splitString = "\\n";
			string connectGCodeLines = printer.Settings.GetValue(SettingsKey.connect_gcode);
			foreach (string connectLine in connectGCodeLines.Split(splitString.ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
			{
				bool sendToPrinter;
				string prepedLine = connectLine.Split(';')[0];
				if (prepedLine != String.Empty)
				{
					prepedLine += '\n';
					x3gConverter.translate(connectLine, out sendToPrinter);
					x3gConverter.GetAndClearOverflowPackets();
				}
			}//MakerBot Settings set

			line = inputFile.ReadLine();
			while (line != null)
			{
				string translationCommand = line.Split(';')[0];
				translationCommand.Trim();
				if (translationCommand != String.Empty)
				{
					translationCommand += '\n';
					//Translate Lines
					bool sendToPrinter;

					x3gLines.Add(x3gConverter.translate(translationCommand, out sendToPrinter));
					x3gLines.AddRange(x3gConverter.GetAndClearOverflowPackets());
					x3gConverter.updateCurrentPosition();

					if (sendToPrinter)//certain commands are for handling internal changes only
					{
						//Write lines to file if needed
						foreach (byte[] x3gLine in x3gLines)
						{
							if (x3gLine != emptyByteArray)
							{
								byte[] trimmedX3gLine = TrimPacketStructure(x3gLine);
								outputFile.Write(trimmedX3gLine, 0, trimmedX3gLine.Length);
							}
						}
					}

					x3gLines.Clear();
				}

				line = inputFile.ReadLine();
			}

			inputFile.Close();
			outputFile.Close();

			return true;
		}

		private static byte[] TrimPacketStructure(byte[] s3gPacket)
		{
			byte[] x3gCommand = new byte[s3gPacket.Length - 3];

			for (int i = 0; i < x3gCommand.Length; i++)
			{
				x3gCommand[i] = s3gPacket[i + 2];
			}

			return x3gCommand;
		}
	}
}

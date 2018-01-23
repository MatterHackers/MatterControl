/*
Copyright (c) 2014, Kevin Pope
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
using System.Threading;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.GCodeVisualizer;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrintQueue
{
	public class ExportToFolderProcess
	{
		private List<PrintItem> allFilesToExport;
		private List<string> savedGCodeFileNames;

		public event EventHandler<StringEventArgs> StartingNextPart;

		public event EventHandler DoneSaving;

		private int itemCountBeingWorkedOn;
		private string exportPath;

		public int ItemCountBeingWorkedOn
		{
			get
			{
				return itemCountBeingWorkedOn;
			}
		}

		public string ItemNameBeingWorkedOn
		{
			get
			{
				if (ItemCountBeingWorkedOn < allFilesToExport.Count)
				{
					return allFilesToExport[ItemCountBeingWorkedOn].Name;
				}

				return "";
			}
		}

		public int CountOfParts
		{
			get
			{
				return allFilesToExport.Count;
			}
		}

		public ExportToFolderProcess(List<PrintItem> list, string exportPath)
		{
			this.allFilesToExport = list;
			this.exportPath = exportPath;

			itemCountBeingWorkedOn = 0;
		}

		public async void Start()
		{
			if (allFilesToExport.Count > 0)
			{
				StartingNextPart?.Invoke(this, new StringEventArgs(ItemNameBeingWorkedOn));

				// TODO: Export operations need to resolve printer context interactively
				var printer = ApplicationController.Instance.ActivePrinter;

				savedGCodeFileNames = new List<string>();
				foreach (PrintItem part in allFilesToExport)
				{
					var printItemWrapper = new PrintItemWrapper(part);
					string extension = Path.GetExtension(printItemWrapper.FileLocation).ToUpper();
					if (extension != "" && ApplicationSettings.ValidFileExtensions.Contains(extension))
					{
						await ApplicationController.Instance.Tasks.Execute((reporter, cancellationToken) =>
						{
							return Slicer.SliceFile(printItemWrapper.FileLocation, printItemWrapper.GetGCodePathAndFileName(), printer, reporter, cancellationToken);
						});
					}
					else if (Path.GetExtension(printItemWrapper.FileLocation).ToUpper() == ".GCODE")
					{
						sliceItem_Done(printItemWrapper, null);
					}
				}
			}
		}

		private void sliceItem_Done(object sender, EventArgs e)
		{
			PrintItemWrapper sliceItem = (PrintItemWrapper)sender;

			sliceItem.SlicingDone -= sliceItem_Done;

			if (File.Exists(sliceItem.FileLocation))
			{
				savedGCodeFileNames.Add(sliceItem.GetGCodePathAndFileName());
			}

			itemCountBeingWorkedOn++;
			if (itemCountBeingWorkedOn < allFilesToExport.Count)
			{
				StartingNextPart?.Invoke(this, new StringEventArgs(ItemNameBeingWorkedOn));
			}
			else
			{
				//UpdatePartStatus(this, new StringEventArgs("Calculating Total filament mm..."));

				if (savedGCodeFileNames.Count > 0)
				{
					double total = 0;
					foreach (string gcodeFileName in savedGCodeFileNames)
					{
						string allContent = File.ReadAllText(gcodeFileName);
						if (allContent.Length > 0)
						{
							string searchString = "filament used =";
							int startPos = allContent.IndexOf(searchString);
							if (startPos > 0)
							{
								int endPos = Math.Min(allContent.IndexOf("\n", startPos), allContent.IndexOf("mm", startPos));
								if (endPos > 0)
								{
									string value = allContent.Substring(startPos + searchString.Length, endPos - startPos - searchString.Length);
									double amountForThisFile;
									if (double.TryParse(value, out amountForThisFile))
									{
										total += amountForThisFile;
									}
								}
							}
						}
					}

					PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();

					// now copy all the gcode to the path given
					for (int i = 0; i < savedGCodeFileNames.Count; i++)
					{
						string savedGcodeFileName = savedGCodeFileNames[i];
						string originalFileName = Path.GetFileName(allFilesToExport[i].Name);
						string outputFileName = Path.ChangeExtension(originalFileName, ".gcode");
						string outputPathAndName = Path.Combine(exportPath, outputFileName);

						if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled))
						{
							GCodeMemoryFile unleveledGCode = new GCodeMemoryFile(savedGcodeFileName,
								new Vector4(),
								new Vector4(),
								new Vector4(),
								CancellationToken.None);

							for (int j = 0; j < unleveledGCode.LineCount; j++)
							{
								PrinterMachineInstruction instruction = unleveledGCode.Instruction(j);
#if DEBUG
								if (instruction.movementType != PrinterMachineInstruction.MovementTypes.Absolute)
								{
									throw new Exception("Radial functions can only execute absolute moves.");
								}
#endif
								Vector3 currentDestination = instruction.Position;

								var printerSettings = ActiveSliceSettings.Instance;

								switch (levelingData.CurrentPrinterLevelingSystem)
								{
									case PrintLevelingData.LevelingSystem.Probe3Points:
										instruction.Line = LevelWizard3Point.ApplyLeveling(printerSettings, instruction.Line, currentDestination, instruction.movementType);
										break;

									case PrintLevelingData.LevelingSystem.Probe7PointRadial:
										instruction.Line = LevelWizard7PointRadial.ApplyLeveling(printerSettings, instruction.Line, currentDestination);
										break;

									case PrintLevelingData.LevelingSystem.Probe13PointRadial:
										instruction.Line = LevelWizard13PointRadial.ApplyLeveling(printerSettings, instruction.Line, currentDestination);
										break;

									default:
										throw new NotImplementedException();
								}
							}
							unleveledGCode.Save(outputPathAndName);
						}
						else
						{
							File.Copy(savedGcodeFileName, outputPathAndName, true);
						}
					}

					if (DoneSaving != null)
					{
						DoneSaving(this, new StringEventArgs(string.Format("{0:0.0}", total)));
					}
				}
			}
		}
	}
}
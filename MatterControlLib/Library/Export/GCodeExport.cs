/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Library.Export
{
	public class GCodeExport : IExportPlugin, IExportWithOptions
	{
		private bool ForceSpiralVase;
		protected PrinterConfig printer;

		public virtual string ButtonText => "Machine File (G-Code)".Localize();

		public virtual string FileExtension => ".gcode";

		public virtual string ExtensionFilter => "Export GCode|*.gcode";

		public virtual ImageBuffer Icon { get; } = AggContext.StaticData.LoadIcon(Path.Combine("filetypes", "gcode.png"));

		public void Initialize(PrinterConfig printer)
		{
			this.printer = printer;
		}

		public virtual bool Enabled
		{
			get => printer != null
				&& printer.Settings.PrinterSelected
				&& !printer.Settings.GetValue<bool>("enable_sailfish_communication")
				&& !ApplicationController.PrinterNeedsToRunSetup(printer);
		}

		public virtual string DisabledReason
		{
			get
			{
				if(printer == null)
				{
					return "Create a printer to export G-Code".Localize();
				}
				else if(!printer.Settings.PrinterSelected)
				{
					return "No Printer Selected".Localize();
				}
				else if(ApplicationController.PrinterNeedsToRunSetup(printer))
				{
					return "Setup Needs to be Run".Localize();
				}

				return "";
			}
		}

		public virtual bool ExportPossible(ILibraryAsset libraryItem) => true;

		public GuiWidget GetOptionsPanel()
		{
			var container = new FlowLayoutWidget()
			{
				Margin = new BorderDouble(left: 40, bottom: 10),
			};

			var theme = AppContext.Theme;

			var spiralVaseCheckbox = new CheckBox("Spiral Vase".Localize(), theme.TextColor, 10)
			{
				Checked = false,
				Cursor = Cursors.Hand,
			};
			spiralVaseCheckbox.CheckedStateChanged += (s, e) =>
			{
				this.ForceSpiralVase = spiralVaseCheckbox.Checked;
			};
			container.AddChild(spiralVaseCheckbox);

			// If print leveling is enabled then add in a check box 'Apply Leveling During Export' and default checked.
			if (printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled))
			{
				var levelingCheckbox = new CheckBox("Apply leveling to G-Code during export".Localize(), theme.TextColor, 10)
				{
					Checked = true,
					Cursor = Cursors.Hand,
					Margin = new BorderDouble(left: 10)
				};
				levelingCheckbox.CheckedStateChanged += (s, e) =>
				{
					this.ApplyLeveling = levelingCheckbox.Checked;
				};
				container.AddChild(levelingCheckbox);
			}

			return container;
		}

		public virtual async Task<List<ValidationError>> Generate(IEnumerable<ILibraryItem> libraryItems, string outputPath, IProgress<ProgressStatus> progress, CancellationToken cancellationToken)
		{
			var firstItem = libraryItems.OfType<ILibraryAsset>().FirstOrDefault();
			if (firstItem != null)
			{
				IObject3D loadedItem = null;

				var assetStream = firstItem as ILibraryAssetStream;
				if (assetStream?.ContentType == "gcode")
				{
					using (var gcodeStream = await assetStream.GetStream(progress: null))
					{
						this.ApplyStreamPipelineAndExport(
							new GCodeFileStream(new GCodeFileStreamed(gcodeStream.Stream), printer),
							outputPath);

						return null;
					}
				}
				else if (firstItem.AssetPath == printer.Bed.EditContext.SourceFilePath)
				{
					// If item is bedplate, save any pending changes before starting the print
					await ApplicationController.Instance.Tasks.Execute("Saving".Localize(), printer, printer.Bed.SaveChanges);
					loadedItem = printer.Bed.Scene;
					CenterOnBed = false;
				}
				else if (firstItem is ILibraryObject3D object3DItem)
				{
					var status = new ProgressStatus()
					{
						Status = "Saving Asset".Localize()
					};

					loadedItem = await object3DItem.CreateContent(null);
					await loadedItem.PersistAssets((percentComplete, text) =>
					{
						status.Progress0To1 = percentComplete;
						progress.Report(status);
					}, publishAssets: false);
				}
				else if (assetStream != null)
				{
					loadedItem = await assetStream.CreateContent(null);
				}

				if (loadedItem != null)
				{
					// Ensure content is on disk before slicing
					await loadedItem.PersistAssets(null);

					try
					{
						string sourceExtension = $".{firstItem.ContentType}";
						string assetPath = await AssetObject3D.AssetManager.StoreMcx(loadedItem, false);

						// TODO: Prior code bypassed GCodeOverridePath mechanisms in EditContext. Consolidating into a single pathway
						string gcodePath = printer.Bed.EditContext.GCodeFilePath(printer);

						var errors = new List<ValidationError>();

						if (ApplicationSettings.ValidFileExtensions.IndexOf(sourceExtension, StringComparison.OrdinalIgnoreCase) >= 0
							|| string.Equals(sourceExtension, ".mcx", StringComparison.OrdinalIgnoreCase))
						{
							if (CenterOnBed)
							{
								// Get Bounds
								var aabb = loadedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

								// Move to bed center
								var bedCenter = printer.Bed.BedCenter;
								loadedItem.Matrix *= Matrix4X4.CreateTranslation((double)-aabb.Center.X, (double)-aabb.Center.Y, (double)-aabb.MinXYZ.Z) * Matrix4X4.CreateTranslation(bedCenter.X, bedCenter.Y, 0);
							}

							string originalSpiralVase = printer.Settings.GetValue(SettingsKey.spiral_vase);

							// Slice
							try
							{
								if (this.ForceSpiralVase)
								{
									printer.Settings.SetValue(SettingsKey.spiral_vase, "1");
								}

								errors = printer.ValidateSettings(validatePrintBed: false);

								if(errors.Any(e => e.ErrorLevel == ValidationErrorLevel.Error))
								{
									return errors;
								}

								await ApplicationController.Instance.Tasks.Execute(
									"Slicing Item".Localize() + " " + loadedItem.Name,
									printer,
									(reporter, cancellationToken2) =>
									{
										return Slicer.SliceItem(loadedItem, gcodePath, printer, reporter, cancellationToken2);
									});
							}
							finally
							{
								if (this.ForceSpiralVase)
								{
									printer.Settings.SetValue(SettingsKey.spiral_vase, originalSpiralVase);
								}
							}
						}

						if (File.Exists(gcodePath))
						{
							ApplyStreamPipelineAndExport(gcodePath, outputPath);
							return errors;
						}
					}
					catch
					{
					}

					return new List<ValidationError>();
				}
			}

			return new List<ValidationError>
			{
				new ValidationError("ItemCannotBeExported")
				{
					Error = "Item cannot be exported".Localize(),
					Details = firstItem?.ToString() ?? ""
				}
			};
		}

		public bool ApplyLeveling { get; set; } = true;

		public bool CenterOnBed { get; set; }

		public static GCodeStream GetExportStream(PrinterConfig printer, GCodeStream gCodeBaseStream, bool applyLeveling)
		{
			var queuedCommandStream = new QueuedCommandsStream(printer, gCodeBaseStream);
			GCodeStream accumulatedStream = queuedCommandStream;

			accumulatedStream = new RelativeToAbsoluteStream(printer, accumulatedStream);

			if (printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1)
			{
				var gCodeFileStream = gCodeBaseStream as GCodeFileStream;
				accumulatedStream = new ToolChangeStream(printer, accumulatedStream, queuedCommandStream, gCodeFileStream);
				accumulatedStream = new ToolSpeedMultiplierStream(printer, accumulatedStream);
			}

			bool levelingEnabled = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled) && applyLeveling;

			accumulatedStream = new BabyStepsStream(printer, accumulatedStream);

			if (levelingEnabled
				&& printer.Settings.GetValue<bool>(SettingsKey.enable_line_splitting))
			{
				accumulatedStream = new MaxLengthStream(printer, accumulatedStream, 1);
			}
			else
			{
				accumulatedStream = new MaxLengthStream(printer, accumulatedStream, 1000);
			}

			if (levelingEnabled
				&& !LevelingValidation.NeedsToBeRun(printer))
			{
				accumulatedStream = new PrintLevelingStream(printer, accumulatedStream);
			}

			if (printer.Settings.GetValue<bool>(SettingsKey.emulate_endstops))
			{
				var softwareEndstopsExStream12 = new SoftwareEndstopsStream(printer, accumulatedStream);
				accumulatedStream = softwareEndstopsExStream12;
			}

			accumulatedStream = new RemoveNOPsStream(printer, accumulatedStream);

			accumulatedStream = new ProcessWriteRegexStream(printer, accumulatedStream, queuedCommandStream);

			return accumulatedStream;
		}

		private void ApplyStreamPipelineAndExport(GCodeFileStream gCodeFileStream, string outputPath)
		{
			try
			{
				var finalStream = GetExportStream(printer, gCodeFileStream, this.ApplyLeveling);

				// Run each line from the source gcode through the loaded pipeline and dump to the output location
				using (var file = new StreamWriter(outputPath))
				{
					string nextLine = finalStream.ReadLine();
					while (nextLine != null)
					{
						if (nextLine.Trim().Length > 0)
						{
							file.WriteLine(nextLine);
						}

						nextLine = finalStream.ReadLine();
					}
				}
			}
			catch (Exception e)
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(e.Message, "Couldn't save file".Localize());
				});
			}
		}

		private void ApplyStreamPipelineAndExport(string gcodeFilename, string outputPath)
		{
			try
			{
				var settings = this.printer.Settings;
				var maxAcceleration = settings.GetValue<double>(SettingsKey.max_acceleration);
				var maxVelocity = settings.GetValue<double>(SettingsKey.max_velocity);
				var jerkVelocity = settings.GetValue<double>(SettingsKey.jerk_velocity);
				var multiplier = settings.GetValue<double>(SettingsKey.print_time_estimate_multiplier) / 100.0;

				this.ApplyStreamPipelineAndExport(
					new GCodeFileStream(
						GCodeFile.Load(
							new StreamReader(gcodeFilename).BaseStream,
							new Vector4(maxAcceleration, maxAcceleration, maxAcceleration, maxAcceleration),
							new Vector4(maxVelocity, maxVelocity, maxVelocity, maxVelocity),
							new Vector4(jerkVelocity, jerkVelocity, jerkVelocity, jerkVelocity),
							new Vector4(multiplier, multiplier, multiplier, multiplier),
							CancellationToken.None),
						printer),
					outputPath);
			}
			catch (Exception e)
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(e.Message, "Couldn't load file".Localize());
				});
			}
		}
	}
}

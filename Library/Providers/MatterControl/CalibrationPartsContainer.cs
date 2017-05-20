/*
Copyright (c) 2017, John Lewin
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.Library
{
	public class CalibrationPartsContainer : LibraryContainer
	{
		public CalibrationPartsContainer()
		{
			this.ChildContainers = new List<ILibraryContainerLink>();
			this.Items = new List<ILibraryItem>();
			this.Name = "Calibration Parts".Localize();

			this.ReloadContainer();
		}

		private void ReloadContainer()
		{
			Task.Run(() =>
			{
				// TODO: Long term do we want to have multiple categories in the view - OEM parts and printer specific calibration parts? Easy to do if so
				/*
				IEnumerable<string> printerFiles;

				string printerCalibrationFiles = ActiveSliceSettings.Instance.GetValue("calibration_files");
				if (string.IsNullOrEmpty(printerCalibrationFiles))
				{
					return;
				}

				string[] calibrationPrintFileNames = printerCalibrationFiles.Split(';');
				if (calibrationPrintFileNames.Length < 0)
				{
					printerFiles = Enumerable.Empty<string>();
				}
				else
				{
					printerFiles = calibrationPrintFileNames;
				} */

				var oemParts = StaticData.Instance.GetFiles(Path.Combine("OEMSettings", "SampleParts"));
				Items = oemParts.Select(s =>
				{
					// TODO: Won't work on Android - make stream based
					return new FileSystemFileItem(StaticData.Instance.MapPath(s));
				}).ToList<ILibraryItem>();

				UiThread.RunOnIdle(this.OnReloaded);
			});
		}

		public override void Dispose()
		{
		}
	}
}

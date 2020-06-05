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

using System.Diagnostics;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.Plugins
{
    public class PdfDocument
    {
        public static string ConvertToPng(string sourceFilePath, int dpi = 150)
        {
#if IS_WINDOWS_FORMS
            if (AggContext.StaticData is FileSystemStaticData fileSystemStaticData)
			{
				var pdfToImageConvert = fileSystemStaticData.MapPath(Path.Combine("Drivers", "ghostscript", "gswin32c.exe"));
				if (!File.Exists(pdfToImageConvert))
				{
					return null;
				}

				if (Path.GetExtension(sourceFilePath).ToUpper() == ".PDF")
				{
					try
					{
						string tempFileName = Path.ChangeExtension(Path.GetRandomFileName(), ".png");
						string outputFilePath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, $"ImageConverter_{tempFileName}");
						string commandArgs = " -q -dQUIET -dSAFER -dBATCH -dNOPAUSE -dNOPROMPT -dMaxBitmap=500000000 -dAlignToPixels=0 " +
							$" -dGridFitTT=2 -sDEVICE=pngalpha -dTextAlphaBits=4 -dGraphicsAlphaBits=4 -r{dpi}x{dpi} -dUseCropBox  " +
							$"-sOutputFile=\"{outputFilePath}\" -f\"{sourceFilePath}\"";

						var pdfOutputProcess = new Process()
						{
							StartInfo = new ProcessStartInfo()
							{
								FileName = pdfToImageConvert,
								Arguments = commandArgs,
								CreateNoWindow = true,
								WindowStyle = ProcessWindowStyle.Hidden,
								UseShellExecute = false
							}
						};

						pdfOutputProcess.Start();
						pdfOutputProcess.WaitForExit();

						return File.Exists(outputFilePath) ? outputFilePath : null;
					}
					catch
					{
					}
				}
			}
#endif
			return null;
		}

		internal static double ScaleMmmPerPixels(int dpi)
		{
			double pixelsPerPoint = 1;
			double pointsPerInch = dpi;
			double mmPerInch = 25.4;
			double mmPerPoint = mmPerInch / pointsPerInch;

			return mmPerPoint / pixelsPerPoint;
		}
	}
}
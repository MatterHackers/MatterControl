/*
Copyright (c) 2019, John Lewin
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
using System.Linq;
using System.Text;
using MatterControl.Printing.Pipelines;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.ImageProcessing;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public static class ExtensionMethods
	{
		public static void SetNonExpandableIcon(this SectionWidget sectionWidget, ImageBuffer icon)
		{
			var imageWidget = sectionWidget.Children.First().Descendants<ImageWidget>().FirstOrDefault();
			imageWidget.Image = icon;
		}

		// Color ExtensionMethods
		public static ImageBuffer MultiplyWithPrimaryAccent(this ImageBuffer sourceImage)
		{
			return sourceImage.Multiply(ApplicationController.Instance.Theme.PrimaryAccentColor);
		}

		public static ImageBuffer SetPreMultiply(this ImageBuffer sourceImage)
		{
			sourceImage.SetRecieveBlender(new BlenderPreMultBGRA());

			return sourceImage;
		}

		public static ImageBuffer AlphaToPrimaryAccent(this ImageBuffer sourceImage)
		{
			return sourceImage.AnyAlphaToColor(ApplicationController.Instance.Theme.PrimaryAccentColor);
		}


		public static string GetDebugState(this GCodeStream sourceStream)
		{
			return GetDebugState(sourceStream, ApplicationController.Instance.ActivePrinters.First());
		}

		public static string GetDebugState(this GCodeStream sourceStream, PrinterConfig printer)
		{
			var context = printer.Connection.TotalGCodeStream;

			var sb = new StringBuilder();

			while (context is GCodeStream gCodeStream)
			{
				sb.AppendFormat("{0} {1}\r\n", gCodeStream.GetType().Name, gCodeStream.DebugInfo);
				context = gCodeStream.InternalStream;
			}

			return sb.ToString();
		}

		public static IEnumerable<GCodeStream> InternalStreams(this GCodeStream context)
		{
			while (context is GCodeStream gCodeStream)
			{
				context = gCodeStream.InternalStream;
				yield return context;
			}
		}
	}
}

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

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl.Library
{
	public static class ExtensionMethods
	{
		// Container ExtensionMethods
		public static IEnumerable<ILibraryContainer> Parents(this ILibraryContainer item)
		{
			var context = item.Parent;
			while (context != null)
			{
				yield return context;
				context = context.Parent;
			}
		}

		public static void AddItem(this ILibraryContainer container, PrintItemWrapper printeItemWrapper)
		{
			throw new NotImplementedException("container.AddItem(PrintItemWrapper)");
		}

		public static void Add(this Dictionary<string, IContentProvider> list, IEnumerable<string> extensions, IContentProvider provider)
		{
			foreach (var extension in extensions)
			{
				list.Add(extension, provider);
			}
		}

		public static ContentResult CreateContent(this ILibraryContentStream item, ReportProgressRatio reporter = null)
		{
			var contentProvider = ApplicationController.Instance.Library.GetContentProvider(item) as ISceneContentProvider;
			return contentProvider?.CreateItem(item, reporter);
		}
		
		// Color ExtensionMethods
		public static ImageBuffer MultiplyWithPrimaryAccent(this ImageBuffer sourceImage)
		{
			return sourceImage.Multiply(ActiveTheme.Instance.PrimaryAccentColor);
		}

		public static ImageBuffer AlphaToPrimaryAccent(this ImageBuffer sourceImage)
		{
			return sourceImage.AnyAlphaToColor(ActiveTheme.Instance.PrimaryAccentColor);
		}
	}
}

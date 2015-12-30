/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.HtmlParsing;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintQueue;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace MatterHackers.MatterControl
{
	public class ImageWidget_AsyncLoadOnDraw : ImageWidget
	{
		public event EventHandler LoadComplete;

		bool startedLoad = false;
		string uriToLoad;

		IRecieveBlenderByte scalingBlender = new BlenderBGRA();
		public void SetScalingBlender(IRecieveBlenderByte blender) { scalingBlender = blender; }

		public ImageWidget_AsyncLoadOnDraw(ImageBuffer image, string uriToLoad)
			: base(image)
		{
			this.uriToLoad = uriToLoad;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (!startedLoad)
			{
				try
				{
					startedLoad = true;
					WebClient client = new WebClient();
					client.DownloadDataCompleted += client_DownloadDataCompleted;
					client.DownloadDataAsync(new Uri(uriToLoad));
				}
				catch (Exception)
				{
					GuiWidget.BreakInDebugger();
				}
			}

			base.OnDraw(graphics2D);
		}

		void client_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
		{
			try // if we get a bad result we can get a target invocation exception. In that case just don't show anything
			{
				byte[] raw = e.Result;
				Stream stream = new MemoryStream(raw);
				ImageBuffer unScaledImage = new ImageBuffer(10, 10, 32, new BlenderBGRA());
				StaticData.Instance.LoadImageData(stream, unScaledImage);
				// If the source image (the one we downloaded) is more than twice as big as our dest image.
				while (unScaledImage.Width > Image.Width * 2)
				{
					// The image sampler we use is a 2x2 filter so we need to scale by a max of 1/2 if we want to get good results.
					// So we scale as many times as we need to to get the Image to be the right size.
					// If this were going to be a non-uniform scale we could do the x and y separately to get better results.
					ImageBuffer halfImage = new ImageBuffer(unScaledImage.Width / 2, unScaledImage.Height / 2, 32, scalingBlender);
					halfImage.NewGraphics2D().Render(unScaledImage, 0, 0, 0, halfImage.Width / (double)unScaledImage.Width, halfImage.Height / (double)unScaledImage.Height);
					unScaledImage = halfImage;
				}
				Image.NewGraphics2D().Render(unScaledImage, 0, 0, 0, Image.Width / (double)unScaledImage.Width, Image.Height / (double)unScaledImage.Height);
				Image.MarkImageChanged();
				Invalidate();

				if (LoadComplete != null)
				{
					LoadComplete(this, null);
				}
			}
			catch (Exception)
			{
				GuiWidget.BreakInDebugger();
			}
		}
	}
}
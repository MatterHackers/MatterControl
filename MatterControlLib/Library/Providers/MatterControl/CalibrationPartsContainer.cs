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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
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
		}

		public override void Load()
		{
			var oemParts = AggContext.StaticData.GetFiles(Path.Combine("OEMSettings", "SampleParts"));
			Items = oemParts.Select(s => new StaticDataItem(s)).ToList<ILibraryItem>();
		}

		private class StaticDataItem : ILibraryAssetStream
		{
			private string relativePath;

			public StaticDataItem()
			{
			}

			public StaticDataItem(string relativePath)
			{
				this.relativePath = relativePath;
			}

			public string FileName => Path.GetFileName(relativePath);

			public string ContentType => Path.GetExtension(relativePath).ToLower().Trim('.');

			public string AssetPath => relativePath;

			public long FileSize { get; } = -1;

			public bool LocalContentExists => true;

			public string Category { get; } = "";

			public string ID => agg_basics.GetLongHashCode(relativePath).ToString();

			public string Name => this.FileName;

			public bool IsProtected => true;

			public bool IsVisible => true;

			public DateTime DateModified { get; } = DateTime.Now;

			public DateTime DateCreated { get; } = DateTime.Now;

			public Task<StreamAndLength> GetStream(Action<double, string> progress)
			{
				return Task.FromResult(new StreamAndLength()
				{
					Stream = AggContext.StaticData.OpenStream(relativePath),
					Length = -1
				});
			}
		}
	}
}

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
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.Library
{
	public class GeneratorItem : ILibraryContentItem
	{
		public GeneratorItem(string name)
		{
			this.Name = name;
			this.IsProtected = true;
		}

		public GeneratorItem(string name, Func<IObject3D> collector, string category = null)
		{
			this.Name = name;
			this.Collector = collector;
			this.Category = category;
		}

		public string ID => $"MatterHackers/ItemGenerator/{Name}".GetHashCode().ToString();
		public string Category { get; set; }
		public string Name { get; set; }
		public string ThumbnailKey { get; } = "";

		public string ContentType { get; set; } = "stl";

		public bool IsProtected { get; set; }
		public bool IsVisible => true;

		/// <summary>
		/// The delegate responsible for producing the item
		/// </summary>
		public Func<IObject3D> Collector { get; }

		public Task<IObject3D> GetContent(ReportProgressRatio<(double ratio, string state)> reportProgress) => Task.FromResult(Collector?.Invoke());

		public void SetContent(IObject3D item)
		{
		}
	}
}
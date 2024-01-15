﻿/*
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
using System.Linq;
using System.Threading.Tasks;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg;

namespace MatterHackers.MatterControl.Library
{
    public static class ColorRange
	{
		private static int currentColorIndex = 0;

		private static Color[] colors;

		private static int totalColors = 0;

		static ColorRange()
		{
			TotalColors = 12;
		}

		public static int TotalColors
		{
			get => totalColors;
			set
			{
				if (totalColors != value)
				{
					totalColors = value;
					colors = Enumerable.Range(0, totalColors).Select(colorIndex => ColorF.FromHSL(colorIndex / (double)totalColors, 1, .5).ToColor()).ToArray();
				}
			}
		}

		public static Color NextColor()
		{
			return colors[(currentColorIndex++) % TotalColors];
		}
	}

	public class GeneratorItem : ILibraryObject3D
	{
		/// <summary>
		/// The delegate responsible for producing the item
		/// </summary>
		private Func<Task<IObject3D>> collector;

		public GeneratorItem(string name)
		{
			this.Name = name;
			this.IsProtected = true;
		}

		public GeneratorItem(string name, Func<Task<IObject3D>> collector, string category = null)
		{
			this.Name = name;
			this.collector = collector;
			this.Category = category;
			//this.Color = ColorRange.NextColor();
		}

		public string ID => $"MatterHackers/ItemGenerator/{Name}".GetLongHashCode().ToString();

		public string Category { get; set; }

		private string _name;
		public string Name
		{
			get => _name; set
			{
				if (_name != value)
				{
					_name = value;
					NameChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		public event EventHandler NameChanged;

		public string ContentType { get; set; } = "mcx";

		public bool IsProtected { get; set; }

		public bool IsVisible => true;

		public DateTime DateCreated { get; set; } = DateTime.Now;

		public DateTime DateModified => DateCreated;

		public Color Color { get; set; }

		public long FileSize => 0;

		public string FileName => $"{this.Name}.{this.ContentType}";

		public string AssetPath => "";

		public bool LocalContentExists => true;

		public async Task<IObject3D> GetObject3D(Action<double, string> reportProgress)
		{
			var object3D = await collector?.Invoke();

			// If the content has not set a color, we'll assign from the running ColorRange
			if (object3D?.Color == Color.Transparent)
			{
				object3D.Color = this.Color;
			}

			return object3D;
		}
	}
}
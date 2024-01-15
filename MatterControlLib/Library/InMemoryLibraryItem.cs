﻿/*
Copyright (c) 2018, John Lewin
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
using System.IO;
using System.Threading.Tasks;
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.Library
{
    public class InMemoryLibraryItem : ILibraryObject3D, ILibraryAssetStream
	{
		public IObject3D Object3D { get; set; }

		public InMemoryLibraryItem(IObject3D object3D)
		{
			this.Object3D = object3D;

			if ((string.IsNullOrEmpty(object3D.Name) || object3D.Name.StartsWith("Workspace 20") || object3D.Name.StartsWith("New Design"))
				&& object3D.Children.Count == 1
				&& !string.IsNullOrEmpty(object3D.Children[0].Name))
			{
				this.Name = object3D.Children[0].Name;
			}
			else
			{
				this.Name = string.IsNullOrEmpty(object3D.Name) ? "Default".Localize() : object3D.Name;
			}
		}

		public string ID => Object3D.ID;

		private string _name;
		public string Name
		{
			get => _name;
			
			set
			{
				if (_name != value)
				{
					_name = value;
					NameChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		public event EventHandler NameChanged;

		public string FileName => Path.ChangeExtension(this.Name, this.ContentType);

		public bool IsProtected => !Object3D.Persistable;

		public bool IsVisible => Object3D.Visible;

		public DateTime DateCreated { get; } = DateTime.Now;

		public DateTime DateModified { get; } = DateTime.Now;

		public string ContentType => "mcx";

		public string Category => "General";

		public string AssetPath { get; set; }

		public long FileSize => this.ToStream().Length;

		public bool LocalContentExists => false;

		public Task<IObject3D> GetObject3D(Action<double, string> reportProgress)
		{
			return Task.FromResult(Object3D);
		}

		public Task<StreamAndLength> GetStream(Action<double, string> progress)
		{
			return Task.FromResult(new StreamAndLength()
			{
				Stream = this.ToStream()
			});
		}

		private MemoryStream ToStream()
		{
			// Serialize to in memory stream
			var memoryStream = new MemoryStream();

			Object3D.SaveTo(memoryStream);

			// Reset to start of content
			memoryStream.Position = 0;

			return memoryStream;
		}
	}
}
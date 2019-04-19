/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.DesignTools
{
	public class PPEContext
	{
		public IObject3D item { get; set; }
		public Dictionary<string, GuiWidget> editRows { get; private set; } = new Dictionary<string, GuiWidget>();

		public GuiWidget GetEditRow(string propertyName)
		{
			GuiWidget value;
			if (editRows.TryGetValue(propertyName, out value))
			{
				return value;
			}

			return null;
		}
	}

	public class PublicPropertyChange
	{
		public PPEContext Context { get; }
		public string Changed { get; }

		public PublicPropertyChange(PPEContext pPEContext, string propertyChanged)
		{
			this.Context = pPEContext;
			this.Changed = propertyChanged;
		}


		/// <summary>
		/// Set the visibility of a property line item in the property editor
		/// </summary>
		/// <param name="editRowName"></param>
		/// <param name="change"></param>
		/// <param name="visible"></param>
		public void SetRowVisible(string editRowName, Func<bool> visible)
		{
			var editRow = this.Context.GetEditRow(editRowName);
			if (editRow != null)
			{
				editRow.Visible = visible.Invoke();
			}
		}
	}

	public interface ITransformWarpperObject3D
	{
		IObject3D TransformWarpper { get; }
	}

	public interface IPropertyGridModifier
	{
		void UpdateControls(PublicPropertyChange change);
	}
}
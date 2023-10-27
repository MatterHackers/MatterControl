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

using System.Collections.Generic;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.DesignTools
{
    public class EditorContext
	{
		/// <summary>
		/// This is the object that is being edited (It could be a Object3D or any other class that has public properties)
		/// </summary>
		public object Item { get; set; }
		
		/// <summary>
		/// All of the rows that have been added to the property editor (the public properties of the Item)
		/// </summary>
		public Dictionary<string, GuiWidget> EditRows { get; private set; } = new Dictionary<string, GuiWidget>();

		/// <summary>
		/// An easy way to get the widget for a property by name (if it exists)
		/// </summary>
		/// <param name="propertyName">The actual text of the property</param>
		/// <returns>The widget that was created for this property</returns>
		public GuiWidget GetEditRow(string propertyName)
		{
			GuiWidget value;
			if (EditRows.TryGetValue(propertyName, out value))
			{
				return value;
			}

			return null;
		}
	}
}
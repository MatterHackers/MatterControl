/*
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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class ListStringField : UIField
	{
		private ThemeConfig theme;

		public ListStringField(ThemeConfig theme)
		{
			this.theme = theme;
		}

		public override void Initialize(int tabIndex)
		{
			this.Content = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(20, 0, 0, 0),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};

			base.Initialize(tabIndex);
		}

		public List<string> _list = new List<string>();
		public List<string> ListValue
		{
			get => _list;
			set
			{
				_list = value;

				this.Rebuild();
			}
		}

		protected virtual void Rebuild()
		{
			this.Content.CloseAllChildren();

			for (int i = 0; i < _list.Count; i++)
			{
				var item = _list[i];

				var inlineEdit = new InlineListItemEdit(item, theme, "none");

				var localIndex = i;

				inlineEdit.ValueChanged += (s, e) =>
				{
					_list[localIndex] = inlineEdit.Text;
				};

				inlineEdit.ItemDeleted += (s, e) =>
				{
					_list.RemoveAt(localIndex);
					this.Rebuild();
				};

				this.Content.AddChild(inlineEdit);
			}

			var addItem = new IconButton(AggContext.StaticData.LoadIcon("md-add-circle_18.png", 18, 18, theme.InvertIcons), theme)
			{
				HAnchor = HAnchor.Right | HAnchor.Absolute,
				Width = theme.ButtonHeight,
				Height = theme.ButtonHeight,
				VAnchor = VAnchor.Absolute,
				Margin = 3
			};

			addItem.Click += (s, e) =>
			{
				_list.Add("New Entry");
				this.Rebuild();
			};

			this.Content.AddChild(addItem);
		}
	}
}

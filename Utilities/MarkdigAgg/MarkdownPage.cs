// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using System.Net;
using Markdig.Renderers;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;

namespace Markdig.Agg
{
	public class MarkdownPage : DialogPage
	{
		public MarkdownPage()
		{
			this.WindowTitle = this.HeaderText = "Markdown Tests";
			contentRow.AddChild(new MarkdownWidget(new Uri("https://raw.githubusercontent.com/lunet-io/markdig/master/"),
				new Uri("https://raw.githubusercontent.com/lunet-io/markdig/master/readme.md")));
		}
	}
}

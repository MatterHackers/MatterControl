// Copyright (c) 2016-2017 Nicolas Musset. All rights reserved.
// This file is licensed under the MIT license.
// See the LICENSE.md file in the project root for more information.

using System;

// ReSharper disable once CheckNamespace
namespace Markdig.Agg
{
	/// <summary>
	/// Provides extension methods for <see cref="MarkdownPipeline"/> to enable several Markdown extensions.
	/// </summary>
	public static class MarkdownExtensions
    {
        /// <summary>
        /// Uses all extensions supported by <c>Markdig.Agg</c>.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <returns>The modified pipeline</returns>
        public static MarkdownPipelineBuilder UseSupportedExtensions(this MarkdownPipelineBuilder pipeline)
        {
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));
            return pipeline
                .UseEmphasisExtras()
                .UseGridTables()
                .UsePipeTables()
                .UseTaskLists()
                .UseAutoLinks();
        }
    }
}

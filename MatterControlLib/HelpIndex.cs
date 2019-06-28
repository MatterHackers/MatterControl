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
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using Newtonsoft.Json;

namespace MatterControlLib
{
	public static class HelpIndex
	{
		private static IndexWriter writer;
		private static StandardAnalyzer analyzer;

		static HelpIndex()
		{
			// Ensures index backwards compatibility
			var AppLuceneVersion = LuceneVersion.LUCENE_48;

			var indexLocation = System.IO.Path.Combine(ApplicationDataStorage.Instance.ApplicationTempDataPath, "LuceneIndex");
			System.IO.Directory.CreateDirectory(indexLocation);

			// create an analyzer to process the text
			analyzer = new StandardAnalyzer(AppLuceneVersion);

			// create an index writer
			writer = new IndexWriter(
				FSDirectory.Open(indexLocation),
				new IndexWriterConfig(AppLuceneVersion, analyzer));
		}

		public static bool IndexExists => writer.MaxDoc > 0;

		public static Task RebuildIndex()
		{
			// If the index lacks a reasonable number of documents, rebuild it from the zip file
			return ApplicationController.Instance.Tasks.Execute(
				"Preparing help index".Localize(),
				null,
				(progress, cancellationToken) =>
				{
					string relativePath = System.IO.Path.Combine("OemSettings", "help-docs.zip");

					IndexZipFile(relativePath, progress, cancellationToken);

					return Task.CompletedTask;
				});
		}

		private static void ProcessHelpTree(HelpArticle context, Dictionary<string, HelpArticle> helpArticles)
		{
			helpArticles[context.Path] = context;

			foreach (var child in context.Children)
			{
				ProcessHelpTree(child, helpArticles);
			}
		}

		private static void IndexZipFile(string filePath, IProgress<ProgressStatus> progress, CancellationToken cancellationToken)
		{
			Dictionary<string, HelpArticle> helpArticles;

			// Clear existing
			writer.DeleteAll();

			// Build index from help-docs.zip
			using (var file = AggContext.StaticData.OpenStream(filePath))
			using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
			{
				var tocEntry = zip.Entries.FirstOrDefault(e => e.FullName == "toc.json");

				using (var docStream = tocEntry.Open())
				{
					var reader = new System.IO.StreamReader(docStream);
					var tocText = reader.ReadToEnd();

					var rootHelpArticle = JsonConvert.DeserializeObject<HelpArticle>(tocText);

					helpArticles = new Dictionary<string, HelpArticle>();

					// Walk the documents tree building up a dictionary of article paths to articles
					ProcessHelpTree(rootHelpArticle, helpArticles);
				}

				var progressStatus = new ProgressStatus()
				{
					//Status = "",
					Progress0To1 = 0
				};

				var count = zip.Entries.Count;
				double i = 0;

				foreach (var entry in zip.Entries)
				{
					progressStatus.Progress0To1 = i++ / count;
					progress.Report(progressStatus);

					// Observe and abort on cancellationToken signal
					if (cancellationToken.IsCancellationRequested)
					{
						writer.DeleteAll();
						writer.Commit();
						return;
					}

					if (entry.FullName.ToLower().EndsWith(".md"))
					{
						using (var docStream = entry.Open())
						{
							var reader = new System.IO.StreamReader(docStream);

							string text = reader.ReadToEnd();

							var doc = new Document();

							// StringField indexes but doesn't tokenise
							doc.Add(new TextField("name", helpArticles[entry.FullName].Name, Field.Store.YES));
							doc.Add(new StringField("path", entry.FullName, Field.Store.YES));
							doc.Add(new TextField("body", text, Field.Store.NO));

							writer.AddDocument(doc);
							writer.Flush(triggerMerge: false, applyAllDeletes: false);
						}
					}
				}
			}

			writer.Commit();
		}

		public static IEnumerable<HelpSearchResult> Search(string text)
		{
			// If the index lacks a reasonable number of documents, rebuild it from the zip file
			if (writer.MaxDoc < 10)
			{
				RebuildIndex();
			}

			var parser = new MultiFieldQueryParser(LuceneVersion.LUCENE_48, new[] { "body", "name" }, analyzer);
			var query = parser.Parse(text);

			// re-use the writer to get real-time updates
			var searcher = new IndexSearcher(writer.GetReader(applyAllDeletes: true));

			var hits = searcher.Search(query, 40 /* top 20 */).ScoreDocs;

			return hits.Select(hit =>
			{
				var foundDoc = searcher.Doc(hit.Doc);
				return new HelpSearchResult()
				{
					Name = foundDoc.Get("name"),
					Path = foundDoc.Get("path")
				};
			});
		}
	}
}

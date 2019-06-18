
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using Newtonsoft.Json;

namespace MatterControlLib
{
	public class LuceneHelpSearch
	{
		private static IndexWriter writer;
		private static StandardAnalyzer analyzer;

		static LuceneHelpSearch()
		{
			// Ensures index backwards compatibility
			var AppLuceneVersion = LuceneVersion.LUCENE_48;

			var indexLocation = System.IO.Path.Combine(ApplicationDataStorage.Instance.ApplicationTempDataPath, "LuceneIndex");
			System.IO.Directory.CreateDirectory(indexLocation);

			var dir = FSDirectory.Open(indexLocation);

			// create an analyzer to process the text
			analyzer = new StandardAnalyzer(AppLuceneVersion);

			// create an index writer
			var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);

			writer = new IndexWriter(dir, indexConfig);

			//IndexDocuments();
		}

		public LuceneHelpSearch()
		{
		}


		private static void ProcessHelpTree(HelpArticle context, Dictionary<string, HelpArticle> helpArticles)
		{
			helpArticles[context.Path] = context;

			foreach (var child in context.Children)
			{
				ProcessHelpTree(child, helpArticles);
			}
		}

		private static void IndexDocuments()
		{
			Dictionary<string, HelpArticle> helpArticles;

			// Clear existing
			writer.DeleteAll();

			// Build index from help-docs.zip
			using (var file = AggContext.StaticData.OpenStream(System.IO.Path.Combine("OemSettings", "help-docs.zip")))
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

				foreach (var entry in zip.Entries)
				{
					if (entry.FullName.ToLower().EndsWith(".md"))
					{
						using (var docStream = entry.Open())
						{
							var reader = new System.IO.StreamReader(docStream);

							string text = reader.ReadToEnd();

							var doc = new Document();

							// StringField indexes but doesn't tokenise
							doc.Add(new StringField("name", helpArticles[entry.FullName].Name, Field.Store.YES));
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

		public IEnumerable<HelpSearchResult> Search(string text)
		{
			var parser = new QueryParser(LuceneVersion.LUCENE_48, "body", analyzer);
			var query = parser.Parse(text);

			// re-use the writer to get real-time updates
			var searcher = new IndexSearcher(writer.GetReader(applyAllDeletes: true));

			var hits = searcher.Search(query, 20 /* top 20 */).ScoreDocs;

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

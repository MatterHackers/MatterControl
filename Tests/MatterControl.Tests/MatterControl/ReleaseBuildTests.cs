#if !__ANDROID__
using MatterHackers.MatterControl.Tests.Automation;
#endif
using Xunit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Threading;
using MatterHackers.MatterControl;


namespace MatterControl.Tests
{
	//[TestFixture, Parallelizable(ParallelScope.Children)]
	public class ReleaseBuildTests
	{
		private static Type debuggableAttribute = typeof(DebuggableAttribute);

        [Fact] // [Test, ChildProcessTest, Category("ReleaseQuality")]
        public void MatterControlAssemblyIsOptimized()
		{
#if (!DEBUG)
            IsAssemblyOptimized(Assembly.Load("MatterControlLib, Culture=neutral, PublicKeyToken=null"));
#endif
		}

        [Fact] // [Test, ChildProcessTest, Category("ReleaseQuality")]
        public void MatterControlKnownAssembliesAreOptimized()
		{
			//MatterHackers.RenderOpenGl.dll

			// This list can be refreshed via the rebuildDependencies() helper function below
			string knownAssemblies = @"VectorMath.dll
						Agg.dll
						PolygonMesh.dll
						agg_clipper_library.dll
						Gui.dll
						Tesselate.dll
						DataConverters2D.dll
						DataConverters3D.dll
						Localizations.dll
						Community.CsharpSqlite.dll
						MatterHackers.Agg.ImageProcessing.dll
						MatterHackers.MarchingSquares.dll
						GuiAutomation.dll";

			foreach (string assemblyName in knownAssemblies.Split('\n').Select(s => s.Trim()))
			{
				var assemblyPath = Path.Combine(MatterControlUtilities.MainBinOutputPath, assemblyName);



				// Missing/renamed assemblies should fail the test and force a correction
				Assert.True(File.Exists(assemblyPath), "Assembly missing: " + assemblyPath);
#if (!DEBUG)
				var assembly = Assembly.LoadFrom(assemblyPath);
				IsAssemblyOptimized(assembly);
#endif
			}
		}

		private void rebuildDependencies()
		{
			// Modify path to point at a recent BuildAgent results file
			var elem = XElement.Load(@"C:\Data\Sources\MatterHackers\BuildAndDeployment\MatterControl\build_sln.xml");
			var items = elem.Descendants().Where(e => e.Name == "target" && "CopyFilesToOutputDirectory" == (string)e.Attribute("name")).SelectMany(e => e.Elements("message").Select(e2 => e2.Value.TrimEnd('.')).Where(s => s.Contains("Copying") && s.Contains(".dll")));

			var referencedItems = new List<string>();

			foreach (var item in items)
			{
				var segments = System.Text.RegularExpressions.Regex.Split(item, "to \"");

				var relativeAssemblyName = segments[1].TrimEnd('"');

				var assemblyName = Path.GetFileName(relativeAssemblyName);

				referencedItems.Add(assemblyName);
			}

			Console.WriteLine(referencedItems);
		}

#if !__ANDROID__
		[Fact]
		public async Task MatterControlRuns()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("PartPreviewContent");

				Assert.True(testRunner.NameExists("PartPreviewContent"));

				return Task.CompletedTask;
			});
		}
#endif

        [Fact] // [Test, ChildProcessTest, Category("ReleaseQuality")]
        public void MatterControlDependenciesAreOptimized()
		{
#if (!DEBUG)
			var matterControl = Assembly.Load("MatterControlLib, Culture=neutral, PublicKeyToken=null");

			// Loop over all referenced assemblies to verify they are optimized and lack (symbols and Debug compile flag)
			foreach (var assemblyName in matterControl.GetReferencedAssemblies())
			{
				var assembly = Assembly.Load(assemblyName.FullName);
				var firstNamespace = assembly?.GetTypes()?.FirstOrDefault()?.Namespace;

				// Only validate our assemblies
				if (firstNamespace != null && (firstNamespace.Contains("MatterHackers") || firstNamespace.Contains("MatterControl")))
				{
					IsAssemblyOptimized(assembly);
				}
			}
#endif
		}

        [Fact] // [Test, ChildProcessTest, Category("ReleaseQuality")]
        public void ClassicDebugComplicationFlagTests()
		{
#if (!DEBUG)
            BuildValidationTests.CheckKnownAssemblyConditionalCompSymbols();
#endif
		}

		private static void IsAssemblyOptimized(Assembly assm)
		{
			var matchedAttributes = assm.GetCustomAttributes(debuggableAttribute, false);
			var assemblyName = assm.GetName();

			if (matchedAttributes.Count() == 0)
			{
				throw new Exception("Symbols likely missing from Release build: " + assemblyName.FullName + ". \r\n\r\nTo resolve the issue, switch Project Properties -> Build -> Advanced -> Debug Info property to 'pdb-only'");
			}

			var debuggable = matchedAttributes.First() as DebuggableAttribute;
			Assert.False(debuggable.IsJITOptimizerDisabled, "Referenced assembly is not optimized: " + assemblyName.Name);
			Assert.False(debuggable.IsJITTrackingEnabled, "Referenced assembly has symbols: " + assemblyName.Name);
			Console.WriteLine("Assembly is optimized: " + assemblyName.Name);
		}
	}
}

using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl;
#if !__ANDROID__
using MatterHackers.MatterControl.Tests.Automation;
#endif
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace MatterControl.Tests
{
	[TestFixture]
	public class ReleaseBuildTests
	{
		private static Type debuggableAttribute = typeof(DebuggableAttribute);

		[Test, Category("ReleaseQuality")]
		public void MatterControlAssemblyIsOptimized()
		{
#if (!DEBUG)
            IsAssemblyOptimized(Assembly.Load("MatterControl, Culture=neutral, PublicKeyToken=null"));
#endif
		}

		[Test, Category("ReleaseQuality")]
		public void MatterControlKnownAssembliesAreOptimized()
		{
#if DEBUG
			string configuration = "Debug";
#else
			string configuration = "Release";
#endif

			// This list can be refreshed via the rebuildDependencies() helper function below
			string knownAssemblies = @"MatterHackers.VectorMath.dll
						AGG.dll
						PlatfromAbstract.dll
						MatterHackers.PolygonMesh.dll
						MatterHackers.Csg.dll
						clipper_library.dll
						MatterHackers.Agg.UI.dll
						Tesselate.dll
						MatterHackers.DataConverters2D.dll
						MatterHackers.RenderOpenGl.dll
						RayTracer.dll
						MatterHackers.DataConverters3D.dll
						MatterHackers.Localizations.dll
						MatterHackers.OpenGL.UI.dll
						agg_platform_win32.dll
						WindowsFileDialogs.dll
						Community.CsharpSqlite.dll
						MatterHackers.SerialPortCommunication.dll
						MatterHackers.MatterControl.Plugins.dll
						MatterHackers.Agg.ImageProcessing.dll
						MatterHackers.MarchingSquares.dll
						GuiAutomation.dll
						MatterControlAuth.dll
						PictureCreator.dll
						PrintNotifications.dll
						CloudServices.dll
						X3GDriver.dll
						Mono.Nat.dll
						BrailBuilder.dll
						TextCreator.dll";

			foreach (string assemblyName in knownAssemblies.Split('\n').Select(s => s.Trim()))
			{
				var assemblyPath = TestContext.CurrentContext.ResolveProjectPath(4, "bin", configuration, assemblyName);

				// Missing/renamed assemblies should fail the test and force a correction
				Assert.IsTrue(File.Exists(assemblyPath), "Assembly missing: " + assemblyPath);
#if (!DEBUG)
				var assembly = Assembly.LoadFrom(assemblyPath);
				IsAssemblyOptimized(assembly);
#endif
			}
		}

		private void rebuildDependencies()
		{
			// Update to point to resent buildagent results file
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
		[Test, RequiresSTA, RunInApplicationDomain]
		public void MatterControlRuns()
		{
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner();
				{
					MatterControlUtilities.PrepForTestRun(testRunner, MatterControlUtilities.PrepAction.CloseSignInAndPrinterSelect);

					resultsHarness.AddTestResult(testRunner.NameExists("SettingsAndControls"));

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, maxTimeToRun: 200);
			Assert.IsTrue(testHarness.AllTestsPassed(1));
		}
#endif

		[Test, Category("ReleaseQuality")]
		public void MatterControlDependenciesAreOptimized()
		{
#if (!DEBUG)
            var matterControl = Assembly.Load("MatterControl, Culture=neutral, PublicKeyToken=null");

            // Loop over all referenced assemblies to verify they are optimized and lack (symbols and Debug compile flag)
            foreach(var assemblyName in matterControl.GetReferencedAssemblies())
            {
                var assembly = Assembly.Load(assemblyName.FullName);
                var firstNamespace = assembly.GetTypes().First().Namespace;

                // Only validate our assemblies
				if (firstNamespace != null && (firstNamespace.Contains("MatterHackers") || firstNamespace.Contains("MatterControl")))
                {
                    IsAssemblyOptimized(assembly);
                }
            }
#endif
		}

		[Test, Category("ReleaseQuality")]
		public void ClassicDebugComplicationFlagTests()
		{
#if (!DEBUG)
            MatterControlApplication.CheckKnownAssemblyConditionalCompSymbols();
#endif
		}

		private static void IsAssemblyOptimized(Assembly assm)
		{
			var matchedAttributes = assm.GetCustomAttributes(debuggableAttribute, false);
			var assemblyName = assm.GetName();

			if (matchedAttributes.Count() == 0)
			{
				Assert.Inconclusive("Symbols likely missing from Release build: " + assemblyName.FullName + ". \r\n\r\nTo resolve the issue, switch Project Properties -> Build -> Advanced -> Debug Info property to 'pdb-only'");
			}

			var debuggable = matchedAttributes.First() as DebuggableAttribute;
			Assert.IsFalse(debuggable.IsJITOptimizerDisabled, "Referenced assembly is not optimized: " + assemblyName.Name);
			Assert.IsFalse(debuggable.IsJITTrackingEnabled, "Referenced assembly is has symbols: " + assemblyName.Name);
			Console.WriteLine("Assembly is optimized: " + assemblyName.Name);
		}
	}
}

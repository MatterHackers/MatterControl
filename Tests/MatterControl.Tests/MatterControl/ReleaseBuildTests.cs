using MatterHackers.MatterControl;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MatterControl.Tests
{
    [TestFixture]
    public class ReleaseBuildTests
    {
        private static Type debuggableAttribute = typeof(DebuggableAttribute);

        [Test, Category("ReleaseQuality")]
        public void MatterControlAssemblyIsOptimized()
        {
#if(!DEBUG)
            IsAssemblyOptimized(Assembly.Load("MatterControl, Culture=neutral, PublicKeyToken=null"));
#endif
        }

        [Test, Category("ReleaseQuality")]
        public void MatterControlDependenciesAreOptimized()
        {
#if(!DEBUG)
            var matterControl = Assembly.Load("MatterControl, Culture=neutral, PublicKeyToken=null");

            // Loop over all referenced assemblies to verify they are optimized and lack (symbols and Debug compile flag)
            foreach(var assemblyName in matterControl.GetReferencedAssemblies())
            {
                var assembly = Assembly.Load(assemblyName.FullName);
                var firstNamespace = assembly.GetTypes().First().Namespace;

                // Only validate our assemblies
                if (firstNamespace.Contains("MatterHackers") || firstNamespace.Contains("MatterControl"))
                {
                    IsAssemblyOptimized(assembly);
                }
            }

            Console.WriteLine(string.Join("\r\n", matterControl.GetReferencedAssemblies().Select(a => a.Name).ToArray()));
#endif
        }

        [Test, Category("ReleaseQuality")]
        public void ClassicDebugComplicationFlagTests()
        {
#if(!DEBUG)
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

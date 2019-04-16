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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using NUnit.Framework;
using static MatterControl.Tests.MatterControl.SliceSettingsFieldTests;

namespace MatterControl.Tests.MatterControl
{
	public static class RunnerX
	{
		public static Task RunTest(this SystemWindow systemWindow, AutomationTest automationTest, int timeout)
		{
			return AutomationRunner.ShowWindowAndExecuteTests(systemWindow, automationTest, timeout);
		}

		[DebuggerStepThrough]
		public static void Add(this List<ValueMap> valueMap, string input, string expected)
		{
			valueMap.Add(new ValueMap(input, expected));
		}
	}

	[TestFixture, Category("SliceSettingsTests"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class SliceSettingsFieldTests
	{
		[SetUp]
		public void TestSetup()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));
		}

		[Test]
		public Task TestExistsForEachUIFieldType()
		{
			var testClass = this.GetType();
			var thisClassMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance);

			// Find and validate all UIField types, skipping abstract classes
			foreach (var fieldType in PluginFinder.FindTypes<UIField>().Where(fieldType => !fieldType.IsAbstract))
			{
				if (fieldType.Name == "UIField")
				{
					continue;
				}

				string expectedTestName = $"{fieldType.Name}Test";
				Assert.AreEqual(
					1,
					thisClassMethods.Where(m => m.Name == expectedTestName).Count(),
					"Required test missing: " + expectedTestName);
			}

			return Task.CompletedTask;
		}

		[Test]
		public async Task DoubleFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;
			var testField = new DoubleField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as MHNumberEdit).ActuallNumberEdit.Text,
				new List<ValueMap>()
				{
					{"0.12345", "0.12345"},
					{"1.2345", "1.2345"},
					{"12.345", "12.345"},
					{"12.7", "12.7"},
					{"+0.12345", "0.12345"},
					{"+1.2345", "1.2345"},
					{"+12.345", "12.345"},
					{"-0.12345", "-0.12345"},
					{"-1.2345", "-1.2345"},
					{"-12.345", "-12.345"},
					{"12.7", "12.7"},
					{"22", "22" },
					// Invalid values revert to expected
					{"abc", "0"},
					{"+abc", "0"},
					{"-abc", "0"},
				});
		}

		[Test]
		public async Task PositiveDoubleFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;
			var testField = new PositiveDoubleField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as MHNumberEdit).ActuallNumberEdit.Text,
				new List<ValueMap>()
				{
					{"0.12345", "0.12345"},
					{"1.2345", "1.2345"},
					{"12.345", "12.345"},
					{"12.7", "12.7"},
					{"+0.12345", "0.12345"},
					{"+1.2345", "1.2345"},
					{"+12.345", "12.345"},
					{"-0.12345", "0"},
					{"-1.2345", "0"},
					{"-12.345", "0"},
					{"-12.7", "0"},
					{"22", "22" },
					// Invalid values revert to expected
					{"abc", "0"},
					{"+abc", "0"},
					{"-abc", "0"},
				});
		}

		[Test]
		public async Task IntFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;
			var testField = new IntField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as MHNumberEdit).ActuallNumberEdit.Text,
				new List<ValueMap>()
				{
					{"0.12345", "0"},
					{"1.2345", "1"},
					{"12.345", "12"},
					{"12.7", "12"}, // Floor not round?
					{"+0.12345", "0"},
					{"+1.2345", "1"},
					{"+12.345", "12"},
					{"-0.12345", "0"},
					{"-1.2345", "-1"},
					{"-12.345", "-12"},
					{"-12.7", "-12"}, // Floor not round?
					{"22", "22" },
					// Invalid values revert to expected
					{"abc", "0"},
					{"+abc", "0"},
					{"-abc", "0"},
				});
		}

		[Test]
		public async Task DoubleOrPercentFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;
			var testField = new DoubleOrPercentField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as MHTextEditWidget).ActualTextEditWidget.Text,
				new List<ValueMap>()
				{
					{"0.12345", "0.12345"},
					{"0.12345%", "0.12345%"},
					{"1.2345", "1.2345"},
					{"1.2345%", "1.2345%"},
					{"12.345", "12.345"},
					{"12.345%", "12.345%"},
					{"12.7", "12.7"},
					{"12.7%", "12.7%"},
					{"+0.12345", "0.12345"},
					{"+0.12345%", "0.12345%"},
					{"+1.2345", "1.2345"},
					{"+1.2345%", "1.2345%"},
					{"+12.345", "12.345"},
					{"+12.345%", "12.345%"},
					{"-0.12345", "-0.12345"},
					{"-0.12345%", "-0.12345%"},
					{"-1.2345", "-1.2345"},
					{"-1.2345%", "-1.2345%"},
					{"-12.345", "-12.345"},
					{"-12.345%", "-12.345%"},
					{"12.7", "12.7"},
					{"12.7%", "12.7%"},
					{"22", "22" },
					{"22%", "22%" },
					// Invalid values revert to expected
					{"abc", "0"},
					{"abc%", "0%"},
					{"+abc", "0"},
					{"+abc%", "0%"},
					{"-abc", "0"},
					{"-abc%", "0%"},
				});
		}

		[Test]
		public async Task IntOrMmFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;
			var testField = new IntOrMmField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as MHTextEditWidget).ActualTextEditWidget.Text,
				new List<ValueMap>()
				{
					{"0.12345", "0"},
					{"0.12345mm", "0.12345mm"},
					{"1.2345", "1"},
					{"1.2345mm", "1.2345mm"},
					{"12.345", "12"},
					{"12.345mm", "12.345mm"},
					{"12.7", "12"},
					{"12.7mm", "12.7mm"},
					{"+0.12345", "0"},
					{"+0.12345mm", "0.12345mm"},
					{"+1.2345", "1"},
					{"+1.2345mm", "1.2345mm"},
					{"+12.345", "12"},
					{"+12.345mm", "12.345mm"},
					{"-0.12345", "0"},
					{"-0.12345mm", "0mm"},
					{"-1.2345", "0"},
					{"-1.2345mm", "0mm"},
					{"-12.345", "0"},
					{"-12.345mm", "0mm"},
					{"12.7", "12"},
					{"12.7mm", "12.7mm"},
					{"22", "22" },
					{"22mm", "22mm" },
					// Invalid values revert to expected
					{"abc", "0"},
					{"abcmm", "0mm"},
					{"+abc", "0"},
					{"+abcmm", "0mm"},
					{"-abc", "0"},
					{"-abcmm", "0mm"},
				});
		}

		[Test]
		public async Task ComPortFieldTest()
		{
			FrostedSerialPort.MockPortsForTest = true;

			var theme = new ThemeConfig();

			var field = new ComPortField(new PrinterConfig(PrinterSettings.Empty), theme);

			await ValidateAgainstValueMap(
				field,
				theme,
				(f) => (f.Content.Children<DropDownList>().FirstOrDefault() as DropDownList).SelectedLabel,
				new List<ValueMap>()
				{
					{"COM-TestA", "COM-TestA"},
					{"COM-TestB", "COM-TestB"},
					{"COM-TestC", "COM-TestC"},
					{"COM-Test0", "COM-Test0"},
					{"COM-Test1", "COM-Test1"},
				});
		}

		[TearDown]
		public void TearDown()
		{
			FrostedSerialPort.MockPortsForTest = false;
		}

		[Test, Ignore("Not Implemented")]
		public void CheckboxFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void MaterialIndexFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void ColorFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void ChildrenSelectorListFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void ToggleboxFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public async Task MultilineStringFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;

			var testField = new MultilineStringField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as MHTextEditWidget).ActualTextEditWidget.Text,
				new List<ValueMap>()
				{
					{"0.12345", "0.12345"},
					{"1.2345", "1.2345"},
					{"12.345", "12.345"},
					{"12.7", "12.7"},
					{"+0.12345", "+0.12345"},
					{"+1.2345", "+1.2345"},
					{"+12.345", "+12.345"},
					{"-0.12345", "-0.12345"},
					{"-1.2345", "-1.2345"},
					{"-12.345", "-12.345"},
					{"12.7", "12.7"},
					{"22", "22" },
					{"abc", "abc"},
					{"+abc", "+abc"},
					{"-abc", "-abc"},
					{"-abc\nline2", "-abc\nline2"},
				});
		}



		[Test]
		public async Task Vector2FieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;

			var testField = new Vector2Field(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) =>
				{
					return string.Join(",", field.Content.Children.OfType<MHNumberEdit>().Select(w => w.ActuallNumberEdit.Text).ToArray());
				},
				new List<ValueMap>()
				{
					{"0.1,0.2", "0.1,0.2"},
					{"1,2", "1,2"},
					{",2", "0,2"}, // Empty components should revert to 0s
					{"x,2", "0,2"}, // Non-numeric components should revert to 0s
					{"2", "0,0"}, // Non-vector4 csv should revert to Vector4.Zero
				});
		}

		[Test]
		public async Task Vector3FieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;

			var testField = new Vector3Field(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) =>
				{
					return string.Join(",", field.Content.Children.OfType<MHNumberEdit>().Select(w => w.ActuallNumberEdit.Text).ToArray());
				},
				new List<ValueMap>()
				{
					{"0.1,0.2,0.3", "0.1,0.2,0.3"},
					{"1,2,3", "1,2,3"},
					{",2,", "0,2,0"}, // Empty components should revert to 0s
					{"x,2,y", "0,2,0"}, // Non-numeric components should revert to 0s
					{",2", "0,0,0"}, // Non-vector4 csv should revert to Vector4.Zero
				});
		}

		[Test]
		public async Task Vector4FieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;

			Vector4Field.VectorXYZWEditWidth = 50;

			var testField = new Vector4Field(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) =>
				{
					return string.Join(",", field.Content.Children.OfType<MHNumberEdit>().Select(w => w.ActuallNumberEdit.Text).ToArray());
				},
				new List<ValueMap>()
				{
					{"0.1,0.2,0.3,0.4", "0.1,0.2,0.3,0.4"},
					{"1,2,3,4", "1,2,3,4"},
					{",2,,4", "0,2,0,4"}, // Empty components should revert to 0s
					{"x,2,y,4", "0,2,0,4"}, // Non-numeric components should revert to 0s
					{",2,", "0,0,0,0"}, // Non-vector4 csv should revert to Vector4.Zero
				});
		}

		[Test]
		public async Task BoundsFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;

			var testField = new BoundsField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) =>
				{
					return string.Join(",", field.Content.Children.OfType<MHNumberEdit>().Select(w => w.ActuallNumberEdit.Text).ToArray());
				},
				new List<ValueMap>()
				{
					{"0.1,0.2,0.3,0.4", "0.1,0.2,0.3,0.4"},
					{"1,2,3,4", "1,2,3,4"},
					{",2,,4", "0,2,0,4"}, // Empty components should revert to 0s
					{"x,2,y,4", "0,2,0,4"}, // Non-numeric components should revert to 0s
					{",2,", "0,0,0,0"}, // Non-vector4 csv should revert to Vector4.Zero
				});
		}

		[Test, Ignore("Not Implemented")]
		public void ListFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void ExtruderOffsetFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void TextFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void ReadOnlyTextFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void BoundDoubleFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void CharFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void DirectionVectorFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void EnumFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void IconEnumFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public async Task MarkdownEditFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public async Task ListStringFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public async Task SurfacedEditorsFieldTest()
		{
			Assert.Fail();
		}

		public class ValueMap
		{
			[DebuggerStepThrough]
			public ValueMap(string input, string expected)
			{
				this.InputValue = input;
				this.ExpectedValue = expected;
			}

			public string InputValue { get; }
			public string ExpectedValue { get; }
		}

		/// <summary>
		/// Take a UIField, a delegate to resolve the UI widget value and a map of input->expected values and validates the results for a given field
		/// </summary>
		/// <param name="field"></param>
		/// <param name="collectValueFromWidget">A delegate to resolve the currently displayed widget value</param>
		/// <param name="valuesMap">A map of input to expected values</param>
		/// <returns></returns>
		public static Task ValidateAgainstValueMap(UIField field, ThemeConfig theme, Func<UIField, string> collectValueFromWidget, IEnumerable<ValueMap> valuesMap)
		{
			// *************** Enable to investigate/debug/develop new/existing tests ************************
			bool investigateDebugTests = false;
			var perItemDelay = (investigateDebugTests) ? 1000 : 0;

			var testsWindow = new UIFieldTestWindow(500, 200, field, theme);

			return testsWindow.RunTest((testRunner) =>
			{
				foreach (var item in valuesMap)
				{
					testsWindow.SetAndValidateValues(item.ExpectedValue, item.InputValue, collectValueFromWidget, perItemDelay);
				}

				return Task.CompletedTask;
			}, 30);

		}
	}
}

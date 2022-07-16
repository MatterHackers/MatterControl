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
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using NUnit.Framework;
using static MatterControl.Tests.MatterControl.SliceSettingsFieldTests;
using TestInvoker;

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

	// NOTE: These tests hang on GLFW currently as the window isn't closed properly.
	[TestFixture, Category("SliceSettingsTests"), Parallelizable(ParallelScope.Children)]
	public class SliceSettingsFieldTests
	{
		[SetUp]
		public void TestSetup()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);
		}

		[Test, ChildProcessTest]
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

		[Test, ChildProcessTest]
		public async Task DoubleFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;
			var testField = new DoubleField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as ThemedNumberEdit).ActuallNumberEdit.Text,
				new List<ValueMap>()
				{
					{ "0.12345", "0.12345" },
					{ "1.2345", "1.2345" },
					{ "12.345", "12.345" },
					{ "12.7", "12.7" },
					{ "+0.12345", "0.12345" },
					{ "+1.2345", "1.2345" },
					{ "+12.345", "12.345" },
					{ "-0.12345", "-0.12345" },
					{ "-1.2345", "-1.2345" },
					{ "-12.345", "-12.345" },
					{ "12.7", "12.7" },
					{ "22", "22" },
					// Invalid values revert to expected
					{ "abc", "0" },
					{ "+abc", "0" },
					{ "-abc", "0" },
				});
		}

		[Test, ChildProcessTest]
		public async Task PositiveDoubleFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;
			var testField = new PositiveDoubleField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as ThemedNumberEdit).ActuallNumberEdit.Text,
				new List<ValueMap>()
				{
					{ "0.12345", "0.12345" },
					{ "1.2345", "1.2345" },
					{ "12.345", "12.345" },
					{ "12.7", "12.7" },
					{ "+0.12345", "0.12345" },
					{ "+1.2345", "1.2345" },
					{ "+12.345", "12.345" },
					{ "-0.12345", "0" },
					{ "-1.2345", "0" },
					{ "-12.345", "0" },
					{ "-12.7", "0" },
					{ "22", "22" },
					// Invalid values revert to expected
					{ "abc", "0" },
					{ "+abc", "0" },
					{ "-abc", "0" },
				});
		}

		[Test, ChildProcessTest]
		public async Task IntFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;
			var testField = new IntField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as ThemedNumberEdit).ActuallNumberEdit.Text,
				new List<ValueMap>()
				{
					{ "0.12345", "0" },
					{ "1.2345", "1" },
					{ "12.345", "12" },
					{ "12.7", "12" }, // Floor not round?
					{ "+0.12345", "0" },
					{ "+1.2345", "1" },
					{ "+12.345", "12" },
					{ "-0.12345", "0" },
					{ "-1.2345", "-1" },
					{ "-12.345", "-12" },
					{ "-12.7", "-12" }, // Floor not round?
					{ "22", "22" },
					// Invalid values revert to expected
					{ "abc", "0" },
					{ "+abc", "0" },
					{ "-abc", "0" },
				});
		}

		[Test, ChildProcessTest]
		public async Task DoubleOrPercentFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;
			var testField = new DoubleOrPercentField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as ThemedTextEditWidget).ActualTextEditWidget.Text,
				new List<ValueMap>()
				{
					{ "0.12345", "0.12345" },
					{ "0.12345%", "0.12345%" },
					{ "1.2345", "1.2345" },
					{ "1.2345%", "1.2345%" },
					{ "12.345", "12.345" },
					{ "12.345%", "12.345%" },
					{ "12.7", "12.7" },
					{ "12.7%", "12.7%" },
					{ "+0.12345", "0.12345" },
					{ "+0.12345%", "0.12345%" },
					{ "+1.2345", "1.2345" },
					{ "+1.2345%", "1.2345%" },
					{ "+12.345", "12.345" },
					{ "+12.345%", "12.345%" },
					{ "-0.12345", "-0.12345" },
					{ "-0.12345%", "-0.12345%" },
					{ "-1.2345", "-1.2345" },
					{ "-1.2345%", "-1.2345%" },
					{ "-12.345", "-12.345" },
					{ "-12.345%", "-12.345%" },
					{ "12.7", "12.7" },
					{ "12.7%", "12.7%" },
					{ "22", "22" },
					{ "22%", "22%" },
					// Invalid values revert to expected
					{ "abc", "0" },
					{ "abc%", "0%" },
					{ "+abc", "0" },
					{ "+abc%", "0%" },
					{ "-abc", "0" },
					{ "-abc%", "0%" },
				});
		}

		[Test, ChildProcessTest]
		public async Task IntOrMmFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;
			var testField = new IntOrMmField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as ThemedTextEditWidget).ActualTextEditWidget.Text,
				new List<ValueMap>()
				{
					{ "0.12345", "0" },
					{ "0.12345mm", "0.12345mm" },
					{ "1.2345", "1" },
					{ "1.2345mm", "1.2345mm" },
					{ "12.345", "12" },
					{ "12.345mm", "12.345mm" },
					{ "12.7", "12" },
					{ "12.7mm", "12.7mm" },
					{ "+0.12345", "0" },
					{ "+0.12345mm", "0.12345mm" },
					{ "+1.2345", "1" },
					{ "+1.2345mm", "1.2345mm" },
					{ "+12.345", "12" },
					{ "+12.345mm", "12.345mm" },
					{ "-0.12345", "0" },
					{ "-0.12345mm", "0mm" },
					{ "-1.2345", "0" },
					{ "-1.2345mm", "0mm" },
					{ "-12.345", "0" },
					{ "-12.345mm", "0mm" },
					{ "12.7", "12" },
					{ "12.7mm", "12.7mm" },
					{ "22", "22" },
					{ "22mm", "22mm" },
					// Invalid values revert to expected
					{ "abc", "0" },
					{ "abcmm", "0mm" },
					{ "+abc", "0" },
					{ "+abcmm", "0mm" },
					{ "-abc", "0" },
					{ "-abcmm", "0mm" },
				});
		}

		[Test, ChildProcessTest]
		public void CorrectStyleForSettingsRow()
		{
			var settings = new PrinterSettings();
			var printer = new PrinterConfig(settings);

			settings.OemLayer = new PrinterSettingsLayer();
			settings.QualityLayer = new PrinterSettingsLayer();
			settings.MaterialLayer = new PrinterSettingsLayer();
			Assert.AreEqual(0, settings.UserLayer.Count);

			var theme = new ThemeConfig();
			var settingsContext = new SettingsContext(printer, null, NamedSettingsLayers.All);

			var key = SettingsKey.layer_height;

			void TestStyle(Color color, bool restoreButton)
			{
				var data = SliceSettingsRow.GetStyleData(printer, theme, settingsContext, key, true);
				Assert.AreEqual(color, data.highlightColor);
				Assert.AreEqual(restoreButton, data.showRestoreButton);
			}

			// make sure all the colors are different
			Assert.AreNotEqual(Color.Transparent, theme.PresetColors.MaterialPreset);
			Assert.AreNotEqual(Color.Transparent, theme.PresetColors.QualityPreset);
			Assert.AreNotEqual(theme.PresetColors.MaterialPreset, theme.PresetColors.QualityPreset);
			Assert.AreNotEqual(theme.PresetColors.MaterialPreset, theme.PresetColors.UserOverride);
			Assert.AreNotEqual(theme.PresetColors.QualityPreset, theme.PresetColors.UserOverride);

			// nothing set no override
			TestStyle(Color.Transparent, false);

			// user override
			settings.UserLayer[key] = "123";
			TestStyle(theme.PresetColors.UserOverride, true);
			settings.UserLayer.Remove(key);

			// Quality override
			settings.QualityLayer[key] = "123";
			TestStyle(theme.PresetColors.QualityPreset, false);
			settings.QualityLayer.Remove(key);

			// Material override
			settings.MaterialLayer[key] = "123";
			TestStyle(theme.PresetColors.MaterialPreset, false);
			settings.MaterialLayer.Remove(key);

			// user override that is the same as the default
			settings.UserLayer[key] = settings.BaseLayer[key];
			TestStyle(Color.Transparent, false);
			settings.UserLayer.Remove(key);

			// Quality override same as default
			settings.QualityLayer[key] = settings.BaseLayer[key];
			TestStyle(theme.PresetColors.QualityPreset, false);
			settings.QualityLayer.Remove(key);

			// Material override same as default
			settings.MaterialLayer[key] = settings.BaseLayer[key];
			TestStyle(theme.PresetColors.MaterialPreset, false);
			settings.MaterialLayer.Remove(key);
		}

		[Test, ChildProcessTest]
		public void RightClickMenuWorksOnSliceSettings()
		{
			PrinterSettings.SliceEngines["MatterSlice"] = new EngineMappingsMatterSlice();
			Clipboard.SetSystemClipboard(new SimulatedClipboard());

			var systemWindow = new SystemWindow(800, 600)
			{
				Name = "Main Window",
			};

			Application.AddTextWidgetRightClickMenu();

			AutomationRunner.TimeToMoveMouse = .1;

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			systemWindow.AddChild(container);

			var theme = ApplicationController.Instance.Theme;
			var settings = new PrinterSettings();
			settings.SetValue(SettingsKey.printer_name, "Test Name");
			settings.SetValue(SettingsKey.start_gcode, "Test GCode");
			var printer = new PrinterConfig(settings);
			var settingsContext = new SettingsContext(printer, null, NamedSettingsLayers.All);
			var testData = new (string setting, string paste, bool selectsOnFocus, string postCutAll)[]
			{
				(SettingsKey.layer_height, "987.654", true, "0"),
				(SettingsKey.start_gcode, "Paste text", false, ""),
				(SettingsKey.printer_name, "three word name", true, ""),
			};

			int tabIndex = 0;

			var fields = new Dictionary<string, GuiWidget>();

			for (int i = 0; i < testData.Length; i++)
			{
				var settingsData = PrinterSettings.SettingsData[testData[i].setting];

				var itemRow = SliceSettingsTabView.CreateItemRow(settingsData,
					settingsContext,
					printer,
					theme,
					ref tabIndex);

				fields[testData[i].setting] = itemRow;

				container.AddChild(itemRow);
			}

			systemWindow.RunTest(testRunner =>
			{
				void RunSingeTest(string setting, string pastText, bool selectsOnFocus, string postCutAll)
				{
					var textWidget = fields[setting].Descendants<InternalTextEditWidget>().First();
					// no selection to start
					Assert.IsTrue(string.IsNullOrEmpty(textWidget.Selection), "Should not have selection");

					// select all and ensure everything is selected
					testRunner.RightClickByName(GetSliceSettingsField(setting));
					if (selectsOnFocus)
					{
						testRunner.Assert(() => !string.IsNullOrEmpty(textWidget.Selection), "Should have selection");
					}
					else
					{
						testRunner.Assert(() => string.IsNullOrEmpty(textWidget.Selection), "Should not have selection");
					}

					testRunner
						.ClickByName("Select All Menu Item")
						.Assert(() => textWidget.Selection == textWidget.Text, "Everything is selected");

					// make sure there is no text in the copy buffer
					Clipboard.Instance.SetText("");

					// copy text out and make sure we get it
					testRunner
						.RightClickByName(GetSliceSettingsField(setting))
						.Assert(() => textWidget.Selection == textWidget.Text, "Selection remains after right click")
						.ClickByName("Copy Menu Item")
						.Assert(() => Clipboard.Instance.GetText() == textWidget.Text, "Copied everything");

					// past in text and make sure it changed
					Clipboard.Instance.SetText(pastText);
					testRunner
						.RightClickByName(GetSliceSettingsField(setting))
						.ClickByName("Paste Menu Item")
						.Assert(() => pastText == textWidget.Text, "Pasted everything");

					// make sure we lose selection if we click off the menu

					// cut works
					testRunner
						.RightClickByName(GetSliceSettingsField(setting))
						.ClickByName("Select All Menu Item")
						.Assert(() => textWidget.Selection == textWidget.Text, "Everything is selected");

					testRunner
						.RightClickByName(GetSliceSettingsField(setting))
						.Assert(() => textWidget.Selection == textWidget.Text, "Selection remains after right click");
					var preCutText = textWidget.Text;
					testRunner
						.ClickByName("Cut Menu Item")
						.ClickByName("Main Window") // loose the selection
						.Assert(() => postCutAll == textWidget.Text, "Cut everything")
						.Assert(() => preCutText == Clipboard.Instance.GetText(), "Cut everything");
				}

				// testRunner.Delay(2000);

				for (int i = 0; i < testData.Length; i++)
				{
					var data = testData[i];
					RunSingeTest(data.setting, data.paste, data.selectsOnFocus, data.postCutAll);
				}

				return Task.CompletedTask;
			},
			2000);
		}

		private string GetSliceSettingsField(string sliceSettingsKey)
		{
			var settingData = PrinterSettings.SettingsData[sliceSettingsKey];
			return $"{settingData.PresentationName} Field";
		}

		[Test, ChildProcessTest]
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
					{ "COM-TestA", "COM-TestA" },
					{ "COM-TestB", "COM-TestB" },
					{ "COM-TestC", "COM-TestC" },
					{ "COM-Test0", "COM-Test0" },
					{ "COM-Test1", "COM-Test1" },
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

		[Test, ChildProcessTest]
		public async Task MultilineStringFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;

			var testField = new MultilineStringField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) => (field.Content as ThemedTextEditWidget).ActualTextEditWidget.Text,
				new List<ValueMap>()
				{
					{ "0.12345", "0.12345" },
					{ "1.2345", "1.2345" },
					{ "12.345", "12.345" },
					{ "12.7", "12.7" },
					{ "+0.12345", "+0.12345" },
					{ "+1.2345", "+1.2345" },
					{ "+12.345", "+12.345" },
					{ "-0.12345", "-0.12345" },
					{ "-1.2345", "-1.2345" },
					{ "-12.345", "-12.345" },
					{ "12.7", "12.7" },
					{ "22", "22" },
					{ "abc", "abc" },
					{ "+abc", "+abc" },
					{ "-abc", "-abc" },
					{ "-abc\nline2", "-abc\nline2" },
				});
		}

		[Test, ChildProcessTest]
		public async Task Vector2FieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;

			var testField = new Vector2Field(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) =>
				{
					return string.Join(",", field.Content.Children.OfType<ThemedNumberEdit>().Select(w => w.ActuallNumberEdit.Text).ToArray());
				},
				new List<ValueMap>()
				{
					{ "0.1,0.2", "0.1,0.2" },
					{ "1,2", "1,2" },
					{ ",2", "0,2" }, // Empty components should revert to 0s
					{ "x,2", "0,2" }, // Non-numeric components should revert to 0s
					{ "2", "0,0" }, // Non-vector4 csv should revert to Vector4.Zero
				});
		}

		[Test, ChildProcessTest]
		public async Task Vector3FieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;

			var testField = new Vector3Field(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) =>
				{
					return string.Join(",", field.Content.Children.OfType<ThemedNumberEdit>().Select(w => w.ActuallNumberEdit.Text).ToArray());
				},
				new List<ValueMap>()
				{
					{ "0.1,0.2,0.3", "0.1,0.2,0.3" },
					{ "1,2,3", "1,2,3" },
					{ ",2,", "0,2,0" }, // Empty components should revert to 0s
					{ "x,2,y", "0,2,0" }, // Non-numeric components should revert to 0s
					{ ",2", "0,0,0" }, // Non-vector4 csv should revert to Vector4.Zero
				});
		}

		[Test, ChildProcessTest]
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
					return string.Join(",", field.Content.Children.OfType<ThemedNumberEdit>().Select(w => w.ActuallNumberEdit.Text).ToArray());
				},
				new List<ValueMap>()
				{
					{ "0.1,0.2,0.3,0.4", "0.1,0.2,0.3,0.4" },
					{ "1,2,3,4", "1,2,3,4" },
					{ ",2,,4", "0,2,0,4" }, // Empty components should revert to 0s
					{ "x,2,y,4", "0,2,0,4" }, // Non-numeric components should revert to 0s
					{ ",2,", "0,0,0,0" }, // Non-vector4 csv should revert to Vector4.Zero
				});
		}

		[Test, ChildProcessTest]
		public async Task BoundsFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;

			var testField = new BoundsField(theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) =>
				{
					return string.Join(",", field.Content.Children.OfType<ThemedNumberEdit>().Select(w => w.ActuallNumberEdit.Text).ToArray());
				},
				new List<ValueMap>()
				{
					{ "0.1,0.2,0.3,0.4", "0.1,0.2,0.3,0.4" },
					{ "1,2,3,4", "1,2,3,4" },
					{ ",2,,4", "0,2,0,4" }, // Empty components should revert to 0s
					{ "x,2,y,4", "0,2,0,4" }, // Non-numeric components should revert to 0s
					{ ",2,", "0,0,0,0" }, // Non-vector4 csv should revert to Vector4.Zero
				});
		}

		[Test, Ignore("Not Implemented")]
		public void ListFieldTest()
		{
			Assert.Fail();
		}

		[Test, ChildProcessTest]
		public async Task ExtruderOffsetFieldTest()
		{
			var theme = MatterHackers.MatterControl.AppContext.Theme;

			var printer = new PrinterConfig(new PrinterSettings());
			var testField = new ExtruderOffsetField(printer,
				new SettingsContext(printer, null, NamedSettingsLayers.All),
				theme);

			await ValidateAgainstValueMap(
				testField,
				theme,
				(field) =>
				{
					return string.Join("x", field.Content.Descendants<ThemedNumberEdit>().Select(w => w.ActuallNumberEdit.Text).ToArray());
				},
				new List<ValueMap>()
				{
					{ "0x0x0", "0x0x0" },
					{ "0x0", "0x0x0" }, // we store 3 offsets now, when we see 2 we should make 3
					// {"", "0x0x0"}, // no values should become 0s
				});
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
			var perItemDelay = investigateDebugTests ? 1000 : 0;

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

﻿/*
Copyright (c) 2017, John Lewin
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
using System.Windows.Forms;
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
		[Test]
		public Task TestExistsForEachUIFieldType()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

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
					"Test for UIField missing - not yet created or typo'd - Expected: " + expectedTestName);
			}

			return Task.CompletedTask;
		}

		[Test]
		public async Task DoubleFieldTest()
		{
			await ValidateAgainstValueMap<DoubleField>(
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
			await ValidateAgainstValueMap<PositiveDoubleField>(
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
			await ValidateAgainstValueMap<IntField>(
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
			await ValidateAgainstValueMap<DoubleOrPercentField>(
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
			await ValidateAgainstValueMap<IntOrMmField>(
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

		[Test, Ignore("Not Implemented")]
		public void CheckboxFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void ToggleboxFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void MultilineStringFieldTest()
		{
			Assert.Fail();
		}

		[Test, RunInApplicationDomain]
		public async Task ComPortFieldTest()
		{
			FrostedSerialPort.MockPortsForTest = true;
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var field = new ComPortField(new PrinterConfig(false, PrinterSettings.Empty));

			await ValidateAgainstValueMap(
				field,
				(f) => (f.Content as DropDownList).SelectedLabel,
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
		public void Vector2FieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void BoundDoubleFieldTest()
		{
			//var field = new BoundDoubleField();
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
		/// Take a Type, a delegate to resolve the UI widget value and a map of input->expected values and validates the results for a given field
		/// </summary>
		/// <param name="collectValueFromWidget">A delegate to resolve the currently displayed widget value</param>
		/// <param name="valuesMap">A map of input to expected values</param>
		/// <returns></returns>
		public static Task ValidateAgainstValueMap<T>(Func<UIField, string> collectValueFromWidget, IEnumerable<ValueMap> valuesMap) where T : UIField, new()
		{
			var field = new T();
			return ValidateAgainstValueMap(new T(), collectValueFromWidget, valuesMap);
		}

		/// <summary>
		/// Take a UIField, a delegate to resolve the UI widget value and a map of input->expected values and validates the results for a given field
		/// </summary>
		/// <param name="field"></param>
		/// <param name="collectValueFromWidget">A delegate to resolve the currently displayed widget value</param>
		/// <param name="valuesMap">A map of input to expected values</param>
		/// <returns></returns>
		public static Task ValidateAgainstValueMap(UIField field, Func<UIField, string> collectValueFromWidget, IEnumerable<ValueMap> valuesMap)
		{
			// *************** Enable to investigate/debug/develop new/existing tests ************************
			bool investigateDebugTests = false;
			var perItemDelay = (investigateDebugTests) ? 500 : 0;

			var testsWindow = new UIFieldTestWindow(400, 200, field);

			return testsWindow.RunTest((testRunner) =>
			{
				var primaryFieldWidget = field.Content as MHNumberEdit;

				foreach (var item in valuesMap)
				{
					testsWindow.SetAndValidateValues(item.ExpectedValue, item.InputValue, collectValueFromWidget, perItemDelay);
				}

				return Task.CompletedTask;
			}, 30);

		}
	}
}

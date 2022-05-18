/*
Copyright (c) 2022, Lars Brubaker
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;
using NUnit.Framework;
using TestInvoker;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), Parallelizable(ParallelScope.Children)]
	public class PartPreviewTests
	{
		[Test, ChildProcessTest]
		public async Task CopyButtonMakesCopyOfPart()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				testRunner.AddItemToBed();

				// Get View3DWidget
				var view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.WaitForName("Calibration - Box.stl");
				Assert.AreEqual(1, scene.Children.Count, "Should have 1 part before copy");

				// Select scene object
				testRunner.Select3DPart("Calibration - Box.stl")
					// Click Copy button and count Scene.Children
					.ClickByName("Duplicate Button")
					.Assert(() => scene.Children.Count == 2, "Should have 2 parts after copy");

				// Click Copy button a second time and count Scene.Children
				testRunner.ClickByName("Duplicate Button");
				testRunner.Assert(() => scene.Children.Count == 3, "Should have 3 parts after 2nd copy");

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}

		[Test, ChildProcessTest]
		public async Task AddMultiplePartsMultipleTimes()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				var parts = new[]
				{
					"Row Item Cone",
					"Row Item Sphere",
					"Row Item Torus"
				};
				testRunner.AddPrimitivePartsToBed(parts, multiSelect: true);

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.WaitForName("Selection");
				Assert.AreEqual(1, scene.Children.Count, $"Should have 1 scene item after first AddToBed");

				testRunner.ClickByName("Print Library Overflow Menu");
				testRunner.ClickByName("Add to Bed Menu Item");
				testRunner.WaitForName("Selection");
				Assert.AreEqual(parts.Length + 1, scene.Children.Count, $"Should have {parts.Length + 1} scene items after second AddToBed");

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}

		[Test, ChildProcessTest]
		public async Task AddingImageConverterWorks()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				testRunner.AddItemToBed("Primitives Row Item Collection", "Row Item Image Converter");

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.Assert(() => scene.Children.Count == 1, $"Should have 1 scene item after first AddToBed")
					.Assert(() => scene.Descendants().Where(i => i is ImageObject3D).Any(), $"Should have 1 scene item after first AddToBed");

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}

		// NOTE: On GLFW, this test appears to fail due to the (lack of) behavior in PressModifierKeys.
		[Test, ChildProcessTest]
		public static async Task ControlClickInDesignTreeView()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				var parts = new[]
				{
					"Row Item Cube",
					"Row Item Half Cylinder",
					"Row Item Half Wedge",
					"Row Item Pyramid"
				};
				testRunner.AddPrimitivePartsToBed(parts, multiSelect: false);

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;
				var designTree = testRunner.GetWidgetByName("DesignTree", out _, 3) as TreeView;
				Assert.AreEqual(scene.Children.Count, FetchTreeNodes().Count, "Scene part count should equal tree node count");

				// Open up some room in the design tree view panel for adding group and selection nodes.
				var splitter = designTree.Parents<Splitter>().First();
				var splitterBar = splitter.Children.Where(child => child.GetType().Name == "SplitterBar").First();
				var treeNodes = FetchTreeNodes();
				var cubeNode = treeNodes.Where(node => ((IObject3D)node.Tag).Name == "Cube").Single();
				var expandBy = cubeNode.Size.Y * 4d;
				testRunner.DragWidget(splitterBar, new Point2D(0, expandBy)).Drop();

				//===========================================================================================//
				// Verify control-click isn't broken in library view.
				var moreParts = new[]
				{
					"Row Item Sphere",
					"Row Item Wedge"
				};
				testRunner.AddPrimitivePartsToBed(moreParts, multiSelect: true);
				var partCount = parts.Length + moreParts.Length;

				Assert.IsTrue(scene.Children.Any(child => child is SelectionGroupObject3D), "Scene should have a selection child");
				treeNodes = FetchTreeNodes();
				Assert.IsFalse(treeNodes.Where(node => node.Tag is SelectionGroupObject3D).Any());
				Assert.AreEqual(treeNodes.Count, partCount, "Design tree should show all parts");
				Assert.AreEqual(
					scene.Children.Sum(child =>
					{
						if (child is SelectionGroupObject3D selection)
						{
							return selection.Children.Count;
						}
						return 1;
					}),
					treeNodes.Count,
					"Number of parts in scene should equal number of nodes in design view");


				//===========================================================================================//
				// Verify rectangle drag select on bed creates a selection group in scene only.
				//
				// Rotate bed to top-down view so it's easier to select parts.
				var top = Matrix4X4.LookAt(Vector3.Zero, new Vector3(0, 0, -1), new Vector3(0, 1, 0));
				view3D.TrackballTumbleWidget.AnimateRotation(top);

				testRunner.Delay()
					.ClickByName("Pyramid")
					.Delay()
					.RectangleSelectParts(view3D.Object3DControlLayer, new[] { "Cube", "Pyramid" })
					.Delay();

				Assert.AreEqual(partCount - 3, scene.Children.Count, "Scene should have {0} children after drag rectangle select", partCount - 3);
				Assert.IsTrue(scene.Children.Any(child => child is SelectionGroupObject3D), "Scene should have a selection child after drag rectangle select");
				Assert.AreEqual(4, scene.SelectedItem.Children.Count, "4 parts should be selected");
				Assert.IsTrue(
					new HashSet<string>(scene.SelectedItem.Children.Select(child => child.Name)).SetEquals(new[] { "Cube", "Half Wedge", "Half Cylinder", "Pyramid" }),
					"Cube, Half Cylinder, Half Wedge, Pyramid should be selected");
				treeNodes = FetchTreeNodes();
				Assert.IsFalse(treeNodes.Where(node => node.Tag is SelectionGroupObject3D).Any());
				Assert.AreEqual(
					scene.Children.Sum(child =>
					{
						if (child is SelectionGroupObject3D selection)
						{
							return selection.Children.Count;
						}
						return 1;
					}),
					treeNodes.Count,
					"Number of parts in scene should equal number of nodes in design view afte drag rectangle select");

				//===========================================================================================//
				// Verify shift-clicking on parts on bed creates a selection group.
				testRunner
					.ClickByName("Sphere")
					.ClickByName("Half Cylinder")
					.PressModifierKeys(AutomationRunner.ModifierKeys.Shift)
					.ClickByName("Pyramid")
					.ReleaseModifierKeys(AutomationRunner.ModifierKeys.Shift);
				Assert.AreEqual(partCount - 1, scene.Children.Count, "Should have {0} children after selection", partCount - 1);
				Assert.IsTrue(scene.Children.Any(child => child is SelectionGroupObject3D), "Selection group should be child of scene");
				Assert.IsFalse(scene.Children.Any(child => child.Name == "Half Cylinder" || child.Name == "Pyramid"), "Half Cylinder and Pyramid should be removed as direct children of scene");
				Assert.IsNull(designTree.SelectedNode, "Design tree shouldn't have a selected node when multiple parts are selected");

				//===========================================================================================//
				// Verify grouping parts creates a group.
				testRunner.ClickByName("Group Button");
				Assert.AreEqual(partCount - 1, scene.Children.Count, "Should have {0} parts after group", partCount - 1);
				Assert.IsInstanceOf<GroupHolesAppliedObject3D>(scene.SelectedItem, "Scene selection should be group");
				Assert.IsInstanceOf<GroupHolesAppliedObject3D>(designTree.SelectedNode.Tag, "Group should be selected in design tree");
				Assert.AreSame(scene.SelectedItem, designTree.SelectedNode.Tag, "Same group object should be selected in scene and design tree");

				treeNodes = FetchTreeNodes();
				Assert.AreEqual(scene.Children.Count, treeNodes.Count, "Scene part count should equal tree node count after group");
				Assert.IsTrue(treeNodes.Any(node => node.Tag is GroupHolesAppliedObject3D), "Design tree should have node for group");
				Assert.AreSame(designTree.SelectedNode.Tag, treeNodes.Single(node => node.Tag is GroupHolesAppliedObject3D).Tag, "Selected node in design tree should be group node");

				var groupNode = treeNodes.Where(node => node.Tag is GroupHolesAppliedObject3D).Single();
				Assert.AreEqual(2, groupNode.Nodes.Count, "Group should have 2 parts");
				Assert.IsTrue(
					new HashSet<string>(groupNode.Nodes.Select(node => ((IObject3D)node.Tag).Name)).SetEquals(new[] {"Half Cylinder", "Pyramid"}),
					"Half Cylinder and Pyramind should be grouped");

				var singleItemNodes = treeNodes
					.Where(node => !(node.Tag is GroupHolesAppliedObject3D))
					.Where(node => !(node.Tag is SelectionGroupObject3D))
					.ToList();
				var singleItemNames = new HashSet<string>(singleItemNodes.Select(item => ((IObject3D)item.Tag).Name));

				Assert.AreEqual(partCount - 2, singleItemNodes.Count, "There should be {0} single item nodes in the design tree", partCount - 2);
				Assert.IsTrue(singleItemNames.SetEquals(new[] {"Cube", "Half Wedge", "Sphere", "Wedge"}), "Cube, Half Wedge, Sphere, Wedge should be single items");

				//===========================================================================================//
				// Verify using the design tree to create a selection group.
				var halfWedgeNode = treeNodes.Where(node => ((IObject3D)node.Tag).Name == "Half Wedge").Single();
				var sphereNode = treeNodes.Where(node => ((IObject3D)node.Tag).Name == "Sphere").Single();
				testRunner.ClickWidget(halfWedgeNode)
					.PressModifierKeys(AutomationRunner.ModifierKeys.Control)
					.ClickWidget(sphereNode)
					.ReleaseModifierKeys(AutomationRunner.ModifierKeys.Control);
				Assert.AreEqual(partCount - 2, scene.Children.Count, "Should have {0} parts after selection", partCount - 2);
				Assert.IsNull(designTree.SelectedNode, "Design tree shouldn't have a selected node after creating selection in design tree");

				//===========================================================================================//
				// Verify control-clicking a part in the group does not get added to the selection group. Only top-level nodes can be
				// selected.
				treeNodes = FetchTreeNodes();
				groupNode = treeNodes.Where(node => node.Tag is GroupHolesAppliedObject3D).Single();
				testRunner.PressModifierKeys(AutomationRunner.ModifierKeys.Control)
					.ClickWidget(groupNode.Nodes.Last())
					.ReleaseModifierKeys(AutomationRunner.ModifierKeys.Control);
				Assert.AreEqual(
					scene.Children.Sum(child =>
					{
						if (child is SelectionGroupObject3D selection)
						{
							return selection.Children.Count;
						}
						return 1;
					}),
					treeNodes.Count,
					"Scene part count should equal design tree node count after control-click on group child");
				Assert.IsInstanceOf<SelectionGroupObject3D>(scene.SelectedItem, "Selection shouldn't change after control-click on group child");
				Assert.AreEqual(2, scene.SelectedItem.Children.Count, "Selection should have 2 parts after control-click on group child");

				//===========================================================================================//
				// Verify adding group to selection.
				testRunner.PressModifierKeys(AutomationRunner.ModifierKeys.Control)
					.ClickWidget(groupNode.TitleBar)
					.ReleaseModifierKeys(AutomationRunner.ModifierKeys.Control);
				Assert.AreEqual(partCount - 3, scene.Children.Count, "Scene should have {0} children after control-clicking group", partCount - 3);
				Assert.IsInstanceOf<SelectionGroupObject3D>(scene.SelectedItem, "Selected item should be a selection group after control-clicking on group");
				Assert.AreEqual(3, scene.SelectedItem.Children.Count, "Selection should have 3 items after control-clicking on group");
				Assert.IsTrue(
					new HashSet<string>(scene.SelectedItem.Children.Select(child => child.Name)).SetEquals(new[] {"Half Wedge", "Sphere", "Half Cylinder, Pyramid" }),
					"Selection should have Group, Half Wedge, Sphere");

				//===========================================================================================//
				// Verify control-clicking on a part in the selection removes it from the selection.
				treeNodes = FetchTreeNodes();
				halfWedgeNode = treeNodes.Where(node => ((IObject3D)node.Tag).Name == "Half Wedge").Single();

				testRunner.PressModifierKeys(AutomationRunner.ModifierKeys.Control)
					.ClickWidget(halfWedgeNode)
					.ReleaseModifierKeys(AutomationRunner.ModifierKeys.Control);

				Assert.IsInstanceOf<SelectionGroupObject3D>(scene.SelectedItem, "Selection group should exist after removing a child");
				Assert.AreEqual(2, scene.SelectedItem.Children.Count, "Selection should have 2 parts after removing a child");
				Assert.IsTrue(
					new HashSet<string>(scene.SelectedItem.Children.Select(child => child.Name)).SetEquals(new[] { "Half Cylinder, Pyramid", "Sphere"}),
					"Group and Sphere should be in selection after removing a child");

				//===========================================================================================//
				// Verify control-clicking on second-to-last part in the selection removes it from the selection
				// and destroys selection group.
				treeNodes = FetchTreeNodes();
				groupNode = treeNodes.Where(node => node.Tag is GroupHolesAppliedObject3D).Single();
				sphereNode = treeNodes.Where(node => ((IObject3D)node.Tag).Name == "Sphere").Single();

				testRunner.PressModifierKeys(AutomationRunner.ModifierKeys.Control)
					.ClickWidget(sphereNode)
					.ReleaseModifierKeys(AutomationRunner.ModifierKeys.Control);

				treeNodes = FetchTreeNodes();
				Assert.AreEqual(scene.Children.Count, treeNodes.Count, "Scene part count should equal design tree node count after removing penultimate child");
				Assert.IsNotInstanceOf<SelectionGroupObject3D>(scene.SelectedItem, "Selection group shouldn't exist after removing penultimate child");
				Assert.AreSame(groupNode.Tag, scene.SelectedItem, "Selection should be group after removing penultimate child");

				//===========================================================================================//
				// Verify control-clicking on a part in the group that's part of the selection doesn't change the selection.
				halfWedgeNode = treeNodes.Where(node => ((IObject3D)node.Tag).Name == "Half Wedge").Single();
				testRunner.PressModifierKeys(AutomationRunner.ModifierKeys.Control)
					.ClickWidget(halfWedgeNode)
					.ReleaseModifierKeys(AutomationRunner.ModifierKeys.Control);

				treeNodes = FetchTreeNodes();
				sphereNode = treeNodes.Where(node => ((IObject3D)node.Tag).Name == "Sphere").Single();
				testRunner.PressModifierKeys(AutomationRunner.ModifierKeys.Control)
					.ClickWidget(sphereNode)
					.ReleaseModifierKeys(AutomationRunner.ModifierKeys.Control);

				treeNodes = FetchTreeNodes();
				groupNode = treeNodes.Where(node => node.Tag is GroupHolesAppliedObject3D).Single();
				testRunner.PressModifierKeys(AutomationRunner.ModifierKeys.Control)
					.ClickWidget(groupNode.Nodes.Last())
					.ReleaseModifierKeys(AutomationRunner.ModifierKeys.Control);

				Assert.IsInstanceOf<SelectionGroupObject3D>(scene.SelectedItem, "Selection shouldn't change after control-click on selection group child");
				Assert.AreEqual(3, scene.SelectedItem.Children.Count, "Selection should have 3 parts after control-click on selection group child");

				//===========================================================================================//
				// Verify clicking on a top-level node that's not in the selection group unselects all the parts in the group
				// and selects the part associated with the clicked node.
				treeNodes = FetchTreeNodes();
				var wedgeNode = treeNodes.Where(node => ((IObject3D)node.Tag).Name == "Wedge").Single();
				testRunner.ClickWidget(wedgeNode);
				Assert.AreEqual(partCount - 1, scene.Children.Count, "Should be {0} parts in the scene after selecting wedge", partCount - 1);
				Assert.AreSame(scene.SelectedItem, wedgeNode.Tag, "Wedge should be selected");
				Assert.IsFalse(scene.Children.Any(child => child is SelectionGroupObject3D), "Selection group should go away when another part is selected");
				Assert.AreSame(scene.SelectedItem, designTree.SelectedNode.Tag, "The same part should be selected in the scene and design tree");

				treeNodes = FetchTreeNodes();
				wedgeNode = treeNodes.Where(node => ((IObject3D)node.Tag).Name == "Wedge").Single();
				Assert.AreSame(designTree.SelectedNode, wedgeNode, "Wedge node should be selected in design tree");
				Assert.IsFalse(treeNodes.Any(node => node.Tag is SelectionGroupObject3D), "Selection group shouldn't exist in design tree after selecting wedge");

				//===========================================================================================//
				// Verify that shift-clicking a part on the bed makes a selection group with a part that's been selected through
				// the design tree.
				testRunner.PressModifierKeys(AutomationRunner.ModifierKeys.Shift)
					.ClickByName("Half Wedge")
					.ReleaseModifierKeys(AutomationRunner.ModifierKeys.Shift);
				Assert.AreEqual(partCount - 2, scene.Children.Count, "Scene should have {0} children after selecting half wedge", partCount - 2);
				Assert.IsNull(designTree.SelectedNode, "Selected node in design tree should be null after selecting half wedge");
				Assert.IsInstanceOf<SelectionGroupObject3D>(scene.SelectedItem, "Should have a selection group after selecting half wedge");
				Assert.IsTrue(
					new HashSet<string>(scene.SelectedItem.Children.Select(child => child.Name)).SetEquals(new [] {"Wedge", "Half Wedge"}),
					"Half Wedge and Wedge should be in selection");

				//===========================================================================================//
				// Verify that control-click on a top-level part adds to an existing selection.
				treeNodes = FetchTreeNodes();
				sphereNode = treeNodes.Where(node => ((IObject3D)node.Tag).Name == "Sphere").Single();
				testRunner.PressModifierKeys(AutomationRunner.ModifierKeys.Control)
					.ClickWidget(sphereNode)
					.ReleaseModifierKeys(AutomationRunner.ModifierKeys.Control);
				Assert.AreEqual(partCount - 3, scene.Children.Count, "Scene should have {0} children after selecting sphere", partCount - 3);
				Assert.IsInstanceOf<SelectionGroupObject3D>(scene.SelectedItem, "Selection in scene should be selection group after adding sphere");
				Assert.IsTrue(
					new HashSet<string>(scene.SelectedItem.Children.Select(child => child.Name)).SetEquals(new [] {"Wedge", "Half Wedge", "Sphere"}),
					"Half Wedge, Sphere, Wedge should be in selection");

				//===========================================================================================//
				// Done

				return Task.CompletedTask;

				// The nodes in the design tree are regenerated after certain events and must
				// be fetched anew.
				List<TreeNode> FetchTreeNodes() =>
					designTree.Children
						.Where(child => child is ScrollingArea)
						.First()
						.Children
						.Where(child => child is FlowLayoutWidget)
						.First()
						.Children
						.Select(child => (TreeNode)child)
						.ToList();
			}, overrideWidth: 1300, maxTimeToRun: 110);
		}

		[Test, ChildProcessTest]
		public async Task DesignTabFileOperations()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab(false);

				// Get View3DWidget
				var view3D = testRunner.GetWidgetByName("View3DWidget", out SystemWindow systemWindow, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				var tempFilaname = "Temp Test Save.mcx";
				var tempFullPath = Path.Combine(ApplicationDataStorage.Instance.DownloadsDirectory, tempFilaname);

				// delete the temp file if it exists in the Downloads folder
				void DeleteTempFile()
				{
					if (File.Exists(tempFullPath))
					{
						File.Delete(tempFullPath);
					}
				}

				DeleteTempFile();

				testRunner.Assert(() => scene.Children.Count == 1, "Should have 1 part (the phil)")
					// Make sure the tab is named 'New Design'
					.Assert(() => systemWindow.GetVisibleWigetWithText("New Design") != null, "Must have New Design")
					// add a new part to the bed
					.AddItemToBed()
					// Click the save button
					.ClickByName("Save")
					// Cancle the save as
					.ClickByName("Cancel Wizard Button")
					// Make sure the tab is named 'New Design'
					.Assert(() => systemWindow.GetVisibleWigetWithText("New Design") != null, "Have a new design tab")
					// Click the close tab button
					.ClickByName("Close Tab Button")
					// Select Cancel
					.ClickByName("Cancel Button")
					// Make sure the tab is named 'New Design'
					.Assert(() => systemWindow.GetVisibleWigetWithText("New Design") != null, "Still have design tab")
					// Click the close tab button
					.ClickByName("Close Tab Button")
					// Select 'Save'
					.ClickByName("Yes Button")
					// Cancel the 'Save As'
					.ClickByName("Cancel Wizard Button")
					// Make sure the window is still open and the tab is named 'New Design'
					.Assert(() => systemWindow.GetVisibleWigetWithText("New Design") != null, "still have desin tab")
					// Click the save button
					.ClickByName("Save")
					// Save a temp file to the downloads folder
					.DoubleClickByName("Computer Row Item Collection")
					.DoubleClickByName("Downloads Row Item Collection")
					.ClickByName("Design Name Edit Field")
					.Type(tempFilaname)
					.ClickByName("Accept Button")
					// Verify it is there
					.Assert(() => File.Exists(tempFullPath), "Must save the file")
					// And that the tab got the name
					.Assert(() => systemWindow.GetVisibleWigetWithText(tempFilaname) != null, "Tab was renamed")
					// and the tooltip is right
					.Assert(() => systemWindow.GetVisibleWigetWithText(tempFilaname).ToolTipText == tempFullPath, "Correct tool tip name")
					// Add a part to the bed
					.AddItemToBed()
					// Click the close tab button (we have an edit so it should show the save request)
					.ClickByName("Close Tab Button")
					// Click the 'Cancel'
					.ClickByName("Cancel Button")
					// Click the 'Save' button
					.ClickByName("Save")
					// Click the close button (now we have no edit it should cancel without request)
					.ClickByName("Close Tab Button");

				// Verify the tab closes without requesting save
				testRunner.Assert(() => systemWindow.GetVisibleWigetWithText(tempFilaname) == null, "The tab should have closed");

				// delete the temp file if it exists in the Downloads folder
				DeleteTempFile();

				return Task.CompletedTask;
			}, maxTimeToRun: 60);
		}

		[Test, ChildProcessTest]
		public async Task GroupAndUngroup()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				testRunner.AddItemToBed();

				// Get View3DWidget and count Scene.Children before Copy button is clicked
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				// Assert expected start count
				Assert.AreEqual(1, scene.Children.Count, "Should have one part before copy");

				// Select scene object
				testRunner.Select3DPart("Calibration - Box.stl");

				for (int i = 2; i <= 6; i++)
				{
					testRunner.ClickByName("Duplicate Button")
						.Assert(() => scene.Children.Count == i, $"Should have {i} parts after copy");
				}

				// Get MeshGroupCount before Group is clicked
				Assert.AreEqual(6, scene.Children.Count, "Scene should have 6 parts after copy loop");

				// Duplicate button moved to new container - move focus back to View3DWidget so CTRL-A below is seen by expected control
				testRunner.Select3DPart("Calibration - Box.stl")
					// select all
					.Type("^a")
					.ClickByName("Group Button")
					.Assert(() => scene.Children.Count == 1, $"Should have 1 parts after group");

				testRunner.ClickByName("Ungroup Button")
					.Assert(() => scene.Children.Count == 6, $"Should have 6 parts after ungroup");

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		[Test, ChildProcessTest]
		public async Task RemoveButtonRemovesParts()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				testRunner.AddItemToBed();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.Select3DPart("Calibration - Box.stl");

				Assert.AreEqual(1, scene.Children.Count, "There should be 1 part on the bed after AddDefaultFileToBedplate()");

				// Add 5 items
				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName("Duplicate Button")
						.Delay(.5);
				}

				Assert.AreEqual(6, scene.Children.Count, "There should be 6 parts on the bed after the copy loop");

				// Remove an item
				testRunner.ClickByName("Remove Button");

				// Confirm
				Assert.AreEqual(5, scene.Children.Count, "There should be 5 parts on the bed after remove");

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task SaveAsToQueue()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				testRunner.AddItemToBed();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;

				testRunner.Select3DPart("Calibration - Box.stl");

				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.SaveBedplateToFolder("Test PartA.mcx", "Queue Row Item Collection")
					.NavigateToLibraryHome()
					.NavigateToFolder("Queue Row Item Collection");

				Assert.IsTrue(testRunner.WaitForName("Row Item Test PartA.mcx"), "The part we added should be in the library");
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by one after Save operation");

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task SaveAsToLocalLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				testRunner.AddItemToBed();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;

				testRunner.Select3DPart("Calibration - Box.stl");

				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.SaveBedplateToFolder("Test PartB", "Local Library Row Item Collection")
					.NavigateToLibraryHome()
					.NavigateToFolder("Local Library Row Item Collection");

				Assert.IsTrue(testRunner.WaitForName("Row Item Test PartB"), "The part we added should be in the library");

				return Task.CompletedTask;
			});
		}
	}

	public static class WidgetExtensions
    {
		/// <summary>
		/// Search the widget stack for a widget that is both visible on screen and has it's text set to the visibleText string
		/// </summary>
		/// <param name="widget">The root widget to search</param>
		/// <param name="">the name to search for</param>
		/// <returns></returns>
		public static GuiWidget GetVisibleWigetWithText(this GuiWidget widget, string visibleText)
        {
			if (widget.ActuallyVisibleOnScreen())
			{
				if (widget.Text == visibleText)
				{
					return widget;
				}

				foreach(var child in widget.Children)
                {
					var childWithText = GetVisibleWigetWithText(child, visibleText);
					if (childWithText != null)
                    {
						return childWithText;
                    }
                }
			}

			return null;
        }
    }
}

/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Ignore("Not clear if these should be ported"), Category("MatterControl.UI.Automation"), Category("MatterControl.Automation"), RunInApplicationDomain]
	public class PrintQueueUncertain
	{
		/// <summary>
		/// *Tests:
		/// *1. When the remove button on a queue item is clicked the queue tab count decreases by one 
		/// *2. When the remove button on a queue item is clicked the item is removed
		/// *3. When the View button on a queue item is clicked the part preview window is opened
		/// </summary>
		/// <returns></returns>
		[Test, Apartment(ApartmentState.STA)]
		public async Task ClickQueueRowItemViewAndRemove()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.Delay(2);

				Assert.AreEqual(4, QueueData.Instance.ItemCount, "Queue should initially have four items");
				Assert.IsTrue(testRunner.WaitForName("Queue Item Batman"));
				Assert.IsTrue(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2"));

				testRunner.ClickByName("Queue Item Batman");
				testRunner.ClickByName("Queue Item Batman Remove");
				testRunner.Delay(2);

				Assert.AreEqual(3, QueueData.Instance.ItemCount, "Batman item removed");
				Assert.IsFalse(testRunner.NameExists("Queue Item Batman", .2), "Batman item removed");

				Assert.IsFalse(testRunner.NameExists("Queue Item 2013-01-25_Mouthpiece_v2 Part Preview", .2), "Mouthpiece Part Preview should not initially be visible");
				testRunner.ClickByName("Queue Item 2013-01-25_Mouthpiece_v2");
				testRunner.Delay(2);
				testRunner.ClickByName("Queue Item 2013-01-25_Mouthpiece_v2 View");

				Assert.IsTrue(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2 Part Preview"), "The Mouthpiece Part Preview should appear after the view button is clicked");

				return Task.CompletedTask;
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items, overrideWidth: 600);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.MatterControl.PrintQueue;

using NUnit.Framework;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.Testing
{
    [TestFixture]
    public class QueueTests
    {
        [Test]
        public void ConfirmItemIsAddedToQueue()
        {
            string testFilePath = @"C:\Users\Greg\Desktop\MatterSliceTestItems\Batman.stl";

            QueueData.Instance.RemoveAll();
            QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem("name", testFilePath)));

            // Instance count is as expected after single add
            Assert.IsTrue(QueueData.Instance.Count == 1);

            var printItem = QueueData.Instance.GetPrintItemWrapper(0);
            bool hasCorrectName = printItem.Name == "name";
            
            // Instance name is as expected
            Assert.IsTrue(hasCorrectName);

            //Instance path is as expected
            Assert.IsTrue(printItem.FileLocation == testFilePath);

            QueueData.Instance.RemoveAll();

            //Instance count is as expected after queue is cleared 
            Assert.IsTrue(QueueData.Instance.Count == 0);
        }

        [Test]
        public void ConfirmMultipleItemsAreAddedToQueue()
        {
            //TODO: Need to find a permanent location for test items so that tests will run correctly 
            string firstQueueItemToBeAdded = @"C:\Users\Greg\Desktop\MatterSliceTestItems\Batman.stl";
            string secondQueueItemToBeAdded = @"C:\Users\Greg\Desktop\MatterSliceTestItems\chichen-itza_pyramid.stl";                                                         

            QueueData.Instance.RemoveAll();

            QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem("nameOne", firstQueueItemToBeAdded)));
            QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem("nameTwo", secondQueueItemToBeAdded)));

            //Instance count is as expected after single add
            Assert.IsTrue(QueueData.Instance.Count == 2);


            var firstPrintItem = QueueData.Instance.GetPrintItemWrapper(0);
            bool firstItemHasCorrectName = firstPrintItem.Name == "nameOne";
            //First instance name is as expected 
            Assert.IsTrue(firstItemHasCorrectName);

            //First instance path is as expected 
            Assert.IsTrue(firstPrintItem.FileLocation == firstQueueItemToBeAdded);



            var secondPrintItem = QueueData.Instance.GetPrintItemWrapper(1);
            bool secondItemHasCorrectName = secondPrintItem.Name == "nameTwo";

            //Second instance name is as expected
            Assert.IsTrue(secondItemHasCorrectName);

            //Second instance path is as expected 
            Assert.IsTrue(secondPrintItem.FileLocation == secondQueueItemToBeAdded);
                                

        }   

        public static void RunAllQueueTests()
        {
            (new QueueTests()).ConfirmItemIsAddedToQueue();
            (new QueueTests()).ConfirmMultipleItemsAreAddedToQueue();
        }

    }
}

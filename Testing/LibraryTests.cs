using MatterHackers.MatterControl.PrintLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl.Testing
{
    [TestFixture]
    public class LibraryTests
    {
        [Test]
        public void ConfirmItemIsAddedToLibrary()
        {
            string pathToLibraryItem = @"C:\Users\Greg\Desktop\MatterSliceTestItems\Batman.stl";

            LibraryData.Instance.AddItem(new PrintItemWrapper(new PrintItem("NameOne", pathToLibraryItem)));

            Assert.IsTrue(LibraryData.Instance.Count == 1);

            var libraryItem = LibraryData.Instance.GetPrintItemWrapper(0);
            bool hasCorrectName = libraryItem.Name == "NameOne";

            Assert.IsTrue(hasCorrectName);

            Assert.IsTrue(libraryItem.FileLocation == pathToLibraryItem);

            LibraryData.Instance.RemoveItem(libraryItem);

            Assert.IsTrue(LibraryData.Instance.Count == 0);
        }


        public static void RunLibraryTests()
        {
            (new LibraryTests()).ConfirmItemIsAddedToLibrary();

        }
    }
}

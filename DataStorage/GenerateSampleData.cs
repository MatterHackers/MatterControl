using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.DataStorage
{
    class GenerateSampleData
    {
        public GenerateSampleData()
        {
            AddPrinters();
        }

        void AddPrinters()
        {
            for (int index = 1; index <= 5; index++)
            {
                Printer printer = new Printer();
                printer.ComPort = string.Format("COM{0}", index);
                printer.BaudRate = "250000";
                printer.Name = string.Format("Printer {0}", index);
                Datastore.Instance.dbSQLite.Insert(printer);
            }
        }
    }
}

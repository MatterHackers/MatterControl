using MatterHackers.MatterControl.PluginSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.Queue.OptionsMenu
{
    public class ExportGcodePlugin : MatterControlPlugin
    {

        public virtual string getButtonText()
        {
            return "";
        }

        public virtual string getFileExtension()
        {
            return "";
        }

        public virtual string getExtensionFilter()
        {
            return "";
        }

        public virtual void generate(string gcodeInputPath, string x3gOutputPath)
        {

        }

    }
}

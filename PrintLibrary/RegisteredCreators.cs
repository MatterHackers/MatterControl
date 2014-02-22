using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.PrintLibrary
{
    public class CreatorInformation
    {
        public EventHandler functionToLaunchCreator;
        public string iconPath;
        public string description;

        public CreatorInformation(EventHandler functionToLaunchCreator, string iconPath, string description)
        {
            this.functionToLaunchCreator = functionToLaunchCreator;
            this.iconPath = iconPath;
            this.description = description;
        }
    }

    public class RegisteredCreators
    {
        static RegisteredCreators instance = null;
        public static RegisteredCreators Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RegisteredCreators();
                }

                return instance;
            }
        }

        public List<CreatorInformation> Creators = new List<CreatorInformation>();

        private RegisteredCreators()
        {
        }

        public void RegisterLaunchFunction(CreatorInformation creatorInformation)
        {
            Creators.Add(creatorInformation);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl
{
    public class UserSettingsFields
    {
        List<string> acceptableTrueFalseValues = new List<string>() { "true", "false" };

        public bool IsSimpleMode
        {
            get
            {
                string currentValue = UserSettings.Instance.get("IsSimpleMode");
                if (acceptableTrueFalseValues.IndexOf(currentValue) == -1)
                {
                    currentValue = "true";
                    UserSettings.Instance.set("IsSimpleMode", currentValue);
                }

                if(currentValue == "true")
                {
                    return true;
                }

                return false;
            }

            set
            {
                if (value)
                {
                    UserSettings.Instance.set("IsSimpleMode", "true");
                }
                else
                {
                    UserSettings.Instance.set("IsSimpleMode", "false");
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl
{
    public class JsonResponseDictionary : Dictionary<string, string>
    {
        public string get(string key)
        {
            string result;
            if (this.ContainsKey(key))
            {
                result = this[key];
                
            }
            else
            {
                result = null;
            }
            return result;
        }

        public bool GetInt(string key, out int result)
        {            
            if (this.get(key) != null)
            {
                bool isInt = Int32.TryParse(this.get(key), out result);
                if (isInt)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                result = 0;
                return false;
            }
        }

    }
}

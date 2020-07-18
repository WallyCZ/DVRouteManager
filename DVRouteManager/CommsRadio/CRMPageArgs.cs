using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVRouteManager.CommsRadio
{
    public class CRMPageArgs
    {
        Dictionary<string, object> arguments = new Dictionary<string, object>();

        public void AddString(string argName, string text)
        {
            arguments.Add(argName, text);
        }

        public string GetString(string argName)
        {
            object text;

            arguments.TryGetValue(argName, out text);
            return (string) text;
        }

        public void AddInteger(string argName, int number)
        {
            arguments.Add(argName, number);
        }

        public int? GetInteger(string argName)
        {
            object number;

            arguments.TryGetValue(argName, out number);
            return (int?)number;
        }

        public void AddFloat(string argName, float number)
        {
            arguments.Add(argName, number);
        }

        public float? GetFloat(string argName)
        {
            object number;

            arguments.TryGetValue(argName, out number);
            return (float?)number;
        }


    }
}

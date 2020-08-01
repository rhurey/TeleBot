using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Permissions;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace phone
{
    internal static class Settings
    {
        public static JObject SettingsObj { get; private set; }

        public static void Init(string file="settings.json")
        {
            var settings = File.ReadAllText(file);
            SettingsObj = JObject.Parse(settings);
        }
    }
}

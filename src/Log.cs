using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace phone
{
    internal static class Log
    {
        private static TextWriter logSink = Console.Out;

        internal static void SetLogLocation(TextWriter location)
        {
            logSink = location;
        }

        internal static void LogMessage(string message, params string[] args)
        {
            logSink.WriteLine($"{DateTime.Now} {message}", args);
        }
    }
}

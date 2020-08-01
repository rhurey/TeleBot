using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Intent;
using System.Runtime.CompilerServices;
using System.Reflection.Metadata;
using System.Linq;
using System.DirectoryServices.ActiveDirectory;
using System.Drawing.Imaging;

namespace phone
{
    class Program
    {
        private static Telebot bot = new Telebot();

        static async Task Main(string[] args)
        {
            Settings.Init();

            await bot.Init(Settings.SettingsObj["Program"]["comport"].Value<string>());

            bot.IncomingCall += (s, e) =>
            {
                Console.WriteLine($"Incoming call from {e}");
            };

            bool quit = false;
            while (!quit)
            {

                Console.Error.WriteLine("a: Answer, q: Quit");
                var input = Console.ReadKey();

                switch (input.KeyChar)
                {
                    case 'a':
                        Console.Error.WriteLine("Answering");
                        await bot.Answer();
                        break;
                    case 'q':
                        Console.Error.WriteLine("Quitting");
                        quit = true;
                        break;
                }
            }
        }
    }
}

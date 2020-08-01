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
    public class Telebot
    {
        private Modem m = new Modem();
        private Synthesizer synth = null;
        private IntentReco recognizer = null;

        public async Task Init(string serialPort)
        {
            await m.Init(serialPort);
            await m.EnterVoiceMode();

            m.Ringing += (s, e) =>
            {
                IncomingCall(this, e);
            };
        }

        public EventHandler<RingInfo> IncomingCall;
        
        public async Task Answer()
        {
            synth = new Synthesizer(m);
            recognizer = new IntentReco(m);
            var helloFetch = synth.GetAudio("Hello?");
            await m.PickupLine();
            await synth.Speak(await helloFetch);
            var ret = await recognizer.RecoOnce();
            Console.WriteLine($"Intent {ret.Intent}");
            bool quit = false;

            while (!quit)
            {
                switch (ret.Intent)
                {
                    case "CanITalkTo":
                        await CanTalkTo(ret);
                        quit = true;
                        break;
                    case "None":
                        // Just get some transcript.
                        await recognizer.Transcribe();
                        quit = true;
                        break;
                    default:
                        await synth.Speak("Huh, what? Can you repeat that?");
                        ret = await recognizer.RecoOnce();
                        break;
                }
            }
            await m.HangupLine();
        }

        private async Task CanTalkTo(IntentReco.IntentResult ret)
        {
            if (ret.Entities.ContainsKey("Name") || ret.Entities.ContainsKey("builtin.personName"))
            {
                var name = ret.Entities.ContainsKey("builtin.personName") ? ret.Entities["builtin.personName"] : ret.Entities["Name"];

                await synth.SpeakSSML($"<speak xmlns=\"http://www.w3.org/2001/10/synthesis\" xmlns:mstts=\"http://www.w3.org/2001/mstts\" xmlns:emo=\"http://www.w3.org/2009/10/emotionml\" version=\"1.0\" xml:lang=\"en-US\"><voice name=\"en-US-AriaNeural\"><mstts:express-as style=\"Cheerful\"><prosody rate=\"0%\" pitch=\"10%\">Alrighty,</prosody><prosody rate=\"0%\" pitch=\"5%\">I'll go get {name},</prosody><prosody rate=\"10%\" pitch=\"-14%\"> who can I say is calling?</prosody></mstts:express-as></voice></speak>");
            }
            else
            {
                await synth.Speak($"I'll look, but they may not want to talk to you. Who is this?");
            }

            var who = await recognizer.RecoOnce();
            string callerName = "";
            switch (who.Intent.ToLower())
            {
                case "mynameis":
                    if (ret.Entities.ContainsKey("Name") || ret.Entities.ContainsKey("builtin.personName"))
                    {
                        callerName = ret.Entities.ContainsKey("builtin.personName") ? ret.Entities["builtin.personName"] : ret.Entities["Name"];
                    }
                    break;
                case "none":
                    // Is the answer one word?
                    if (1 == CountWords(who.Text))
                    {
                        callerName = who.Text;
                    }
                    break;
                default:
                    await synth.Speak("Sure");
                    break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            await synth.SpeakSSML("<speak xmlns=\"http://www.w3.org/2001/10/synthesis\" xmlns:mstts=\"http://www.w3.org/2001/mstts\" xmlns:emo=\"http://www.w3.org/2009/10/emotionml\" version=\"1.0\" xml:lang=\"en-US\"><voice name=\"en-US-AriaNeural\"><mstts:express-as style=\"Empathy\"><prosody rate=\"0%\" pitch=\"0%\" volume=\"10\">oh, shoot</prosody></mstts:express-as></voice></speak>");

            await synth.SpeakSSML($"<speak xmlns=\"http://www.w3.org/2001/10/synthesis\" xmlns:mstts=\"http://www.w3.org/2001/mstts\" xmlns:emo=\"http://www.w3.org/2009/10/emotionml\" version=\"1.0\" xml:lang=\"en-US\"><voice name=\"en-US-AriaNeural\"><mstts:express-as style=\"Empathy\"><prosody rate=\"0%\" pitch=\"0%\" volume=\"100\">{callerName}, There's a little problem here.</prosody></mstts:express-as></voice></speak>");

            await recognizer.Transcribe();

            await synth.Speak("We've reached the end of the demo");
        }

        private static int CountWords(string text)
        {
            int wordCount = 0, index = 0;

            // skip whitespace until first word
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;

            while (index < text.Length)
            {
                // check if current char is part of a word
                while (index < text.Length && !char.IsWhiteSpace(text[index]))
                    index++;

                wordCount++;

                // skip whitespace until next word
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                    index++;
            }

            return wordCount;
        }
    }

}

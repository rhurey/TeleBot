using System;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

using Newtonsoft.Json.Linq;

namespace phone
{
    class Synthesizer
    {
        private readonly Modem modem;
        private readonly SpeechSynthesizer synthesizer;
        public Synthesizer(Modem modem)
        {
            this.modem = modem;
            SpeechConfig speechConfig = SpeechConfig.FromSubscription(Settings.SettingsObj["Synthesizer"]["key"].Value<string>(),
                Settings.SettingsObj["Synthesizer"]["region"].Value<string>());
            speechConfig.SpeechSynthesisLanguage = "en-us";
            speechConfig.SpeechSynthesisVoiceName = "en-US-AriaNeural";
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff8Khz8BitMonoMULaw);

            var ttsStream = AudioOutputStream.CreatePullStream();

            AudioConfig sp = AudioConfig.FromStreamOutput(ttsStream);
            this.synthesizer = new SpeechSynthesizer(speechConfig, sp);
        }
        public async Task<byte[]> GetAudio(string text)
        {
            Console.WriteLine($"Getting {text}");
            return (await synthesizer.SpeakTextAsync(text)).AudioData;
        }

        public async Task Speak(byte[] audio)
        {
            Console.WriteLine($"Speaking Audio");
            await modem.Speak(audio);
        }

        public async Task Speak(string text)
        {
            Console.WriteLine($"Speaking {text}");
            var speech = await synthesizer.SpeakTextAsync(text);
            await modem.Speak(speech.AudioData);
        }

        public async Task SpeakSSML(string text)
        {
            Console.WriteLine($"Speaking SSML{text}");

            var speech = await synthesizer.SpeakSsmlAsync(text);
            if (speech.Reason == ResultReason.Canceled)
            {
                var details = SpeechSynthesisCancellationDetails.FromResult(speech);
                Console.WriteLine($"REASON: {details.ErrorDetails}");
                return;
            }
            await modem.Speak(speech.AudioData);
        }
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Intent;
using Newtonsoft.Json.Linq;

namespace phone
{
    class IntentReco
    {
        private readonly Modem modem;
        private readonly IntentRecognizer recognizer;
        public IntentReco(Modem modem)
        {
            this.modem = modem;
            PushAudioInputStream ps = AudioInputStream.CreatePushStream(AudioStreamFormat.GetWaveFormatPCM(8000, 8, 1));
            modem.AudioReceived += (s, e) =>
            {
                if (ps != null)
                {
                    //Console.WriteLine($"Writing {e.Length} bytes " + BitConverter.ToString(e));
                    ps.Write(e);
                }
            };

            SpeechConfig intentConfig = SpeechConfig.FromSubscription(Settings.SettingsObj["IntentReco"]["key"].Value<string>(),
                Settings.SettingsObj["IntentReco"]["region"].Value<string>());
            AudioConfig ac = AudioConfig.FromStreamInput(ps);
            this.recognizer = new IntentRecognizer(intentConfig, ac);
            this.recognizer.Recognized += (s, e) =>
            {
                Console.WriteLine($"Got back response '{e.Result.Text}'");
            };

            LanguageUnderstandingModel lm = LanguageUnderstandingModel.FromAppId("d66a9c17-a234-47db-a4ef-881237063ae9");
            recognizer.AddAllIntents(lm);

        }

        public async Task<IntentResult> RecoOnce()
        {
            await modem.StartVoiceRecord();
            var speech = await recognizer.RecognizeOnceAsync();
            await modem.StopVoiceRecord();

            var json = speech.Properties.GetProperty(PropertyId.LanguageUnderstandingServiceResponse_JsonResult);
            Console.WriteLine(speech.ToString() + " " + json);
            JObject result = JObject.Parse(json);

            var ret = new IntentResult()
            {
                Intent = result["topScoringIntent"]["intent"].ToString(),
                Entities = new Dictionary<string, string>(result["entities"].Count()),
                Text = speech.Text
            };

            foreach (var oneEntity in result["entities"])
            {
                ret.Entities[oneEntity["type"].ToString()] = oneEntity["entity"].ToString();
            }
            return ret;
        }

        public async Task Transcribe()
        {
            ManualResetEvent silenceEvent = new ManualResetEvent(false);

            await modem.StartVoiceRecord();
            recognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.NoMatch)
                {
                    Console.WriteLine("Got end of speech");
                    silenceEvent.Set();
                }
            };

            await recognizer.StartContinuousRecognitionAsync();
            silenceEvent.WaitOne();
            Console.WriteLine("Stopping");
            await recognizer.StopContinuousRecognitionAsync();
            await modem.StopVoiceRecord();
            Console.WriteLine("Stopped");

        }
        public class IntentResult
        {
            public string Intent { get; set; }
            public Dictionary<string, string> Entities { get; set; }
            public string Text { get; set; }
        }
    }
}

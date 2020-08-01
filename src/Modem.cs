using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace phone
{
    class Modem
    {
        private SerialPort serialPort;
        private object modemControlLock = new object();

        private enum state
        {
            idle,
            monitoring,
            lineInUse,
            voiceMode,
            voiceTransfer
        }

        private Task readLoop;

        private state currentState;

        private void Log(string message, params string[] p)
        {
            phone.Log.LogMessage(message, p);
        }

        private async Task SendCommandText(string commandText, string expectedResponse = "OK")
        {
            Log($"Sending command {commandText} ({BitConverter.ToString(Encoding.ASCII.GetBytes(commandText))})");
            serialPort.Write($"{commandText}\r");
            if (!string.IsNullOrWhiteSpace(expectedResponse))
            {
                await ReadLine(expectedResponse);
            }
        }

        public Modem()
        {
            currentState = state.idle;
        }

        public async Task Init(string portName)
        {
            Contract.Assert(currentState == state.idle, "Modem was not idle");

            serialPort = new SerialPort(portName, 115200);
            // Set the read/write timeouts             
            serialPort.ReadTimeout = 1500;
            serialPort.WriteTimeout = 1500;
            serialPort.Open();

            Log("Checking modem");
            await SendCommandText("AT");

            Log("Sending reinit");
            await SendCommandText("ATZ0");
        }

        public IEnumerable<string> AvailablePorts
        {
            get
            {
                return SerialPort.GetPortNames();
            }
        }

        public event EventHandler<RingInfo> Ringing;

        public event EventHandler<byte[]> AudioReceived;
        public async Task EnterVoiceMode()
        {
            Contract.Assert(currentState == state.idle, $"Modem was not idle: {currentState}");
            Log("Changing to voice mode");
            // Put the modem into voice mode:
            await SendCommandText("AT+FCLASS=8");

            Log("Setting output format");
            await SendCommandText("AT+VSM=1");
            currentState = state.voiceMode;
        }

        public async Task PickupLine()
        {
            Log("Picking up the line");
            // Pick up the line:
            await SendCommandText("AT+VLS=1");
        }

        public async Task HangupLine()
        {
            Log("Hanging up the line");
            // Pick up the line:
            await SendCommandText("AT+VLS=0");
        }

        public async Task StartVoiceRecord()
        {
            Log("Setting output format");
            await SendCommandText("AT+VSM=1");

            Log("Starting audio transfer.");
            await SendCommandText("AT+VRX", "CONNECT");
            readLoop = ReadAudio();
        }

        public async Task Speak(byte[] buffer)
        {
            Log("Setting output format");
            await SendCommandText("AT+VSM=131");

            Log("Starting audio transfer out.");
            await SendCommandText("AT+VTX", "CONNECT");

            serialPort.WriteTimeout = 30000;
            serialPort.Write(buffer, 0, buffer.Length);

            serialPort.Write(new char[] { (char)16, (char)33 }, 0, 2);
            await ReadLine("OK");

            serialPort.WriteTimeout = 1500;
        }

        private readonly string ringString = new string(new char[] { (char)16, 'R' });
        private ManualResetEvent ringLoop = new ManualResetEvent(false);

        public async Task<Dictionary<string, string>> WaitForRing()
        {
            stopRingWatch = false;
            bool gotRing = false;
            Dictionary<string, string> ret = new Dictionary<string, string>();

            // Turn on caller ID
            await SendCommandText("AT+VCID=1");

            serialPort.ReadTimeout = 5000;

            ringLoop.Reset();

            while (!stopRingWatch)
            {
                try
                {
                    await ReadLine(ringString);
                    stopRingWatch = true;
                    gotRing = true;
                }
                catch (TimeoutException)
                {
                    Log("Timeout waiting for ring");
                }
            }
            serialPort.ReadTimeout = 1500;
            ringLoop.Set();

            if (gotRing)
            {
                // We got the ring. Now read the next 4 lines.
                for (int i = 0; i < 5; i++)
                {
                    var line = serialPort.ReadLine();
                    Log(line);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var split = line.Split("=");
                        ret.Add(split[0].Substring(0, split[0].Length - 1), split[1].Substring(1, split[1].Length - 1));
                    }
                }
                Ringing(this, new RingInfo(ret));
            }


            return ret;
        }

        private bool stopRingWatch;
        public Task CancelRing()
        {
            return Task.Run(() =>
            {
                stopRingWatch = true;
                ringLoop.WaitOne();
            });


        }

        public async Task<IEnumerable<string>> GetVoiceModes()
        {
            await SendCommandText("AT+VSM=?", null);
            bool done = false;
            List<string> modes = new List<string>();
            while (!done)
            {
                var line = await Read();
                if (line == "OK\r")
                {
                    done = true;
                }
                else if (!line.StartsWith("AT+VSM=?\r") && !string.IsNullOrWhiteSpace(line))
                {
                    Log($"Found Mode {line}");
                    modes.Add(line);
                }
            }

            return modes;
        }

        public async Task StopVoiceRecord()
        {
            serialPort.Write(new char[] { (char)16, (char)33 }, 0, 2);
            await readLoop;
            await ReadOK();
        }

        private Task ReadAudio()
        {
            return Task.Run(() =>
            {
                var buffer = new byte[serialPort.ReadBufferSize];
                bool done = false;

                while (!done)
                {
                    var read = serialPort.Read(buffer, 0, serialPort.ReadBufferSize);
                    StringBuilder hex = new StringBuilder(buffer.Length * 2);

                    byte[] retBuffer = new byte[read];

                    for (int j = 0; j < read; j++)
                    {
                        if (buffer[j] == 16 &&
                            j < read &&
                            buffer[j + 1] == 16)
                        {
                            j++;
                        }

                        if (buffer[j] == 16 &&
                            j < read &&
                            buffer[j + 1] == 3)
                        {
                            done = true;
                        }
                        retBuffer[j] = buffer[j];
                    }

                    AudioReceived(this, retBuffer);
                }

                Log("Read loop done");
                return;
            });
        }

        private async Task ReadOK()
        {
            await ReadLine("OK");
        }

        private async Task ReadLine(string key)
        {
            StringBuilder sb = new StringBuilder();
            Log($"Waiting for key {key} {BitConverter.ToString(Encoding.ASCII.GetBytes(key))}");
            string line = "";
            while (true)
            {
                line = await Read();
                Log($"Null {string.IsNullOrWhiteSpace(line)} {line.StartsWith(key)}");
                if (!string.IsNullOrWhiteSpace(line) && line.StartsWith(key))
                {
                    break;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
            Log("Done waitng");
        }

        private Task<string> Read()
        {
            return Task<string>.Run(() =>
            {
                string message = serialPort.ReadLine();

                Log($"Got {message} {BitConverter.ToString(Encoding.ASCII.GetBytes(message))}");
                return message;
            });
        }
    }

    public class RingInfo
    {
        public DateTime CallTime { get; private set; }
        public string Name { get; private set; }
        public string Number { get; private set; }

        public RingInfo(Dictionary<string, string> cidInfo)
        {
            string date = "0101";
            string time = "0000";


            date = cidInfo["DATE"];
            time = cidInfo["TIME"];
            Name = cidInfo["NAME"];
            Number = cidInfo["NMBR"];

            var dtString = $"{date.Substring(0, 2)}/{date.Substring(2)}/{DateTime.Now.Year} {time.Substring(0, 2)}:{time.Substring(2)}";
            CallTime = DateTime.Parse(dtString);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Call At {CallTime}");
            sb.AppendLine($"From: {Name}");
            sb.AppendLine($"{Number}");
            return sb.ToString();
        }
    }

}
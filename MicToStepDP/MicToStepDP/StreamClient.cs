using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using Concentus.Structs;
using Concentus;
using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using Concentus.Structs;


namespace StreamTestServer
{
    public class StreamClient
    {
        private TcpClient _tcpclient;
        private Thread _tcpthread;

        private bool _running = false;

        private ConcentusCodec _encoder;

        NAudio.Wave.WaveInEvent _waveRecorder;
        StreamWriter _streamWriter;
        public StreamClient()
        {
            _tcpclient = new TcpClient();
        }

        public void start()
        {
            _tcpthread = new Thread(new ThreadStart(handleSoundSending));
            _tcpthread.IsBackground = true;
            _tcpthread.Start();
        }

        int totalMS = 0;
        int totalMScompressed = 0;
        internal bool isRunning;

        private void NWaveIn_DataAvailable(object sender, NAudio.Wave.WaveInEventArgs e)
        {
            byte[] buffer2 = new byte[e.BytesRecorded];
            Console.WriteLine("[CLIENT] recorded " + e.BytesRecorded.ToString() + " bytes PCM audio");

            totalMS += 20;

            Array.Copy(e.Buffer, buffer2, e.BytesRecorded);
            AudioChunk audio = new AudioChunk(buffer2, 16000);
            byte[] opus = _encoder.Compress(audio);

            try
            {
                _tcpclient.GetStream().Write(opus, 0, opus.Length);
            }
            catch(Exception ex)
            {
                // remote connection was closed
                this.stop();
            }

            totalMScompressed += 20;
            Console.WriteLine("[CLIENT] sends " + opus.Length.ToString() + " bytes " + "(total time compressed: " + totalMScompressed.ToString() + ")" + "(total time recorded: " + totalMS.ToString() + ")");


            // check if there are some bytes left over in the encoder
            opus = _encoder.Compress(null);
            if (opus.Length > 0)
            {
                try
                {
                    _tcpclient.GetStream().Write(opus, 0, opus.Length);
                }
                catch(Exception)
                {
                    // remote connection was closed
                    this.stop();
                }

                totalMScompressed += 20;
                Console.WriteLine("[CLIENT] sends " + opus.Length.ToString() + " bytes " + "(total time compressed: " + totalMScompressed.ToString() + ")" + "(total time recorded: " + totalMS.ToString() + ")");
            }


            if (_running == false)
            {
                _waveRecorder.StopRecording();
            }
        }

        public void stop()
        {
            if (_running == false)
                return;

            _running = false;
            _waveRecorder.StopRecording();            
            _tcpthread.Abort();
        }

        private void handleSoundSending()
        {
            _tcpclient.Connect("127.0.0.1", 50001);
            _streamWriter = new StreamWriter(_tcpclient.GetStream());
            _running = true;

            _encoder = new ConcentusCodec();

            _waveRecorder = new NAudio.Wave.WaveInEvent();
            _waveRecorder.WaveFormat = new NAudio.Wave.WaveFormat(16000, 16, 1);            
            _waveRecorder.BufferMilliseconds = 20;
            _waveRecorder.DeviceNumber = 0;
            _waveRecorder.DataAvailable += NWaveIn_DataAvailable;
            _waveRecorder.StartRecording();
        }
    }
}

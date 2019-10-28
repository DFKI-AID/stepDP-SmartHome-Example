using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace StreamTestServer
{
    public class StreamServer
    {
        private TcpListener _tcpserver;
        private Thread _tcpthread;

        private bool _running;
                      
        public StreamServer()
        {
            _tcpserver = new TcpListener(IPAddress.Any, 500001);
        }

        public void start()
        {
            _tcpthread = new Thread(new ThreadStart(this.handleTCPServer));
            _running = true;
            _tcpthread.Start();
        }

        public void stop()
        {
            _running = false;
            _tcpthread.Abort();
        }

        int totalMS = 0;
        private void handleTCPServer()
        {
            _tcpserver.Start();
            TcpClient audioClient = _tcpserver.AcceptTcpClient();

            NAudio.Wave.WaveOut waveOut = new NAudio.Wave.WaveOut();
            NAudio.Wave.BufferedWaveProvider waveBuffer = new NAudio.Wave.BufferedWaveProvider(new NAudio.Wave.WaveFormat(16000, 16, 1));
            waveBuffer.BufferDuration = TimeSpan.FromSeconds(1);
            waveOut.Init(waveBuffer);
            waveOut.Play();

            ConcentusCodec decoder = new ConcentusCodec();

            while (_running)
            {
                byte[] buffer = new byte[1000];
                int read = audioClient.GetStream().Read(buffer, 0, 1000);

                byte[] data = new byte[read];
                Array.Copy(buffer, data, read);

                AudioChunk pcm = decoder.Decompress(data);

                byte[] pcmData = pcm.GetDataAsBytes();
                waveBuffer.AddSamples(pcmData, 0, pcmData.Length);
                totalMS += (int)pcm.Length.TotalMilliseconds;
                Console.WriteLine("[SERVER] reads " + read.ToString() + " bytes, decompressed to " + pcmData.Length.ToString() + " (length: " + pcm.Length.TotalMilliseconds + " ms)" + "(total time: " + totalMS.ToString() + ")");
            }

            audioClient.Close();
            _tcpserver.Stop();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using System.IO;
using Concentus;
using Concentus.Common.CPlusPlus;
using Concentus.Enums;
using Concentus.Structs;
using System.Diagnostics;


namespace StreamTestServer
{

    public class AudioChunk
    {
        public short[] Data;
        public int SampleRate;

        /// <summary>
        /// Creates an empty 16khz audio sample
        /// </summary>
        public AudioChunk()
        {
            Data = new short[0];
            SampleRate = 16000;
        }

        /// <summary>
        /// Creates a new audio sample from a 2-byte little endian array
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="sampleRate"></param>
        public AudioChunk(byte[] rawData, int sampleRate)
            : this(AudioMath.BytesToShorts(rawData), sampleRate)
        {
        }

        /// <summary>
        /// Creates a new audio sample from a base64-encoded chunk representing a 2-byte little endian array
        /// </summary>
        /// <param name="base64Data"></param>
        /// <param name="sampleRate"></param>
        public AudioChunk(string base64Data, int sampleRate)
            : this(Convert.FromBase64String(base64Data), sampleRate)
        {
        }

        /// <summary>
        /// Creates a new audio sample from a linear set of 16-bit samples
        /// </summary>
        /// <param name="rawData"></param>
        /// <param name="sampleRate"></param>
        public AudioChunk(short[] rawData, int sampleRate)
        {
            Data = rawData;
            SampleRate = sampleRate;
        }

        public AudioChunk(Stream wavFileStream)
        {
            List<float[]> buffers = new List<float[]>();
            int length = 0;
            using (WaveFileReader reader = new WaveFileReader(wavFileStream))
            {
                SampleRate = reader.WaveFormat.SampleRate;
                while (reader.Position < reader.Length)
                {
                    float[] data = reader.ReadNextSampleFrame();
                    if (data == null)
                        break;
                    length += data.Length;
                    buffers.Add(data);
                }
            }

            Data = new short[length];
            int cursor = 0;
            float scale = (float)(short.MaxValue);
            foreach (float[] chunk in buffers)
            {
                for (int c = 0; c < chunk.Length; c++)
                {
                    Data[cursor + c] = (short)(chunk[c] * scale);
                }
                cursor += chunk.Length;
            }
            wavFileStream.Close();
        }

        /// <summary>
        /// Creates a new audio sample from a .WAV file name
        /// </summary>
        /// <param name="fileName"></param>
        //public AudioChunk(string fileName)
        //    : this(new FileStream(fileName, FileMode.Open))
        //{
        //}

        public byte[] GetDataAsBytes()
        {
            return AudioMath.ShortsToBytes(Data);
        }

        public string GetDataAsBase64()
        {
            return Convert.ToBase64String(GetDataAsBytes());
        }

        public AudioChunk Amplify(float amount)
        {
            short[] amplifiedData = new short[DataLength];
            for (int c = 0; c < amplifiedData.Length; c++)
            {
                float newVal = (float)Data[c] * amount;
                if (newVal > short.MaxValue)
                    amplifiedData[c] = short.MaxValue;
                else if (newVal < short.MinValue)
                    amplifiedData[c] = short.MinValue;
                else
                    amplifiedData[c] = (short)newVal;
            }
            return new AudioChunk(amplifiedData, SampleRate);
        }

        public float Peak()
        {
            float highest = 0;
            for (int c = 0; c < Data.Length; c++)
            {
                float test = Math.Abs((float)Data[c]);
                if (test > highest)
                    highest = test;
            }
            return highest;
        }

        public double Volume()
        {
            double curVolume = 0;
            // No Enumerable.Average function for short values, so do it ourselves
            for (int c = 0; c < Data.Length; c++)
            {
                if (Data[c] == short.MinValue)
                    curVolume += short.MaxValue;
                else
                    curVolume += Math.Abs(Data[c]);
            }
            curVolume /= DataLength;
            return curVolume;
        }

        public AudioChunk Normalize()
        {
            double volume = Peak();
            return Amplify(short.MaxValue / (float)volume);
        }

        public int DataLength
        {
            get
            {
                return Data.Length;
            }
        }

        public TimeSpan Length
        {
            get
            {
                return TimeSpan.FromMilliseconds((double)Data.Length * 1000 / SampleRate);
            }
        }

        public AudioChunk Concatenate(AudioChunk other)
        {
            AudioChunk toConcatenate = other;
            int combinedDataLength = DataLength + toConcatenate.DataLength;
            short[] combinedData = new short[combinedDataLength];
            Array.Copy(Data, combinedData, DataLength);
            Array.Copy(toConcatenate.Data, 0, combinedData, DataLength, toConcatenate.DataLength);
            return new AudioChunk(combinedData, SampleRate);
        }
    }




    public class AudioMath
    {
        /// <summary>
        /// Converts interleaved byte samples (such as what you get from a capture device)
        /// into linear short samples (that are much easier to work with)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static short[] BytesToShorts(byte[] input)
        {
            return BytesToShorts(input, 0, input.Length);
        }

        /// <summary>
        /// Converts interleaved byte samples (such as what you get from a capture device)
        /// into linear short samples (that are much easier to work with)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static short[] BytesToShorts(byte[] input, int offset, int length)
        {
            short[] processedValues = new short[length / 2];
            for (int c = 0; c < processedValues.Length; c++)
            {
                processedValues[c] = (short)(((int)input[(c * 2) + offset]) << 0);
                processedValues[c] += (short)(((int)input[(c * 2) + 1 + offset]) << 8);
            }

            return processedValues;
        }

        /// <summary>
        /// Converts linear short samples into interleaved byte samples, for writing to a file, waveout device, etc.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static byte[] ShortsToBytes(short[] input)
        {
            return ShortsToBytes(input, 0, input.Length);
        }

        /// <summary>
        /// Converts linear short samples into interleaved byte samples, for writing to a file, waveout device, etc.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static byte[] ShortsToBytes(short[] input, int offset, int length)
        {
            byte[] processedValues = new byte[length * 2];
            for (int c = 0; c < length; c++)
            {
                processedValues[c * 2] = (byte)(input[c + offset] & 0xFF);
                processedValues[c * 2 + 1] = (byte)((input[c + offset] >> 8) & 0xFF);
            }

            return processedValues;
        }

        /// <summary>
        /// Returns the power-of-two value that is closest to the given value.
        /// ex: "100" returns "128", "4100" returns "4096", etc.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        internal static int NPOT(int val)
        {
            double logBase = Math.Log((double)val, 2);
            double upperBound = Math.Ceiling(logBase);
            int nearestPowerOfTwo = (int)Math.Pow(2, upperBound);
            return nearestPowerOfTwo;
        }

        // Curve calculation

        // Hermitian (smoothstep) curves
        private static readonly double[] _smoothStepTable = new double[5000];
        // Window function curves
        private static readonly double[] _nuttallWindow = new double[5000];
        private static readonly double[] _blackmanWindow = new double[5000];

        /// <summary>
        /// Precalculates a bunch of curves and tables upon class initialization
        /// </summary>
        static AudioMath()
        {
            // Precache the hermitian curve table
            for (int c = 0; c < _smoothStepTable.Length; c++)
            {
                double x = (double)c / _smoothStepTable.Length;
                _smoothStepTable[c] = (3 * x * x) - (2 * x * x * x);
            }
            // Blackman window table
            for (int c = 0; c < _blackmanWindow.Length; c++)
            {
                double x = ((double)c / _blackmanWindow.Length) - 0.5;
                double a0 = 7938d / 18606d;
                double a1 = 9240d / 18608d;
                double a2 = 1430d / 18606d;
                _blackmanWindow[c] = 1 - (a0 - (a1 * Math.Cos(2 * Math.PI * x)) + (a2 * Math.Cos(4 * Math.PI * x)));
            }
            // Nuttall window table
            for (int c = 0; c < _nuttallWindow.Length; c++)
            {
                double x = ((double)c / _nuttallWindow.Length) - 0.5;
                double a0 = 0.355768;
                double a1 = 0.487396;
                double a2 = 0.144232;
                double a3 = 0.012604;
                _nuttallWindow[c] = 1 - (a0 - (a1 * Math.Cos(2 * Math.PI * x)) + (a2 * Math.Cos(4 * Math.PI * x)) - (a3 * Math.Cos(6 * Math.PI * x)));
            }
        }

        internal static float GaussianWindow(float x)
        {
            float x1 = (x - 0.5f) * 2f;
            return (float)Math.Exp(0 - (x1 * x1) / 0.15);
        }

        internal static double BlackmanWindow(double x)
        {
            int idx = (int)(x * _blackmanWindow.Length);
            if (idx < 0 || idx >= _blackmanWindow.Length)
                return 0;
            return _blackmanWindow[idx];
        }

        internal static double NuttallWindow(double x)
        {
            int idx = (int)(x * _nuttallWindow.Length);
            if (idx < 0 || idx >= _nuttallWindow.Length)
                return 0;
            return _nuttallWindow[idx];
        }

        /// <summary>
        /// Models the basic smoothstep curve 3x^2 - 2x^3.
        /// This is a smoothed curve between (0, 0) and (1, 1).
        /// Any x < 0 or x > 1 will be clamped to the min/max value.
        /// </summary>
        /// <param name="x">The portion of the curve to return</param>
        /// <returns>The smoothed curve at that x-value</returns>
        internal static double SmoothStep(double x)
        {
            int idx = (int)(x * _smoothStepTable.Length);
            if (idx < 0)
                return 0;
            if (idx >= _smoothStepTable.Length)
                return 1;
            return _smoothStepTable[idx];
        }

        /// <summary>
        /// Normalizes a curve so that the highest peak is equal to 1.0
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        internal static double[] NormalizeCurveByPeak(double[] curve)
        {
            double[] returnVal = new double[curve.Length];
            double peak = 0.0001;
            foreach (double x in curve)
            {
                if (x > peak)
                    peak = x;
            }
            for (int c = 0; c < curve.Length; c++)
            {
                returnVal[c] = curve[c] / peak;
            }
            return returnVal;
        }

        /// <summary>
        /// Normalizes a curve so that its total mass is equal to 1.0
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        internal static double[] NormalizeCurveByMass(double[] curve)
        {
            double[] returnVal = new double[curve.Length];
            double mass = 0.00001;
            foreach (double x in curve)
            {
                mass += Math.Abs(x);
            }
            for (int c = 0; c < curve.Length; c++)
            {
                returnVal[c] = curve[c] / mass;
            }
            return returnVal;
        }

        internal static double HermitianLowpassWindow(double band, double cutoffFreq, double width)
        {
            double start = cutoffFreq - width;
            double end = cutoffFreq + width;
            if (band < start)
                return 1.0;
            if (band > end)
                return 0.0;
            double x = (band - start) / (width * 2);
            return SmoothStep(1 - x);
        }

        internal static double HermitianHighpassWindow(double band, double cutoffFreq, double width)
        {
            double start = cutoffFreq - width;
            double end = cutoffFreq + width;
            if (band < start)
                return 0.0;
            if (band > end)
                return 1.0;
            double x = (band - start) / (width * 2);
            return SmoothStep(x);
        }
    }

    public class ConcentusCodec : IOpusCodec
    {
        private int _bitrate = 16;
        private int _complexity = 5;
        private double _frameSize = 20;
        private int _packetLoss = 0;
        private bool _vbr = false;
        private bool _cvbr = false;
        private OpusApplication _application = OpusApplication.OPUS_APPLICATION_VOIP;

        private BasicBufferShort _incomingSamples = new BasicBufferShort(16000);

        private OpusEncoder _encoder;
        private OpusDecoder _decoder;
        private Stopwatch _timer = new Stopwatch();

        private byte[] scratchBuffer = new byte[10000];

        public ConcentusCodec()
        {
            _encoder = OpusEncoder.Create(16000, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            _encoder.LSBDepth = 16;
            SetBitrate(_bitrate);
            SetComplexity(_complexity);
            SetVBRMode(_vbr, _cvbr);
            _encoder.EnableAnalysis = false; // works onyl with 48khz
            _decoder = OpusDecoder.Create(16000, 1);            
        }

        public void SetBitrate(int bitrate)
        {
            _bitrate = bitrate;
            _encoder.Bitrate = (_bitrate * 1024);
        }

        public void SetComplexity(int complexity)
        {
            _complexity = complexity;
            _encoder.Complexity = (_complexity);
        }

        public void SetFrameSize(double frameSize)
        {
            _frameSize = frameSize;
        }

        public void SetPacketLoss(int loss)
        {
            _packetLoss = loss;
            if (loss > 0)
            {
                _encoder.PacketLossPercent = _packetLoss;
                _encoder.UseInbandFEC = true;
            }
            else
            {
                _encoder.PacketLossPercent = 0;
                _encoder.UseInbandFEC = false;
            }
        }

        public void SetApplication(OpusApplication application)
        {
            _application = application;
            _encoder.Application = _application;
        }

        public void SetVBRMode(bool vbr, bool constrained)
        {
            _vbr = vbr;
            _cvbr = constrained;
            _encoder.UseVBR = vbr;
            _encoder.UseConstrainedVBR = constrained;
        }

        private int GetFrameSize()
        {
            return (int)(16000 * _frameSize / 1000);
        }

        public byte[] Compress(AudioChunk input)
        {
            int frameSize = GetFrameSize();

            if (input != null)
            {
                short[] newData = input.Data;
                _incomingSamples.Write(newData);
            }
            else
            {
                // If input is null, assume we are at end of stream and pad the output with zeroes
                int paddingNeeded = _incomingSamples.Available() % frameSize;
                if (paddingNeeded > 0)
                {
                    _incomingSamples.Write(new short[paddingNeeded]);
                }
            }

            int outCursor = 0;

            if (_incomingSamples.Available() >= frameSize)
            {
                _timer.Reset();
                _timer.Start();
                short[] nextFrameData = _incomingSamples.Read(frameSize);
                int thisPacketSize = _encoder.Encode(nextFrameData, 0, frameSize, scratchBuffer, outCursor, scratchBuffer.Length);                
                outCursor += thisPacketSize;
                _timer.Stop();
            }

            byte[] finalOutput = new byte[outCursor];
            Array.Copy(scratchBuffer, 0, finalOutput, 0, outCursor);
            return finalOutput;
        }

        public AudioChunk Decompress(byte[] inputPacket)
        {
            int frameSize = GetFrameSize();

            short[] outputBuffer = new short[frameSize];

       
            // Normal decoding
            int thisFrameSize = _decoder.Decode(inputPacket, 0, inputPacket.Length, outputBuffer, 0, frameSize, false);

            short[] finalOutput = new short[thisFrameSize];
            Array.Copy(outputBuffer, finalOutput, thisFrameSize);

            // Update statistics
            OpusMode curMode = OpusPacketInfo.GetEncoderMode(inputPacket, 0);            
            OpusBandwidth curBandwidth = OpusPacketInfo.GetBandwidth(inputPacket, 0);     
            return new AudioChunk(finalOutput, 16000);
        }
    }


    public interface IOpusCodec
    {
        void SetBitrate(int bitrate);
        void SetComplexity(int complexity);
        void SetPacketLoss(int loss);
        void SetApplication(OpusApplication application);
        void SetFrameSize(double frameSize);
        void SetVBRMode(bool vbr, bool constrained);
        byte[] Compress(AudioChunk input);
        AudioChunk Decompress(byte[] input);
    }


    public class BasicBuffer<T>
    {
        private T[] data;
        private int writeIndex = 0;
        private int readIndex = 0;
        private int available = 0;
        private int capacity = 0;

        public BasicBuffer(int capacity)
        {
            this.capacity = capacity;
            data = new T[capacity];
        }

        public void Write(T[] toWrite)
        {
            // Write the data in chunks
            int sourceIndex = 0;
            while (sourceIndex < toWrite.Length)
            {
                int count = Math.Min(toWrite.Length - sourceIndex, capacity - writeIndex);
                Array.Copy(toWrite, sourceIndex, data, writeIndex, count);
                writeIndex = (writeIndex + count) % capacity;
                sourceIndex += count;
            }
            available += toWrite.Length;
            // Did we overflow? In this case, move the readIndex to just after the writeIndex
            if (available > capacity)
            {
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
            }
        }

        /// <summary>
        /// Writes a single value
        /// </summary>
        /// <param name="toWrite"></param>
        public void Write(T toWrite)
        {
            data[writeIndex] = toWrite;
            writeIndex = (writeIndex + 1) % capacity;
            available += 1;
            if (available > capacity)
            {
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
            }
        }

        public void Clear()
        {
            writeIndex = 0;
            readIndex = 0;
            available = 0;
        }

        public T[] Read(int count)
        {
            T[] returnVal = new T[count];
            // Read the data in chunks
            int sourceIndex = 0;
            while (sourceIndex < count)
            {
                int readCount = Math.Min(count - sourceIndex, capacity - readIndex);
                Array.Copy(data, readIndex, returnVal, sourceIndex, readCount);
                readIndex = (readIndex + readCount) % capacity;
                sourceIndex += readCount;
            }
            available -= count;
            // Did we underflow? In this case, move the writeIndex to where the next data will be read
            if (available < 0)
            {
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }
            return returnVal;
        }

        /// <summary>
        /// Reads a single value
        /// </summary>
        /// <returns></returns>
        public T Read()
        {
            T returnVal = data[readIndex];
            readIndex = (readIndex + 1) % capacity;
            available -= 1;
            if (available < 0)
            {
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }
            return returnVal;
        }

        /// <summary>
        /// Reads from the buffer without actually consuming the data
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public T[] Peek(int count)
        {
            int toRead = Math.Min(available, count);
            T[] returnVal = new T[toRead];
            // Read the data in chunks
            int sourceIndex = 0;
            int localReadIndex = readIndex;
            while (sourceIndex < toRead)
            {
                int readCount = Math.Min(toRead - sourceIndex, capacity - localReadIndex);
                Array.Copy(data, localReadIndex, returnVal, sourceIndex, readCount);
                localReadIndex = (localReadIndex + readCount) % capacity;
                sourceIndex += readCount;
            }
            return returnVal;
        }

        public int Available()
        {
            return available;
        }

        public int Capacity()
        {
            return capacity;
        }
    }

    /// <summary>
    /// A drop-in implementation of BasicBuffer strongly typed to the int16 data.
    /// This has better performance in the CLR because it avoids the boxed arrays used in the generic buffer class
    /// </summary>
    public class BasicBufferShort
    {
        private short[] data;
        private int writeIndex = 0;
        private int readIndex = 0;
        private int available = 0;
        private int capacity = 0;

        public BasicBufferShort(int capacity)
        {
            this.capacity = capacity;
            data = new short[capacity];
        }

        public void Write(short[] toWrite)
        {
            // Write the data in chunks
            int sourceIndex = 0;
            while (sourceIndex < toWrite.Length)
            {
                int count = Math.Min(toWrite.Length - sourceIndex, capacity - writeIndex);
                Array.Copy(toWrite, sourceIndex, data, writeIndex, count);
                writeIndex = (writeIndex + count) % capacity;
                sourceIndex += count;
            }
            available += toWrite.Length;
            // Did we overflow? In this case, move the readIndex to just after the writeIndex
            if (available > capacity)
            {
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
            }
        }

        /// <summary>
        /// Writes a single value
        /// </summary>
        /// <param name="toWrite"></param>
        public void Write(short toWrite)
        {
            data[writeIndex] = toWrite;
            writeIndex = (writeIndex + 1) % capacity;
            available += 1;
            if (available > capacity)
            {
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
            }
        }

        public void Clear()
        {
            writeIndex = 0;
            readIndex = 0;
            available = 0;
        }

        public short[] Read(int count)
        {
            short[] returnVal = new short[count];
            // Read the data in chunks
            int sourceIndex = 0;
            while (sourceIndex < count)
            {
                int readCount = Math.Min(count - sourceIndex, capacity - readIndex);
                Array.Copy(data, readIndex, returnVal, sourceIndex, readCount);
                readIndex = (readIndex + readCount) % capacity;
                sourceIndex += readCount;
            }
            available -= count;
            // Did we underflow? In this case, move the writeIndex to where the next data will be read
            if (available < 0)
            {
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }
            return returnVal;
        }

        /// <summary>
        /// Reads a single value
        /// </summary>
        /// <returns></returns>
        public short Read()
        {
            short returnVal = data[readIndex];
            readIndex = (readIndex + 1) % capacity;
            available -= 1;
            if (available < 0)
            {
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }
            return returnVal;
        }

        /// <summary>
        /// Reads from the buffer without actually consuming the data
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public short[] Peek(int count)
        {
            int toRead = Math.Min(available, count);
            short[] returnVal = new short[toRead];
            // Read the data in chunks
            int sourceIndex = 0;
            int localReadIndex = readIndex;
            while (sourceIndex < toRead)
            {
                int readCount = Math.Min(toRead - sourceIndex, capacity - localReadIndex);
                Array.Copy(data, localReadIndex, returnVal, sourceIndex, readCount);
                localReadIndex = (localReadIndex + readCount) % capacity;
                sourceIndex += readCount;
            }
            return returnVal;
        }

        public int Available()
        {
            return available;
        }

        public int Capacity()
        {
            return capacity;
        }
    }

    /// <summary>
    /// A drop-in implementation of BasicBuffer strongly typed to the uint8 data.
    /// This has better performance in the CLR because it avoids the boxed arrays used in the generic buffer class
    /// </summary>
    public class BasicBufferByte
    {
        private byte[] data;
        private int writeIndex = 0;
        private int readIndex = 0;
        private int available = 0;
        private int capacity = 0;

        public BasicBufferByte(int capacity)
        {
            this.capacity = capacity;
            data = new byte[capacity];
        }

        public void Write(byte[] toWrite)
        {
            // Write the data in chunks
            int sourceIndex = 0;
            while (sourceIndex < toWrite.Length)
            {
                int count = Math.Min(toWrite.Length - sourceIndex, capacity - writeIndex);
                Array.Copy(toWrite, sourceIndex, data, writeIndex, count);
                writeIndex = (writeIndex + count) % capacity;
                sourceIndex += count;
            }
            available += toWrite.Length;
            // Did we overflow? In this case, move the readIndex to just after the writeIndex
            if (available > capacity)
            {
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
            }
        }

        /// <summary>
        /// Writes a single value
        /// </summary>
        /// <param name="toWrite"></param>
        public void Write(byte toWrite)
        {
            data[writeIndex] = toWrite;
            writeIndex = (writeIndex + 1) % capacity;
            available += 1;
            if (available > capacity)
            {
                readIndex = (writeIndex + 1) % capacity;
                available = capacity;
            }
        }

        public void Clear()
        {
            writeIndex = 0;
            readIndex = 0;
            available = 0;
        }

        public byte[] Read(int count)
        {
            byte[] returnVal = new byte[count];
            // Read the data in chunks
            int sourceIndex = 0;
            while (sourceIndex < count)
            {
                int readCount = Math.Min(count - sourceIndex, capacity - readIndex);
                Array.Copy(data, readIndex, returnVal, sourceIndex, readCount);
                readIndex = (readIndex + readCount) % capacity;
                sourceIndex += readCount;
            }
            available -= count;
            // Did we underflow? In this case, move the writeIndex to where the next data will be read
            if (available < 0)
            {
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }
            return returnVal;
        }

        /// <summary>
        /// Reads a single value
        /// </summary>
        /// <returns></returns>
        public byte Read()
        {
            byte returnVal = data[readIndex];
            readIndex = (readIndex + 1) % capacity;
            available -= 1;
            if (available < 0)
            {
                writeIndex = (readIndex + 1) % capacity;
                available = 0;
            }
            return returnVal;
        }

        /// <summary>
        /// Reads from the buffer without actually consuming the data
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public byte[] Peek(int count)
        {
            int toRead = Math.Min(available, count);
            byte[] returnVal = new byte[toRead];
            // Read the data in chunks
            int sourceIndex = 0;
            int localReadIndex = readIndex;
            while (sourceIndex < toRead)
            {
                int readCount = Math.Min(toRead - sourceIndex, capacity - localReadIndex);
                Array.Copy(data, localReadIndex, returnVal, sourceIndex, readCount);
                localReadIndex = (localReadIndex + readCount) % capacity;
                sourceIndex += readCount;
            }
            return returnVal;
        }

        public int Available()
        {
            return available;
        }

        public int Capacity()
        {
            return capacity;
        }
    }

}

/*
 * Copyright 2017-2022 by Starkku
 * This file is part of BagFileTool, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see LICENSE.txt.
 */

using System;
using System.IO;

namespace BagFileTool.FileTypes
{
    /// <summary>
    /// PCM / IMA ADPCM-encoded wave audio file.
    /// </summary>
    public class WavFile
    {
        /// <summary>
        /// True if the wave audio file has been initialized, otherwise false.
        /// </summary>
        public bool Initialized => audioData != null;

        /// <summary>
        /// Filename of the wave audio file.
        /// </summary>
        public string Filename { get; private set; }

        /// <summary>
        /// Encoding type used by the wave audio file.
        /// </summary>
        public WavFileEncoding Encoding { get; private set; } = WavFileEncoding.Invalid;

        /// <summary>
        /// Number of audio channels in the wave audio file.
        /// </summary>
        public short Channels { get; private set; }

        /// <summary>
        /// Sample rate of the wave audio file.
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Block / chunk size of wave audio file data. Only used by IMA ADPCM encoding. 
        /// </summary>
        public int BlockSize { get; private set; }

        /// <summary>
        /// Size of wave audio data.
        /// </summary>
        public int AudioDataSize { get; private set; }

        /// <summary>
        /// How many bits there are per audio sample.
        /// </summary>
        public short BitsPerSample { get; private set; }

        private byte[] audioData = null;
        private bool headerLoaded = false;
        private long dataPosition;

        /// <summary>
        /// Initialize a wave audio file from pre-defined set of data.
        /// </summary>
        /// <param name="filename">Filename of the wave audio file.</param>
        /// <param name="sampleRate">Sample rate of the wave audio file.</param>
        /// <param name="channels">Number of audio channels in the wave audio file.</param>
        /// <param name="useADPCMEncoding">Use ADPCM encoding instead of PCM.</param>
        /// <param name="bitsPerSample">Number of bits per sample in the wave audio file.</param>
        /// <param name="audioData">Actual wave audio data in a byte array.</param>
        /// <param name="blockSize">Block / chunk size of the wave audio file. Only used by IMA ADPCM encoding.</param>
        public WavFile(string filename, int sampleRate, short channels, bool useADPCMEncoding, short bitsPerSample, byte[] audioData, int blockSize = 0)
        {
            if (audioData == null || audioData.Length < 1)
                return;

            SampleRate = sampleRate;
            Filename = filename;
            Channels = channels;
            Encoding = useADPCMEncoding ? WavFileEncoding.IMA_ADPCM : WavFileEncoding.PCM;
            BitsPerSample = bitsPerSample;
            this.audioData = audioData;
            BlockSize = blockSize;
        }

        /// <summary>
        /// Initialize a wave audio file from pre-defined set of data.
        /// </summary>
        /// <param name="filename">Filename of the wave audio file.</param>
        /// <param name="sampleRate">Sample rate of the wave audio file.</param>
        /// <param name="formatFlag">A format flag. 6 = mono PCM, 7 = stereo PCM, 12/28 = mono IMA ADPCM, 13/29 = stereo IMA ADPCM. Any other values are unsupported.</param>
        /// <param name="audioData">Actual wave audio data in a byte array.</param>
        /// <param name="blockSize">Block / chunk size of the wave audio file. Only used by IMA ADPCM encoding.</param>
        public WavFile(string filename, int sampleRate, int formatFlag, byte[] audioData, int blockSize = 0)
        {
            if (audioData == null || audioData.Length < 1)
                return;

            SampleRate = sampleRate;
            Filename = filename;
            this.audioData = audioData;
            BlockSize = blockSize;

            switch (formatFlag)
            {
                case 2:
                    Channels = 1;
                    Encoding = WavFileEncoding.PCM;
                    BitsPerSample = 8;
                    break;
                case 3:
                    Channels = 2;
                    Encoding = WavFileEncoding.PCM;
                    BitsPerSample = 8;
                    break;
                case 6:
                    Channels = 1;
                    Encoding = WavFileEncoding.PCM;
                    BitsPerSample = 16;
                    break;
                case 7:
                    Channels = 2;
                    Encoding = WavFileEncoding.PCM;
                    BitsPerSample = 16;
                    break;
                case 12:
                    Channels = 1;
                    Encoding = WavFileEncoding.IMA_ADPCM;
                    BitsPerSample = 4;
                    break;
                case 13:
                    Channels = 2;
                    Encoding = WavFileEncoding.IMA_ADPCM;
                    BitsPerSample = 4;
                    break;
                case 28:
                    Channels = 1;
                    Encoding = WavFileEncoding.IMA_ADPCM;
                    BitsPerSample = 4;
                    break;
                case 29:
                    Channels = 2;
                    Encoding = WavFileEncoding.IMA_ADPCM;
                    BitsPerSample = 4;
                    break;
                default:
                    Encoding = WavFileEncoding.Invalid;
                    return;
            }
        }

        /// <summary>
        /// Initialize a wave audio object with filename.
        /// </summary>
        /// <param name="filenameInput">Input filename of the wave audio file.</param>
        public WavFile(string filenameInput)
        {
            Filename = filenameInput;
        }

        /// <summary>
        /// Initialize a wave audio object.
        /// </summary>
        public WavFile()
        {
        }

        /// <summary>
        /// Loads header only from wave audio file.
        /// </summary>
        /// <param name="filenameInput">Filename to load the wave audio file from.</param>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        public string LoadHeader(string filenameInput = null)
        {
            headerLoaded = false;
            audioData = null;

            if (!string.IsNullOrEmpty(filenameInput))
                Filename = filenameInput;

            FileStream fs = null;
            byte[] buffer = new byte[4];
            int factSize = 0;
            int listSize = 0;

            try
            {
                fs = new FileStream(Filename, FileMode.Open);

                fs.Seek(16, SeekOrigin.Begin);
                fs.Read(buffer, 0, 4);
                int HeaderSize = BitConverter.ToInt32(buffer, 0);

                fs.Seek(20, SeekOrigin.Begin);
                fs.Read(buffer, 0, 2);
                short AudioFormat = BitConverter.ToInt16(buffer, 0);

                if (AudioFormat == 1)
                    Encoding = WavFileEncoding.PCM;
                else if (AudioFormat == 17)
                    Encoding = WavFileEncoding.IMA_ADPCM;
                else
                    return "Unknown audio encoding.";

                fs.Seek(22, SeekOrigin.Begin);
                fs.Read(buffer, 0, 2);
                Channels = BitConverter.ToInt16(buffer, 0);

                fs.Seek(24, SeekOrigin.Begin);
                fs.Read(buffer, 0, 4);
                SampleRate = BitConverter.ToInt32(buffer, 0);

                fs.Seek(32, SeekOrigin.Begin);
                fs.Read(buffer, 0, 2);
                short BlockAlign = BitConverter.ToInt16(buffer, 0);

                if (Encoding == WavFileEncoding.IMA_ADPCM)
                    BlockSize = BlockAlign;

                fs.Seek(34, SeekOrigin.Begin);
                fs.Read(buffer, 0, 2);
                BitsPerSample = BitConverter.ToInt16(buffer, 0);

                if (Encoding == WavFileEncoding.PCM)
                {
                    fs.Seek(HeaderSize + 20, SeekOrigin.Begin);
                    fs.Read(buffer, 0, 4);
                    string listID = System.Text.Encoding.ASCII.GetString(buffer, 0, buffer.Length);

                    if (listID.Equals("list", StringComparison.InvariantCultureIgnoreCase))
                    {
                        fs.Seek(HeaderSize + 24, SeekOrigin.Begin);
                        fs.Read(buffer, 0, 4);
                        listSize = BitConverter.ToInt32(buffer, 0) + 8;
                    }

                    fs.Seek(HeaderSize + 24 + listSize, SeekOrigin.Begin);
                    fs.Read(buffer, 0, 4);
                    AudioDataSize = BitConverter.ToInt32(buffer, 0);
                }
                else if (Encoding == WavFileEncoding.IMA_ADPCM)
                {
                    fs.Seek(HeaderSize + 20, SeekOrigin.Begin);
                    fs.Read(buffer, 0, 4);
                    string factID = System.Text.Encoding.ASCII.GetString(buffer, 0, buffer.Length);

                    if (factID.Equals("fact", StringComparison.InvariantCultureIgnoreCase))
                        factSize = 12;

                    fs.Seek(HeaderSize + 20 + factSize, SeekOrigin.Begin);
                    fs.Read(buffer, 0, 4);
                    string listID = System.Text.Encoding.ASCII.GetString(buffer, 0, buffer.Length);

                    if (listID.Equals("list", StringComparison.InvariantCultureIgnoreCase))
                    {
                        fs.Seek(HeaderSize + 24 + factSize, SeekOrigin.Begin);
                        fs.Read(buffer, 0, 4);
                        listSize = BitConverter.ToInt32(buffer, 0) + 8;
                    }

                    fs.Seek(HeaderSize + 24 + factSize + listSize, SeekOrigin.Begin);
                    fs.Read(buffer, 0, 4);
                    AudioDataSize = BitConverter.ToInt32(buffer, 0);
                }

                dataPosition = fs.Position;
                //audioData = new byte[dataSize];
                //fs.Read(audioData, 0, dataSize);

            }
            catch (Exception e)
            {
                return e.Message;
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }

            //Initialized = true;
            headerLoaded = true;
            return null;
        }


        /// <summary>
        /// Loads data only from wave audio file.
        /// </summary>
        /// <param name="filenameInput">Filename to load the wave audio file from.</param>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        public string LoadData(string filenameInput = null)
        {
            if (Initialized)
                return "Wave audio file data has already been loaded.";

            if (!headerLoaded)
                return "Wave audio file header information has not been loaded.";

            if (!string.IsNullOrEmpty(filenameInput))
                Filename = filenameInput;

            FileStream fs = null;

            try
            {
                fs = new FileStream(Filename, FileMode.Open);
                audioData = new byte[AudioDataSize];
                fs.Seek(dataPosition, SeekOrigin.Begin);
                fs.Read(audioData, 0, AudioDataSize);
            }
            catch (Exception e)
            {
                return e.Message;
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }

            return null;
        }

        /// <summary>
        /// Saves a wave audio file.
        /// </summary>
        /// <param name="filenameOutput">Filename to use when saving the wave audio file.</param>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        public string Save(string filenameOutput = null)
        {
            if (!Initialized)
                return "Wave audio file not properly initialized.";

            if (headerLoaded && audioData == null)
                return "Wave audio file data has not been loaded yet.";

            if (Encoding == WavFileEncoding.PCM)
                return SavePCMFile(filenameOutput);
            else if (Encoding == WavFileEncoding.IMA_ADPCM)
                return SaveADPCMFile(filenameOutput);

            return "Wave audio file encoding type unknown.";
        }

        /// <summary>
        /// Saves a wave audio file using PCM encoding.
        /// </summary>
        /// <param name="filenameOutput">Filename to use when saving the wave audio file.</param>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        private string SavePCMFile(string filenameOutput)
        {
            string filename = Filename;

            if (!string.IsNullOrEmpty(filenameOutput))
                filename = filenameOutput;

            FileStream fs = null;

            try
            {
                fs = new FileStream(filename, FileMode.Create);
                byte[] tmp = System.Text.Encoding.ASCII.GetBytes("RIFF");
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(audioData.Length + 36);
                fs.Write(tmp, 0, tmp.Length);

                tmp = System.Text.Encoding.ASCII.GetBytes("WAVE");
                fs.Write(tmp, 0, tmp.Length);

                tmp = System.Text.Encoding.ASCII.GetBytes("fmt ");
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(16);
                fs.Write(tmp, 0, tmp.Length);
                short audioformat = 1;
                tmp = BitConverter.GetBytes(audioformat);
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(Channels);
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(SampleRate);
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(SampleRate * Channels * (BitsPerSample / 8));
                fs.Write(tmp, 0, tmp.Length);
                short blockalign = (short)(Channels * (BitsPerSample / 8));
                tmp = BitConverter.GetBytes(blockalign);
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(BitsPerSample);
                fs.Write(tmp, 0, tmp.Length);

                tmp = System.Text.Encoding.ASCII.GetBytes("data");
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(audioData.Length);
                fs.Write(tmp, 0, tmp.Length);
                fs.Write(audioData, 0, audioData.Length);
            }
            catch (Exception e)
            {
                return e.Message;
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }

            return null;
        }

        /// <summary>
        /// Save a wave audio file using IMA ADPCM encoding.
        /// </summary>
        /// <param name="filenameOutput">Filename to use when saving the wave audio file.</param>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        private string SaveADPCMFile(string filenameOutput)
        {
            string filename = Filename;

            if (!string.IsNullOrEmpty(filenameOutput))
                filename = filenameOutput;

            FileStream fs = null;

            try
            {
                fs = new FileStream(filename, FileMode.Create);
                byte[] tmp = System.Text.Encoding.ASCII.GetBytes("RIFF");
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(audioData.Length + 52);
                fs.Write(tmp, 0, tmp.Length);

                tmp = System.Text.Encoding.ASCII.GetBytes("WAVE");
                fs.Write(tmp, 0, tmp.Length);

                tmp = System.Text.Encoding.ASCII.GetBytes("fmt ");
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(20);
                fs.Write(tmp, 0, tmp.Length);
                short audioformat = 17;
                tmp = BitConverter.GetBytes(audioformat);
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(Channels);
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(SampleRate);
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes((SampleRate * Channels * BlockSize) / ((BlockSize * (2 / Channels)) - 7) / Channels);
                fs.Write(tmp, 0, tmp.Length);
                short blocksize = (short)(BlockSize);
                tmp = BitConverter.GetBytes(blocksize);
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(BitsPerSample);
                fs.Write(tmp, 0, tmp.Length);
                short ima = 2;
                tmp = BitConverter.GetBytes(ima);
                fs.Write(tmp, 0, tmp.Length);
                byte channelmodifier = (byte)(Channels == 1 ? 2 : 1);
                short blocksizeflag = (short)(BlockSize * channelmodifier - 7);
                tmp = BitConverter.GetBytes(blocksizeflag);
                fs.Write(tmp, 0, tmp.Length);

                tmp = System.Text.Encoding.ASCII.GetBytes("fact");
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(4);
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes((audioData.Length / BlockSize) * ((BlockSize * (2 / Channels)) - 7));
                fs.Write(tmp, 0, tmp.Length);

                tmp = System.Text.Encoding.ASCII.GetBytes("data");
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(audioData.Length);
                fs.Write(tmp, 0, tmp.Length);
                fs.Write(audioData, 0, audioData.Length);
            }
            catch (Exception e)
            {
                return e.Message;
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }

            return null;
        }

        /// <summary>
        /// Returns a copy of this wave audio object's wave audio data.
        /// </summary>
        /// <returns>Byte array of wave audio data. Null if audio data has not been loaded yet.</returns>
        public byte[] GetAudioData()
        {
            if (headerLoaded && audioData == null)
                return null;

            return (byte[])audioData.Clone();
        }
    }

    /// <summary>
    /// Wave audio file encoding type.
    /// </summary>
    public enum WavFileEncoding { PCM, IMA_ADPCM, Invalid };
}

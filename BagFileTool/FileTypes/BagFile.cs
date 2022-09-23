/*
 * Copyright 2017-2022 by Starkku
 * This file is part of BagFileTool, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see LICENSE.txt.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Starkku.Utilities.FileTypes;

namespace BagFileTool.FileTypes
{
    /// <summary>
    /// Command & Conquer: Red Alert 2 audio bag file.
    /// </summary>
    public class BagFile
    {
        // TODO: Add proper support for Emperor: Battle for Dune bag files. 
        // Does not currently support the merged index + bag the game uses by default (if manually separated beforehand it will work for wave audio) or the MP3 audio it can make use of.

        /// <summary>
        /// Has the bag file been initialized & loaded from a file or not.
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Input filename of the audio bag file.
        /// </summary>
        public string FilenameInput { get; private set; }

        /// <summary>
        /// Output filename of the audio bag file.
        /// </summary>
        public string FilenameOutput { get; private set; }

        /// <summary>
        /// Input filename of the audio index file.
        /// </summary>
        public string IndexFilenameInput { get; private set; }

        /// <summary>
        /// Output filename of the audio index file.
        /// </summary>
        public string IndexFilenameOutput { get; private set; }

        /// <summary>
        /// Current offset position at the end of the bag file to which new data can be appended to.
        /// </summary>
        public int DataEndOffset { get; private set; }

        private IdxFile indexFile;
        private readonly Dictionary<string, WavFile> filesToAdd = new Dictionary<string, WavFile>();
        private FileStream fsInput = null;
        private bool modifiedSinceLastSave = false;

        /// <summary>
        /// Initializes a new audio bag file.
        /// </summary>
        /// <param name="filenameInput">Input filename of the audio bag file.</param>
        /// <param name="filenameInput">Output filename of the audio bag file. If not set, input filename is used as output.</param>
        /// <param name="indexFilenameInput">Input filename of the audio index file. If not set, input bag file name is used with altered extension.</param>
        /// <param name="indexFilenameOutput">Output filename of the audio index file. If not set, output bag file name is used with altered extension.</param>
        public BagFile(string filenameInput, string filenameOutput = null, string indexFilenameInput = null, string indexFilenameOutput = null)
        {
            FilenameInput = filenameInput;

            if (!string.IsNullOrEmpty(filenameOutput))
                FilenameOutput = filenameOutput;
            else if (!string.IsNullOrEmpty(FilenameInput))
                FilenameOutput = FilenameInput;

            if (!string.IsNullOrEmpty(indexFilenameInput))
                IndexFilenameInput = indexFilenameInput;
            else if (!string.IsNullOrEmpty(FilenameInput))
                IndexFilenameInput = Path.ChangeExtension(FilenameInput, ".idx");

            if (!string.IsNullOrEmpty(indexFilenameOutput))
                IndexFilenameOutput = indexFilenameOutput;
            else
                IndexFilenameOutput = Path.ChangeExtension(FilenameOutput, ".idx");
        }

        /// <summary>
        /// Initializes a new, empty audio bag file.
        /// <paramref name="filenameOutput"/>
        /// <paramref name="indexFilenameOutput"/>
        /// </summary>
        public BagFile(string filenameOutput, string indexFilenameOutput = null) : this(null, filenameOutput, null, indexFilenameOutput)
        {
        }

        ~BagFile()
        {
            if (fsInput != null)
                fsInput.Close();
        }

        /// <summary>
        /// Initializes the file streams and index file used by the audio bag file.
        /// </summary>
        /// <param name="skipInput">If set to true, initializing input file stream is skipped.</param>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        public string Initialize()
        {
            bool skipInput = FilenameInput == null;

            if (!skipInput && !File.Exists(FilenameInput))
            {
                Initialized = false;
                return "Bag file '" + FilenameInput + "' does not exist.";
            }
            else if (string.IsNullOrEmpty(FilenameOutput))
                return "Bag file output filename is invalid.";

            else
            {
                try
                {
                    if (!skipInput)
                        fsInput = new FileStream(FilenameInput, FileMode.Open);
                }
                catch (Exception e)
                {
                    Initialized = false;
                    return "Bag file stream initialization failed: " + e.Message;
                }

                if (fsInput != null && !fsInput.CanRead)
                {
                    Initialized = false;
                    return "Bag file '" + FilenameInput + "' exists but is in use by another program.";
                }

                if (fsInput != null && fsInput.CanRead)
                    DataEndOffset = (int)fsInput.Length;

                byte[] buffer = new byte[4];
                fsInput.Read(buffer, 0, buffer.Length);
                string id = Encoding.ASCII.GetString(buffer);
                fsInput.Read(buffer, 0, buffer.Length);
                int version = BitConverter.ToInt32(buffer, 0);

                if (id == "GABA" && version == 4)
                {
                    fsInput.Read(buffer, 0, buffer.Length);
                    int fileCount = BitConverter.ToInt32(buffer, 0);
                    long offset = fsInput.Position + 4 + fileCount * 64;
                    FileStream fsIndex;

                    try
                    {
                        IndexFilenameInput = Path.GetTempFileName();
                        fsIndex = new FileStream(IndexFilenameInput, FileMode.Create);
                    }
                    catch (Exception e)
                    {
                        Initialized = false;
                        return "Index file (merged) stream initialization failed: " + e.Message;
                    }

                    byte[] buffer_b = new byte[offset];
                    fsInput.Position = 0;
                    fsInput.Read(buffer_b, 0, buffer_b.Length);
                    fsIndex.Write(buffer_b, 0, buffer_b.Length);
                    fsIndex.Close();
                }

                fsInput.Position = 0;

                string errorMsg = InitializeIndex();

                if (errorMsg != null)
                {
                    Initialized = false;
                    return "Loading index file failed: " + errorMsg;
                }

                Initialized = true;
                return null;
            }
        }

        /// <summary>
        /// Saves audio bag & index file.
        /// </summary>
        /// <param name="filenameOutput"></param>
        /// <param name="indexFilenameOutput"></param>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        public string Save(string filenameOutput = null, string indexFilenameOutput = null)
        {
            if (!modifiedSinceLastSave)
                return "No modifications have been made since last save.";

            if (indexFile == null || !indexFile.Initialized)
                return "Index file has not been initialized.";
            else if (!Initialized)
                return "Bag file has not been initialized.";

            string filename = FilenameOutput;

            if (!string.IsNullOrEmpty(filenameOutput))
                filename = filenameOutput;

            string errorMsg;

            FileStream fs = null;

            try
            {
                fs = new FileStream(filename, FileMode.Create);

                foreach (IdxFileHeader header in indexFile.Headers)
                {
                    filesToAdd.TryGetValue(header.Filename, out WavFile wavFile);
                    int position = (int)fs.Position;
                    if (wavFile != null)
                    {
                        errorMsg = wavFile.LoadData();

                        if (errorMsg != null)
                            return "Failed to save file " + wavFile.Filename + " to bag file: " + errorMsg;

                        byte[] audioData = wavFile.GetAudioData();
                        fs.Write(audioData, 0, audioData.Length);
                        filesToAdd.Remove(header.Filename);
                    }
                    else
                    {
                        byte[] audioData = new byte[header.DataSize];
                        fsInput.Seek(header.DataOffset, SeekOrigin.Begin);
                        fsInput.Read(audioData, 0, audioData.Length);
                        fs.Write(audioData, 0, audioData.Length);
                    }

                    header.DataOffset = position;
                }
            }
            catch (Exception e)
            {
                return "Failed to save bag file: " + e.Message;
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }

            if (indexFile.Version != 4)
                errorMsg = indexFile.Save(indexFilenameOutput);
            else
            {
                errorMsg = indexFile.Save(filename, filename);
            }

            if (errorMsg != null)
                return "Saving index file failed: " + errorMsg;

            modifiedSinceLastSave = false;
            return null;
        }

        /// <summary>
        /// Initializes audio index file.
        /// </summary>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        private string InitializeIndex()
        {
                if (!string.IsNullOrEmpty(IndexFilenameInput) && !File.Exists(IndexFilenameInput))
                    return "Index file does not exist";

                if (string.IsNullOrEmpty(FilenameInput) && string.IsNullOrEmpty(IndexFilenameInput))
                    indexFile = new IdxFile(IndexFilenameOutput);
                else
                    indexFile = new IdxFile(IndexFilenameInput, IndexFilenameOutput);

            if (!indexFile.Initialized)
                return indexFile.Load();
            else
                return null;
        }

        /// <summary>
        /// Adds a wave audio file to audio bag & index.
        /// </summary>
        /// <param name="wavFile">Wave audio file to add to the bag & index.</param>
        /// <param name="overwriteExistingFiles">If set, existing files with same filename are overwritten.</param>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        public string AddFile(WavFile wavFile, bool overwriteExistingFiles = true)
        {

            if (indexFile == null || !indexFile.Initialized)
                return "Index file has not been initialized.";
            else if (!Initialized)
                return "Bag file has not been initialized.";

            if (wavFile == null)
                return "Wave audio file is invalid.";

            if (wavFile.Encoding == WavFileEncoding.Invalid)
                return "Wave audio has not been initialized.";

            int formatflag = 0;

            if (wavFile.Encoding == WavFileEncoding.PCM && wavFile.BitsPerSample == 8)
                formatflag = 2;
            else if (wavFile.Encoding == WavFileEncoding.PCM && wavFile.BitsPerSample == 16)
                formatflag = 6;
            else if (wavFile.Encoding == WavFileEncoding.IMA_ADPCM)
                formatflag = indexFile.Version == 4 ? 28 : 12;

            if (wavFile.Channels == 2)
                formatflag += 1;

            if (formatflag == 0 || formatflag == 1)
                return "Wave audio file format is invalid";

            string filename = Path.GetFileNameWithoutExtension(wavFile.Filename);

            if (overwriteExistingFiles && indexFile.GetFileHeader(filename) != null)
            {
                indexFile.RemoveFileHeader(filename);
                filesToAdd.Remove(filename);
            }

            filesToAdd.Add(filename, wavFile);
            indexFile.AddFileHeader(filename, -1, wavFile.AudioDataSize, wavFile.SampleRate, formatflag, wavFile.BlockSize);
            modifiedSinceLastSave = true;
            return null;
        }

        /// <summary>
        /// Gets a wave audio file from audio bag & index matching a given filename.
        /// </summary>
        /// <param name="filename">Filename of wave audio file, with or without file extension.</param>
        /// <returns>Wave audio file matching given filename, null if no match was found.</returns>
        public WavFile GetFile(string filename)
        {
            if (indexFile == null || !indexFile.Initialized || !Initialized || string.IsNullOrEmpty(filename))
                return null;

            string filenameNoExt = Path.GetFileNameWithoutExtension(filename);
            IdxFileHeader fileHeader = indexFile.GetFileHeader(filenameNoExt);

            if (fileHeader == null)
                return null;

            filesToAdd.TryGetValue(fileHeader.Filename, out WavFile wavFile);

            if (wavFile != null)
                wavFile.LoadData();

            else
            {
                byte[] data = GetAudioData(fileHeader.DataOffset, fileHeader.DataSize);
                wavFile = new WavFile(fileHeader.Filename + ".wav", fileHeader.SampleRate, fileHeader.FormatFlag, data, fileHeader.BlockSize);
            }

            if (wavFile.Initialized)
                return wavFile;
            else
                return null;
        }

        /// <summary>
        /// Gets a list containing files in audio bag & index.
        /// </summary>
        /// <returns>List containing all files in audio bag & index.</returns>
        public List<WavFile> GetAllFiles()
        {
            if (indexFile == null || !indexFile.Initialized || !Initialized)
                return new List<WavFile>(0);

            List<WavFile> wavFiles = new List<WavFile>();

            foreach (IdxFileHeader header in indexFile.Headers)
            {
                WavFile wavFile = GetFile(header.Filename);
                if (wavFile != null)
                    wavFiles.Add(wavFile);
            }

            return wavFiles;
        }


        /// <summary>
        /// Removes an audio file from audio bag & index matching a given filename.
        /// </summary>
        /// <param name="filename">Filename of wave audio file to remove, with or without file extension.</param>
        /// <returns>True if file was removed, otherwise false.</returns>
        public bool RemoveFile(string filename)
        {
            if (indexFile == null || !indexFile.Initialized || !Initialized || string.IsNullOrEmpty(filename))
                return false;

            string filenameNoExt = Path.GetFileNameWithoutExtension(filename);

            bool removed = indexFile.RemoveFileHeader(filenameNoExt);

            if (removed)
                filesToAdd.Remove(filenameNoExt);

            modifiedSinceLastSave = removed;
            return removed;
        }

        /// <summary>
        /// Gets audio data from the audio bag file.
        /// </summary>
        /// <param name="dataOffset">Start offset of the data in the data bag file.</param>
        /// <param name="dataSize">Size of the data in the data bag file.</param>
        /// <returns>Byte array of the data. If data cannot be found (invalid offset / size), then null.</returns>
        private byte[] GetAudioData(int dataOffset, int dataSize)
        {
            if (!Initialized || !File.Exists(FilenameInput))
                return null;

            byte[] data;

            try
            {
                data = new byte[dataSize];
                fsInput.Seek(dataOffset, SeekOrigin.Begin);
                fsInput.Read(data, 0, dataSize);
            }
            catch (Exception)
            {
                return null;
            }

            return data;
        }
    }
}

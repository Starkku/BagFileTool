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
using System.Text;
using System.IO;
using System.Collections.ObjectModel;

namespace BagFileTool.FileTypes
{
    /// <summary>
    /// Command & Conquer: Red Alert 2 audio index file.
    /// </summary>
    internal class IdxFile
    {
        /// <summary>
        /// True if the index file has been initialized. Otherwise false.
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Input filename of the index file.
        /// </summary>
        public string FilenameInput { get; private set; }

        /// <summary>
        /// Output filename of the index file.
        /// </summary>
        public string FilenameOutput { get; private set; }

        /// <summary>
        /// Index file format ID.
        /// </summary>
        public string ID { get; private set; } = "GABA";

        /// <summary>
        /// Index file version.
        /// </summary>
        public int Version { get; private set; } = 2;

        /// <summary>
        /// Number of file headers in the index file.
        /// </summary>
        public int FileCount => fileHeaders.Count;

        /// <summary>
        /// Read-only collection of file headers in the index file.
        /// </summary>
        public ReadOnlyCollection<IdxFileHeader> Headers => fileHeaders.AsReadOnly();

        private readonly List<IdxFileHeader> fileHeaders = new List<IdxFileHeader>();

        /// <summary>
        /// Initializes a new index file.
        /// </summary>
        /// <param name="filenameInput">Input filename of the audio index file.</param>
        /// <param name="filenameInput">Output filename of the audio index file. If not set, input filename is used as output.</param>
        public IdxFile(string filenameInput, string filenameOutput = null)
        {
            FilenameInput = filenameInput;

            if (!string.IsNullOrEmpty(filenameOutput))
                FilenameOutput = filenameOutput;
            else
                FilenameOutput = FilenameInput;
        }

        /// <summary>
        /// Initializes a new, empty index file.
        /// <paramref name="filenameOutput"/>
        /// </summary>
        public IdxFile(string filenameOutput) : this(null, filenameOutput)
        {
            Initialized = true;
        }

        /// <summary>
        /// Load an index file.
        /// </summary>
        /// <param name="filenameInput">Filename to use when loading the index file.</param>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        public string Load(string filenameInput = null)
        {
            Initialized = false;

            if (!string.IsNullOrEmpty(filenameInput))
                FilenameInput = filenameInput;

            byte[] buffer = new byte[4];
            byte[] fileheaderBuffer = new byte[36];
            int offset = 0;
            FileStream fs = null;

            try
            {
                fs = new FileStream(FilenameInput, FileMode.Open);
                fs.Read(buffer, 0, buffer.Length);
                offset += 4;
                ID = Encoding.ASCII.GetString(buffer, 0, buffer.Length);
                fs.Read(buffer, 0, buffer.Length);
                offset += 4;
                Version = BitConverter.ToInt32(buffer, 0);
                fs.Read(buffer, 0, buffer.Length);
                offset += 4;
                int fileCount = BitConverter.ToInt32(buffer, 0);

                if (Version == 4)
                {
                    fs.Read(buffer, 0, buffer.Length);
                    offset += 4;
                    fileheaderBuffer = new byte[64];

                    for (int i = 0; i < fileCount; i++)
                    {
                        fs.Read(fileheaderBuffer, 0, fileheaderBuffer.Length);
                        fileHeaders.Add(new IdxFileHeader(fileheaderBuffer));
                        offset += 64;
                    }
                }
                else
                {
                    for (int i = 0; i < fileCount; i++)
                    {
                        fs.Read(fileheaderBuffer, 0, fileheaderBuffer.Length);
                        fileHeaders.Add(new IdxFileHeader(fileheaderBuffer));
                        offset += 36;
                    }
                }

            }
            catch (Exception e)
            {
                return "Index file loading failed: " + e.Message;
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }

            Initialized = true;
            return null;
        }

        /// <summary>
        /// Save an index file.
        /// </summary>
        /// <param name="filenameOutput">Filename to use when saving the index file.</param>
        /// <param name="filenameMerge">Filename of bag file to merge to index.</param>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        public string Save(string filenameOutput = null, string filenameMerge = null)
        {
            if (!Initialized)
                return "Index file has not been initialized.";

            string filename = FilenameOutput;

            if (!string.IsNullOrEmpty(filenameOutput))
                filename = filenameOutput;

            bool merge = !string.IsNullOrEmpty(filenameMerge) && File.Exists(filenameMerge);
            string originalFilename = filename;
            filename = merge ? Path.GetTempFileName() : filename;

            FileStream fs = null;
            FileStream fsMerge = null;

            try
            {
                fs = new FileStream(filename, FileMode.Create);
                byte[] tmp = Encoding.ASCII.GetBytes(ID);
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(Version);
                fs.Write(tmp, 0, tmp.Length);
                tmp = BitConverter.GetBytes(FileCount);
                fs.Write(tmp, 0, tmp.Length);

                foreach (IdxFileHeader header in fileHeaders)
                {
                    byte[] headerData = header.GetHeaderData(Version);
                    fs.Write(headerData, 0, headerData.Length);
                }

                if (merge)
                {
                    fsMerge = new FileStream(filenameMerge, FileMode.Open);
                    fsMerge.CopyTo(fs);
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
            finally
            {
                if (fs != null)
                    fs.Close();

                if (fsMerge != null)
                    fsMerge.Close();
            }

            if (merge)
            {
                File.Copy(filename, originalFilename, true);
                File.Delete(filename);
            }

            return null;
        }

        /// <summary>
        /// Returns a file header matching the specified filename from the index file.
        /// </summary>
        /// <param name="filename">Filename of the file header in the index file to find.</param>
        /// <returns>Returns a file header from the index file matching the specified filename. Null if no matching header is found.</returns>
        public IdxFileHeader GetFileHeader(string filename)
        {
            IdxFileHeader header = null;

            foreach (IdxFileHeader h in fileHeaders)
            {
                if (h.Filename == filename)
                {
                    header = h;
                    break;
                }
            }

            return header;
        }

        /// <summary>
        /// Add a new file header to the index file.
        /// </summary>
        /// <param name="filename">Filename field for the new file header.</param>
        /// <param name="dataOffset">Audio offset field for the new file header.</param>
        /// <param name="dataSize">Audio data size field for the new file header.</param>
        /// <param name="sampleRate">Audio sample rate field for the new file header.</param>
        /// <param name="formatFlag">Audio format flag field for the new file header.</param>
        /// <param name="blockSize">Audio encoding block size field for the new file header.</param>
        public void AddFileHeader(string filename, int dataOffset, int dataSize, int sampleRate, int formatFlag, int blockSize)
        {
            IdxFileHeader newheader = new IdxFileHeader(filename, dataOffset, dataSize, sampleRate, formatFlag, blockSize);
            fileHeaders.Add(newheader);
        }

        /// <summary>
        /// Removes file header matching the specified filename from the index file.
        /// </summary>
        /// <param name="filename">Filename of the header to remove.</param>
        /// <returns>True if file header was removed, otherwise false.</returns>
        public bool RemoveFileHeader(string filename)
        {
            IdxFileHeader header = fileHeaders.Find(x => x.Filename.Equals(filename));

            if (header != null)
            {
                fileHeaders.Remove(header);
                return true;
            }
            else
                return false;
        }
    }

    /// <summary>
    /// RA2/YR/EBFD audio index file audio file header.
    /// </summary>
    internal class IdxFileHeader
    {
        /// <summary>
        /// True if the file header has been properly initialized. Otherwise false.
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Filename field of the header.
        /// </summary>
        public string Filename { get; private set; }

        /// <summary>
        /// Audio data offset field of the header.
        /// </summary>
        public int DataOffset { get; set; }

        /// <summary>
        /// Audio data size field of the header.
        /// </summary>
        public int DataSize { get; set; }

        /// <summary>
        /// Audio sample rate field of the header.
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Audio format flag field of the header.
        /// </summary>
        public int FormatFlag { get; private set; }

        /// <summary>
        /// Audio encoding block size field of the header.
        /// </summary>
        public int BlockSize { get; private set; }

        /// <summary>
        /// Initializes a new file header object from a byte array of file header data.
        /// </summary>
        /// <param name="fileHeaderData"></param>
        public IdxFileHeader(byte[] fileHeaderData)
        {
            if (fileHeaderData == null || fileHeaderData.Length < 1)
                return;

            ParseHeaderData(fileHeaderData);
        }

        /// <summary>
        /// Initializes a new file header object from parameters.
        /// </summary>
        /// <param name="filename">Filename field for the new file header.</param>
        /// <param name="dataOffset">Audio offset field for the new file header.</param>
        /// <param name="dataSize">Audio data size field for the new file header.</param>
        /// <param name="sampleRate">Audio sample rate field for the new file header.</param>
        /// <param name="formatFlag">Audio format flag field for the new file header.</param>
        /// <param name="blockSize">Audio encoding block size field for the new file header.</param>
        public IdxFileHeader(string filename, int dataOffset, int dataSize, int sampleRate, int formatFlag, int blockSize)
        {
            Filename = filename;
            DataOffset = dataOffset;
            DataSize = dataSize;
            SampleRate = sampleRate;
            FormatFlag = formatFlag;
            BlockSize = blockSize;
            Initialized = true;
        }

        /// <summary>
        /// Initializes a new, empty file header object.
        /// </summary>
        public IdxFileHeader()
        {
            Initialized = false;
        }

        /// <summary>
        /// Parses file header data from a byte array of file header data.
        /// </summary>
        /// <param name="fileHeaderData">Data for a file header in index file, in a byte array.</param>
        /// <returns>Error message if something went wrong. Otherwise null.</returns>
        public string ParseHeaderData(byte[] fileHeaderData)
        {
            Initialized = false;

            try
            {
                if (fileHeaderData.Length == 36)
                {
                    List<byte> tmp = new List<byte>();

                    for (int i = 0; i < fileHeaderData.Length; i++)
                    {
                        if (fileHeaderData[i] == 0)
                            break;
                        tmp.Add(fileHeaderData[i]);
                    }

                    byte[] fname = tmp.ToArray();
                    Filename = Encoding.ASCII.GetString(fname, 0, fname.Length);
                    DataOffset = BitConverter.ToInt32(fileHeaderData, 16);
                    DataSize = BitConverter.ToInt32(fileHeaderData, 20);
                    SampleRate = BitConverter.ToInt32(fileHeaderData, 24);
                    FormatFlag = BitConverter.ToInt32(fileHeaderData, 28);
                    BlockSize = BitConverter.ToInt32(fileHeaderData, 32);
                }
                else if (fileHeaderData.Length == 64)
                {
                    List<byte> tmp = new List<byte>();

                    for (int i = 0; i < fileHeaderData.Length; i++)
                    {
                        if (fileHeaderData[i] == 0)
                            break;
                        tmp.Add(fileHeaderData[i]);
                    }

                    byte[] fname = tmp.ToArray();
                    Filename = Encoding.ASCII.GetString(fname, 0, fname.Length);
                    DataOffset = BitConverter.ToInt32(fileHeaderData, 32);
                    DataSize = BitConverter.ToInt32(fileHeaderData, 36);
                    SampleRate = BitConverter.ToInt32(fileHeaderData, 40);
                    FormatFlag = BitConverter.ToInt32(fileHeaderData, 44);
                    BlockSize = BitConverter.ToInt32(fileHeaderData, 48);
                }
                else
                    return "File header length is not valid";
            }
            catch (Exception e)
            {
                return e.Message;
            }

            Initialized = true;
            return null;
        }

        /// <summary>
        /// Returns the file header fields in a byte array.
        /// </summary>
        /// <param name="version">Index file format version number.</param>
        /// <returns>Byte array containing the file header data, or null in case something goes wrong.</returns>
        public byte[] GetHeaderData(int version)
        {
            if (!Initialized)
                return null;

            List<byte> data = new List<byte>();
            int dataLength = version == 4 ? 64 : 36;

            try
            {
                byte[] filename = Encoding.ASCII.GetBytes(Filename);
                int filenameMaxLength = version == 4 ? 32 : 16;
                int d = filenameMaxLength - filename.Length;
                data.AddRange(filename);

                for (int i = 0; i < d; i++)
                    data.Add(0);

                data.AddRange(BitConverter.GetBytes(DataOffset));
                data.AddRange(BitConverter.GetBytes(DataSize));
                data.AddRange(BitConverter.GetBytes(SampleRate));
                data.AddRange(BitConverter.GetBytes(FormatFlag));
                data.AddRange(BitConverter.GetBytes(BlockSize));

                if (data.Count < dataLength)
                {
                    int paddingCount = dataLength - data.Count;
                    for (int i = 0; i < paddingCount; i++)
                        data.Add(0);
                }
            }
            catch (Exception)
            {
                return null;
            }

            if (data.Count != dataLength)
                return null;

            return data.ToArray();
        }
    }
}

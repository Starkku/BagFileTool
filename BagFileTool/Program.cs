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
using BagFileTool.FileTypes;
using BagFileTool.Utility;
using NDesk.Options;
using Starkku.Utilities;

namespace BagFileTool
{
    static class Program
    {
        private static OptionSet options;
        private static Settings settings = new Settings();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            options = new OptionSet
                {
                    { "h|?|help", "Show help.", v => settings.ShowHelp = true},
                    { "i|input-filename=", "Input filemame.", v => settings.FilenameInput = v},
                    { "o|output-filename=", "Output filename.", v => settings.FilenameOutput = v},
                    { "a|add-files=", "Comma-separated list of files (and / or directories - all files contained within will be added recursively) to add to bag file.", v => settings.FilesToAdd = GetFilesToAdd(v)},
                    { "e|extract-files=", "Comma-separated list of filenames (without extension) to extract from bag file. If empty or wildcard (*), all files are extracted.", v => settings.FilesToExtract = GetFilesToExtract(v)},
                    { "l|log", "If set, writes a log to a file in program directory.", v => settings.WriteLogFile = true},
                    { "d|debug", "If set, shows debug-level logging in console window.", v => settings.ShowDebugLogging = true}
                };

            try
            {
                options.Parse(args);
            }
            catch (Exception e)
            {
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Encountered an error while parsing command-line parameters. Message: " + e.Message);
                Console.ResetColor();
                ShowHelp();
                return;
            }

            Logger.Initialize(settings.WriteLogFile, settings.ShowDebugLogging);

            if (settings.FilesToAdd != null && settings.FilesToExtract != null)
            {
                Logger.Error("Only either -a or -e is allowed as a parameter, not both.");
                ShowHelp();
                return;
            }
            else if (settings.FilesToAdd == null && settings.FilesToExtract == null)
            {
                Logger.Error("Not enough parameters.");
                ShowHelp();
                return;
            }

            if (settings.FilesToExtract != null)
                settings.ExtractDirectory = settings.FilenameOutput;

            bool validPaths = PathHelper.GetFilePaths(settings.FilenameInput, settings.FilenameOutput, settings.FilesToAdd != null && settings.FilesToAdd.Length > 0,
                out string bagInputFilename, out string bagOutputFilename, out string indexInputFilename, out string indexOutputFilename);

            if (!validPaths)
            {
                ShowHelp();
                return;
            }

            BagFile bagFile = new BagFile(bagInputFilename, bagOutputFilename, indexInputFilename, indexOutputFilename);
            string errorMsg = bagFile.Initialize();

            if (errorMsg != null)
            {
                Logger.Error(errorMsg);
                return;
            }

            if (settings.FilesToAdd != null && settings.FilesToAdd.Length > 0)
            {
                Logger.Info("Adding audio files to bag & index.");

                foreach (string filename in settings.FilesToAdd)
                {
                    WavFile wavFile = new WavFile(filename);
                    errorMsg = wavFile.LoadHeader();

                    if (errorMsg != null)
                    {
                        Logger.Warn("Could not load file: " + filename + ". Error message: " + errorMsg);
                        continue;
                    }

                    errorMsg = bagFile.AddFile(wavFile);

                    if (errorMsg != null)
                    {
                        Logger.Warn("Could not add file: " + filename + ". Error message: " + errorMsg);
                        continue;
                    }
                    else
                    {
                        Logger.Info("Added file: " + filename + "");
                    }
                }
                errorMsg = bagFile.Save();

                if (errorMsg != null)
                    Logger.Error(errorMsg);
                else
                    Logger.Info("Bag & index files successfully saved.");
            }
            else if (settings.FilesToExtract != null)
            {
                Logger.Info("Extracting audio files from bag & index.");

                string extractDirectory = settings.ExtractDirectory.EndsWith("\\") || settings.ExtractDirectory.EndsWith("//") ?
                    settings.ExtractDirectory : settings.ExtractDirectory + Path.DirectorySeparatorChar;

                extractDirectory = Path.GetDirectoryName(extractDirectory);

                if (!Directory.Exists(extractDirectory))
                {
                    Logger.Warn("Output directory '" + extractDirectory + "' does not exist - attempting to create.");
                    try
                    {
                        Directory.CreateDirectory(extractDirectory);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Creating directory '" + extractDirectory + "' failed: " + e.Message);
                    }
                }

                List<WavFile> files = new List<WavFile>();

                if (settings.FilesToExtract.Length < 1 || (settings.FilesToExtract.Length == 1 && (string.IsNullOrEmpty(settings.FilesToExtract[0]) || settings.FilesToExtract[0].Equals("*"))))
                {
                    files.AddRange(bagFile.GetAllFiles());
                }
                else
                {
                    foreach (string filename in settings.FilesToExtract)
                    {
                        if (string.IsNullOrEmpty(filename))
                            continue;

                        WavFile wavFile = bagFile.GetFile(filename);

                        if (wavFile != null)
                            files.Add(wavFile);
                    }
                }

                foreach (WavFile wavFile in files)
                {
                    errorMsg = wavFile.Save(Path.Combine(extractDirectory, Path.GetFileName(wavFile.Filename)));

                    if (errorMsg != null)
                    {
                        Logger.Warn("Could not extract file: " + wavFile.Filename + ". Error message: " + errorMsg);
                        continue;
                    }

                    Logger.Info("Extracted file: " + wavFile.Filename + "");
                }
            }
        }

        private static string[] GetFilesToAdd(string fileNames)
        {
            if (string.IsNullOrEmpty(fileNames))
                return null;

            List<string> allowedExtensions = new List<string>() { ".wav" };
            List<string> filenames = new List<string>();

            string[] split = fileNames.Split(new string[] { ",", }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string splitStr in split)
            {
                string filename = splitStr.Trim();
                string extension = Path.GetExtension(filename);

                if (File.Exists(filename) && allowedExtensions.Contains(extension))
                    filenames.Add(filename);
                else if (Directory.Exists(filename))
                    filenames.AddRange(FileSystem.GetFilesMatchingExtensions(filename, allowedExtensions, true));
            }

            return filenames.ToArray();
        }

        private static string[] GetFilesToExtract(string fileNames)
        {
            if (fileNames == null)
                return null;
            if (fileNames == string.Empty)
                return new string[0];

            string[] split = fileNames.Split(new string[] { ",", }, StringSplitOptions.RemoveEmptyEntries);
            string[] filenames = new string[split.Length];

            for (int i = 0; i < split.Length; i++)
            {
                filenames[i] = split[i].Trim();
            }

            return filenames;
        }

        /// <summary>
        /// Shows help for command line arguments.
        /// </summary>
        private static void ShowHelp()
        {
            Console.ResetColor();
            Console.Write("Usage: ");
            Console.WriteLine("");
            var sb = new System.Text.StringBuilder();
            var sw = new StringWriter(sb);
            options.WriteOptionDescriptions(sw);
            Console.WriteLine(sb.ToString());
        }
    }
}

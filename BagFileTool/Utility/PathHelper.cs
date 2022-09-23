/*
 * Copyright 2017-2022 by Starkku
 * This file is part of BagFileTool, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see LICENSE.txt.
 */

using System.IO;
using Starkku.Utilities;

namespace BagFileTool.Utility
{
    /// <summary>
    /// Helper class for input / output path handling.
    /// </summary>
    public class PathHelper
    {
        /// <summary>
        /// Verifies and sets file paths.
        /// </summary>
        /// <param name="filenameInput">Input filename.</param>
        /// <param name="filenameOutput">Output filename.</param>
        /// <param name="allowSkipInput">Allow input path to not be set.</param>
        /// <param name="bagInputFilename">Will be set to bag file input filename.</param>
        /// <param name="bagOutputFilename">Will be set to bag file output filename.</param>
        /// <param name="indexInputFilename">Will be set to index file input filename.</param>
        /// <param name="indexOutputFilename">Will be set to index file output filename.</param>
        /// <returns>True if paths are valid, false if not.</returns>
        public static bool GetFilePaths(string filenameInput, string filenameOutput, bool allowSkipInput, out string bagInputFilename,
            out string bagOutputFilename, out string indexInputFilename, out string indexOutputFilename)
        {
            Logger.Info("Verifying & setting input & output file paths.");

            bool createNew = false;
            bool useInputAsOutput = false;

            bagInputFilename = null;
            bagOutputFilename = null;
            indexInputFilename = null;
            indexOutputFilename = null;

            bool inputIsOK;
            if (allowSkipInput && (string.IsNullOrEmpty(filenameInput) || !File.Exists(filenameInput)))
            {
                createNew = true;
                inputIsOK = true;
            }
            else if (string.IsNullOrEmpty(filenameInput) || !File.Exists(filenameInput))
            {
                Logger.Error("Specified input file does not exist.");
                return false;
            }
            else
            {
                inputIsOK = true;
                Logger.Info("Input file path is OK.");
            }

            bool outputIsOK;
            if (string.IsNullOrEmpty(filenameOutput))
            {
                if (inputIsOK && !createNew)
                {
                    Logger.Warn("Output file path is invalid - using input file path as output file path.");
                    outputIsOK = true;
                    useInputAsOutput = true;
                }
                else
                {
                    Logger.Error("Specified output file file path is invalid.");
                    return false;
                }
            }
            else
            {
                outputIsOK = true;
                Logger.Info("Output file path is OK.");
            }

            if (!createNew && inputIsOK)
            {
                string baseInputPath = Path.Combine(Path.GetDirectoryName(filenameInput), Path.GetFileNameWithoutExtension(filenameInput));
                bagInputFilename = Path.ChangeExtension(baseInputPath, ".bag");
                indexInputFilename = Path.ChangeExtension(baseInputPath, ".idx");
            }

            if (outputIsOK)
            {
                if (useInputAsOutput)
                {
                    bagOutputFilename = bagInputFilename;
                    indexOutputFilename = indexInputFilename;
                }
                else
                {
                    string baseOutputPath = Path.Combine(Path.GetDirectoryName(filenameOutput), Path.GetFileNameWithoutExtension(filenameOutput));
                    bagOutputFilename = Path.ChangeExtension(baseOutputPath, ".bag");
                    indexOutputFilename = Path.ChangeExtension(baseOutputPath, ".idx");
                }
            }

            return inputIsOK && outputIsOK;
        }
    }
}

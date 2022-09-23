/*
 * Copyright 2017-2022 by Starkku
 * This file is part of BagFileTool, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see LICENSE.txt.
 */

namespace BagFileTool.Utility
{
    /// <summary>
    /// Program settings.
    /// </summary>
    struct Settings
    {
        /// <summary>
        /// Show help on usage on startup.
        /// </summary>
        public bool ShowHelp { get; set; }

        /// <summary>
        /// Input filename.
        /// </summary>
        public string FilenameInput { get; set; }

        /// <summary>
        /// Output filename.
        /// </summary>
        public string FilenameOutput { get; set; }

        /// <summary>
        /// Files to add to bag / index file.
        /// </summary>
        public string[] FilesToAdd { get; set; }

        /// <summary>
        /// Files to extract from bag / index file. If empty but not null, all files are extracted.
        /// </summary>
        public string[] FilesToExtract { get; set; }

        /// <summary>
        /// If set, writes a log file.
        /// </summary>
        public bool WriteLogFile { get; set; }

        /// <summary>
        /// If set, shows debug-level logging in console.
        /// </summary>
        public bool ShowDebugLogging { get; set; }

        /// <summary>
        /// Output directory for extracted files.
        /// </summary>
        public string ExtractDirectory { get; set; }
    }
}

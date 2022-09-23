# BagFileTool

Command line program for creating and modifying `idx` and `bag` files from Westwood Studios games as well as extracting their contents.

Currently only the formats from Command & Conquer: Red Alert 2 and Emperor: Battle for Dune are supported. However MPEG-3 audio file support for latter is not implemented currently.

Latest automatically generated release can be found [here](https://github.com/Starkku/BagFileTool/releases/tag/latest).

Accepted parameters:
```
-h, -?, --help                Show help.
-i, --input-filename=VALUE    Input filemame.
-o, --output-filename=VALUE   Output filename.
-a, --add-files=VALUE         Comma-separated list of files (and / or directories - all files contained within will be added recursively) to add to bag file.
-e, --extract-files=VALUE     Comma-separated list of filenames (without extension) to extract from bag file. If empty or wildcard (*), all files are extracted.
-l, --log                     If set, writes a log to a file in program directory.
-d, --debug                   If set, shows debug-level logging in console window.
```

## Acknowledgements

BagFileTool uses code from the following open-source projects to make its functionality possible.

* Starkku.Utilities: https://github.com/Starkku/Starkku.Utilities
* NDesk.Options: http://www.ndesk.org/Options

## License

This program is licensed under GPL Version 3 or any later version.

See [LICENSE.txt](LICENSE.txt) for more information.

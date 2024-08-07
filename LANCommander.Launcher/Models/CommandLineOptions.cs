﻿using CommandLine;
using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    [Verb("RunScript", HelpText = "Run a script for a game")]
    public class RunScriptCommandLineOptions
    {
        [Option("GameId", HelpText = "The GUID of the installed game")]
        public Guid GameId { get; set; }

        [Option("InstallDirectory", HelpText = "The base directory in which the installed game is located")]
        public string InstallDirectory { get; set; }

        [Option("Type", HelpText = "The type of script to run")]
        public ScriptType Type { get; set; }

        [Option("NewName", HelpText = "The new name to use in a name change script")]
        public string NewName { get; set; }

        [Option("NewKey", HelpText = "The new key to use in a key change script")]
        public string NewKey { get; set; }
    }

    [Verb("Import", HelpText = "Import library items from the server")]
    public class ImportCommandLineOptions
    {

    }
}

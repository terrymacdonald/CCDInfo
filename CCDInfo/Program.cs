﻿using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using DisplayMagicianShared.Windows;
using NLog.Config;
using DisplayMagicianShared;

namespace CCDInfo
{    

    class Program
    {  

        static WINDOWS_DISPLAY_CONFIG myDisplayConfig = new WINDOWS_DISPLAY_CONFIG();        

        static void Main(string[] args)
        {

            // Prepare NLog for logging
            var config = new NLog.Config.LoggingConfiguration();

            // Targets where to log to: File and Console
            //string date = DateTime.Now.ToString("yyyyMMdd.HHmmss");
            string AppLogFilename = Path.Combine($"CCDInfo.log");

            // Rules for mapping loggers to targets          
            NLog.LogLevel logLevel = NLog.LogLevel.Trace;            

            // Create the log file target
            var logfile = new NLog.Targets.FileTarget("logfile")
            {
                FileName = AppLogFilename,
                DeleteOldFileOnStartup = true
            };

            // Create a logging rule to use the log file target
            var loggingRule = new LoggingRule("LogToFile");
            loggingRule.EnableLoggingForLevels(logLevel, NLog.LogLevel.Fatal);
            loggingRule.Targets.Add(logfile);
            loggingRule.LoggerNamePattern = "*";
            config.LoggingRules.Add(loggingRule);

            // Apply config           
            NLog.LogManager.Configuration = config;
            
            // Start the Log file
            SharedLogger.logger.Info($"CCDInfo/Main: Starting CCDInfo v1.0.5");

            
            Console.WriteLine($"\nCCDInfo v1.0.5");
            Console.WriteLine($"==============");
            Console.WriteLine($"By Terry MacDonald 2021\n");

            if (args.Length > 0)
            {
                if (args[0] == "save")
                {
                    SharedLogger.logger.Debug($"CCDInfo/Main: Attempting to save the display settings to {args[1]} as save command was provided");
                    if (args.Length != 2)
                    {
                        Console.WriteLine($"ERROR - You need to provide a filename in which to save display settings");
                        SharedLogger.logger.Error($"CCDInfo/Main: ERROR - You need to provide a filename in which to save display settings");
                        Environment.Exit(1);
                    }
                    saveToFile(args[1]);
                    if (!File.Exists(args[1]))
                    {
                        Console.WriteLine($"ERROR - Couldn't save settings to the file {args[1]}");
                        SharedLogger.logger.Error($"CCDInfo/Main: ERROR - Couldn't save settings to the file {args[1]}");
                        Environment.Exit(1);
                    }                    
                }
                else if (args[0] == "load")
                {
                    SharedLogger.logger.Debug($"CCDInfo/Main: Attempting to use the display settings in {args[1]} as load command was provided");
                    if (args.Length != 2)
                    {
                        Console.WriteLine($"ERROR - You need to provide a filename from which to load display settings");
                        SharedLogger.logger.Error($"CCDInfo/Main: ERROR - You need to provide a filename from which to load display settings");
                        Environment.Exit(1);
                    }
                    if (!File.Exists(args[1]))
                    {
                        Console.WriteLine($"ERROR - Couldn't find the file {args[1]} to load settings from it");
                        SharedLogger.logger.Error($"CCDInfo/Main: ERROR - Couldn't find the file {args[1]} to load settings from it");
                        Environment.Exit(1);
                    }
                    loadFromFile(args[1]);
                }
                else if (args[0] == "possible")
                {
                    SharedLogger.logger.Debug($"CCDInfo/Main: showing if the {args[1]} is a valid display cofig file as possible command was provided");
                    if (args.Length != 2)
                    {
                        Console.WriteLine($"ERROR - You need to provide a filename from which we will check if the display settings are possible");
                        SharedLogger.logger.Error($"CCDInfo/Main: ERROR - You need to provide a filename from which we will check if the display settings are possible");
                        Environment.Exit(1);
                    }
                    if (!File.Exists(args[1]))
                    {
                        Console.WriteLine($"ERROR - Couldn't find the file {args[1]} to check the settings from it");
                        SharedLogger.logger.Error($"CCDInfo/Main: ERROR - Couldn't find the file {args[1]} to check the settings from it");
                        Environment.Exit(1);
                    }
                    possibleFromFile(args[1]);
                }
                else if (args[0] == "currentids")
                {
                    SharedLogger.logger.Debug($"CCDInfo/Main: showing currently connected display ids as currentids command was provided"); 
                    Console.WriteLine("The current display identifiers are:");
                    SharedLogger.logger.Info($"CCDInfo/Main: The current display identifiers are:");
                    foreach (string displayId in WinLibrary.GetLibrary().GetCurrentDisplayIdentifiers())
                    {
                        Console.WriteLine(@displayId);
                        SharedLogger.logger.Info($@"{displayId}");
                    }
                }
                else if (args[0] == "allids")
                {
                    SharedLogger.logger.Debug($"CCDInfo/Main: showing all display ids as allids command was provided");
                    Console.WriteLine("All connected display identifiers are:");
                    SharedLogger.logger.Info($"CCDInfo/Main: All connected display identifiers are:");
                    foreach (string displayId in WinLibrary.GetLibrary().GetAllConnectedDisplayIdentifiers())
                    {
                        Console.WriteLine(@displayId);
                        SharedLogger.logger.Info($@"{displayId}");
                    }
                }
                else if (args[0] == "help" || args[0] == "--help" || args[0] == "-h" || args[0] == "/?" || args[0] == "-?")
                {
                    SharedLogger.logger.Debug($"CCDInfo/Main: Showing help as help command was provided");
                    showHelp();
                    Environment.Exit(1);
                }
                else
                {
                    SharedLogger.logger.Debug($"CCDInfo/Main: Showing help as an invalid command was provided");                    
                    showHelp();
                    Console.WriteLine("*** ERROR - Invalid command line parameter provided! ***\n");
                    Environment.Exit(1);
                }
            }
            else
            {
                SharedLogger.logger.Debug($"CCDInfo/Main: Attempting to show information about the current display settings as no command was provided");
                // We're in display current config mode
                Console.WriteLine(WinLibrary.GetLibrary().PrintActiveConfig());
            }
            Console.WriteLine();
            Environment.Exit(0);
        }

        static void showHelp()
        {
            Console.WriteLine($"CCDInfo is a little program to help test setting display layout and HDR settings in Windows 10 64-bit and later.\n");
            Console.WriteLine($"You can run it without any command line parameters, and it will print all the information it can find from the \nWindows Display CCD interface.\n");
            Console.WriteLine($"You can also run it with 'CCDInfo save myfilename.cfg' and it will save the current display configuration into\nthe myfilename.cfg file.\n");
            Console.WriteLine($"This is most useful when you subsequently use the 'CCDInfo load myfilename.cfg' command, as it will load the\ndisplay configuration from the myfilename.cfg file and make it live. In this way, you can make yourself a library\nof different cfg files with different display layouts, then use the CCDInfo load command to swap between them.\n\n");
            Console.WriteLine($"Valid commands:\n");
            Console.WriteLine($"\t'CCDInfo' will print information about your current display setting.");
            Console.WriteLine($"\t'CCDInfo save myfilename.cfg' will save your current display setting to the myfilename.cfg file.");
            Console.WriteLine($"\t'CCDInfo load myfilename.cfg' will load and apply the display setting in the myfilename.cfg file.");
            Console.WriteLine($"\t'CCDInfo possible myfilename.cfg' will test the display setting in the myfilename.cfg file to see\n\t\tif it is possible.");
            Console.WriteLine($"\nUse DisplayMagician to store display settings for each game you have. https://github.com/terrymacdonald/DisplayMagician\n");
        }

        static void saveToFile(string filename)
        {
            SharedLogger.logger.Trace($"CCDInfo/saveToFile: Attempting to save the current display configuration to the {filename}.");

            SharedLogger.logger.Trace($"CCDInfo/saveToFile: Getting the current Active Config");
            // Get the current configuration
            myDisplayConfig = WinLibrary.GetLibrary().GetActiveConfig();

            SharedLogger.logger.Trace($"CCDInfo/saveToFile: Attempting to convert the current Active Config objects to JSON format");
            // Save the object to file!
            try
            {
                SharedLogger.logger.Trace($"CCDInfo/saveToFile: Attempting to convert the current Active Config objects to JSON format");

                var json = JsonConvert.SerializeObject(myDisplayConfig, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Populate,
                    TypeNameHandling = TypeNameHandling.Auto

                });


                if (!string.IsNullOrWhiteSpace(json))
                {
                    SharedLogger.logger.Error($"CCDInfo/saveToFile: Saving the display settings to {filename}.");

                    File.WriteAllText(filename, json, Encoding.Unicode);

                    SharedLogger.logger.Error($"CCDInfo/saveToFile: Display settings successfully saved to {filename}.");
                    Console.WriteLine($"Display settings successfully saved to {filename}.");
                }
                else
                {
                    SharedLogger.logger.Error($"CCDInfo/saveToFile: The JSON string is empty after attempting to convert the current Active Config objects to JSON format");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CCDInfo/saveToFile: ERROR - Unable to save the profile repository to the {filename}.");
                SharedLogger.logger.Error(ex, $"CCDInfo/saveToFile: Saving the display settings to the {filename}.");
            }
        }

        static void loadFromFile(string filename)
        {
            string json = "";
            try
            {
                SharedLogger.logger.Trace($"CCDInfo/loadFromFile: Attempting to load the display configuration from {filename} to use it.");
                json = File.ReadAllText(filename, Encoding.Unicode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CCDInfo/loadFromFile: ERROR - Tried to read the JSON file {filename} to memory but File.ReadAllTextthrew an exception.");
                SharedLogger.logger.Error(ex, $"CCDInfo/loadFromFile: Tried to read the JSON file {filename} to memory but File.ReadAllTextthrew an exception.");
            }

            if (!string.IsNullOrWhiteSpace(json))
            {
                SharedLogger.logger.Trace($"CCDInfo/loadFromFile: Contents exist within {filename} so trying to read them as JSON.");
                try
                {
                    myDisplayConfig = JsonConvert.DeserializeObject<WINDOWS_DISPLAY_CONFIG>(json, new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore,
                        DefaultValueHandling = DefaultValueHandling.Include,
                        TypeNameHandling = TypeNameHandling.Auto,
                        ObjectCreationHandling = ObjectCreationHandling.Replace
                    });
                    SharedLogger.logger.Trace($"CCDInfo/loadFromFile: Successfully parsed {filename} as JSON.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CCDInfo/loadFromFile: ERROR - Tried to parse the JSON in the {filename} but the JsonConvert threw an exception.");
                    SharedLogger.logger.Error(ex,$"CCDInfo/loadFromFile: Tried to parse the JSON in the {filename} but the JsonConvert threw an exception.");
                }

                if (!WinLibrary.GetLibrary().IsActiveConfig(myDisplayConfig))
                {
                    if (WinLibrary.GetLibrary().IsPossibleConfig(myDisplayConfig))
                    {
                        SharedLogger.logger.Trace($"CCDInfo/loadFromFile: The display settings within {filename} are possible to use right now, so we'll use attempt to use them.");
                        Console.WriteLine($"Attempting to apply display config from {filename}");
                        WinLibrary.GetLibrary().SetActiveConfig(myDisplayConfig);
                        SharedLogger.logger.Trace($"CCDInfo/loadFromFile: The display settings within {filename} were successfully applied.");
                        Console.WriteLine($"Display config successfully applied");
                    }
                    else
                    {
                        Console.WriteLine($"CCDInfo/loadFromFile: ERROR - Cannot apply the display config in {filename} as it is not currently possible to use it.");
                        SharedLogger.logger.Error($"CCDInfo/loadFromFile: ERROR - Cannot apply the display config in {filename} as it is not currently possible to use it.");
                    }
                }
                else
                {
                    Console.WriteLine($"The display settings in {filename} are already installed. No need to install them again. Exiting.");
                    SharedLogger.logger.Info($"CCDInfo/loadFromFile: The display settings in {filename} are already installed. No need to install them again. Exiting.");
                }                

            }
            else
            {
                Console.WriteLine($"CCDInfo/loadFromFile: ERROR - The {filename} profile JSON file exists but is empty! So we're going to treat it as if it didn't exist.");
                SharedLogger.logger.Error($"CCDInfo/loadFromFile: The {filename} profile JSON file exists but is empty! So we're going to treat it as if it didn't exist.");
            }
        }

        static void possibleFromFile(string filename)
        {
            string json = "";
            try
            {
                SharedLogger.logger.Trace($"CCDInfo/possibleFromFile: Attempting to load the display configuration from {filename} to see if it's possible.");
                json = File.ReadAllText(filename, Encoding.Unicode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CCDInfo/possibleFromFile: ERROR - Tried to read the JSON file {filename} to memory but File.ReadAllTextthrew an exception.");
                SharedLogger.logger.Error(ex, $"CCDInfo/possibleFromFile: Tried to read the JSON file {filename} to memory but File.ReadAllTextthrew an exception.");
            }

            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    SharedLogger.logger.Trace($"CCDInfo/possibleFromFile: Contents exist within {filename} so trying to read them as JSON.");
                    myDisplayConfig = JsonConvert.DeserializeObject<WINDOWS_DISPLAY_CONFIG>(json, new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore,
                        DefaultValueHandling = DefaultValueHandling.Include,
                        TypeNameHandling = TypeNameHandling.Auto,
                        ObjectCreationHandling = ObjectCreationHandling.Replace
                    });
                    SharedLogger.logger.Trace($"CCDInfo/possibleFromFile: Successfully parsed {filename} as JSON.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CCDInfo/possibleFromFile: ERROR - Tried to parse the JSON in the {filename} but the JsonConvert threw an exception.");
                    SharedLogger.logger.Error(ex, $"CCDInfo/possibleFromFile: Tried to parse the JSON in the {filename} but the JsonConvert threw an exception.");
                }

                if (WinLibrary.GetLibrary().IsPossibleConfig(myDisplayConfig))
                {
                    SharedLogger.logger.Trace($"CCDInfo/possibleFromFile: The display settings in {filename} are able to be applied on this computer if you'd like to apply them.");
                    Console.WriteLine($"The display settings in {filename} are able to be applied on this computer if you'd like to apply them.");
                    Console.WriteLine($"You can apply them with the command 'CCDInfo load {filename}'");
                }
                else
                {
                    SharedLogger.logger.Trace($"CCDInfo/possibleFromFile: The {filename} file contains a display setting that will NOT work on this computer right now.");
                    SharedLogger.logger.Trace($"CCDInfo/possibleFromFile: This may be because the required screens are turned off, or some other change has occurred on the PC.");
                    Console.WriteLine($"The {filename} file contains a display setting that will NOT work on this computer right now.");
                    Console.WriteLine($"This may be because the required screens are turned off, or some other change has occurred on the PC.");
                }

            }
            else
            {
                SharedLogger.logger.Error($"CCDInfo/possibleFromFile: The {filename} profile JSON file exists but is empty! So we're going to treat it as if it didn't exist.");
                Console.WriteLine($"CCDInfo/possibleFromFile: The {filename} profile JSON file exists but is empty! So we're going to treat it as if it didn't exist.");
            }
        }
    }
    
}

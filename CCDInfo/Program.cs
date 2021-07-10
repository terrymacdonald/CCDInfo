using Newtonsoft.Json;
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

        WinLibrary winLibrary = new WinLibrary();
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static SharedLogger sharedLogger;


        static void Main(string[] args)
        {

            // Prepare NLog for logging

            //NLog.Common.InternalLogger.LogLevel = NLog.LogLevel.Debug;
            //NLog.Common.InternalLogger.LogToConsole = true;
            //NLog.Common.InternalLogger.LogFile = "C:\\Users\\terry\\AppData\\Local\\DisplayMagician\\Logs\\nlog-internal.txt";

            var config = new NLog.Config.LoggingConfiguration();

            // Targets where to log to: File and Console
            //string date = DateTime.Now.ToString("yyyyMMdd.HHmmss");
            string AppLogFilename = Path.Combine($"CCDInfo.log");

            // Rules for mapping loggers to targets          
            NLog.LogLevel logLevel = NLog.LogLevel.Info;            

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

            // Make DisplayMagicianShared use the same log file by sending it the 
            // details of the existing NLog logger
            sharedLogger = new SharedLogger(logger);

            // Start the Log file
            logger.Info($"Starting CCDInfo v1.0.5");

            
            Console.WriteLine($"CCDInfo v1.0.5");
            Console.WriteLine($"==============");
            Console.WriteLine($"By Terry MacDonald 2021\n");

            if (args.Length > 0)
            {
                if (args[0] == "save")
                {                    
                    if (args.Length != 2)
                    {
                        Console.WriteLine($"ERROR - You need to provide a filename in which to save display settings");
                        Environment.Exit(1);
                    }
                    saveToFile(args[1]);
                    if (!File.Exists(args[1]))
                    {
                        Console.WriteLine($"ERROR - Couldn't save settings to the file {args[1]}");
                        Environment.Exit(1);
                    }                    
                }
                else if (args[0] == "load")
                {
                    if (args.Length != 2)
                    {
                        Console.WriteLine($"ERROR - You need to provide a filename from which to load display settings");
                        Environment.Exit(1);
                    }
                    if (!File.Exists(args[1]))
                    {
                        Console.WriteLine($"ERROR - Couldn't find the file {args[1]} to load settings from it");
                        Environment.Exit(1);
                    }
                    loadFromFile(args[1]);
                }
                else if (args[0] == "currentids")
                {
                    Console.WriteLine("The current display identifiers are:");
                    foreach (string displayId in WinLibrary.GetLibrary().GetCurrentDisplayIdentifiers())
                    {
                        Console.WriteLine(@displayId);
                    }
                }
                else if (args[0] == "allids")
                {
                    Console.WriteLine("All connected display identifiers are:");
                    foreach (string displayId in WinLibrary.GetLibrary().GetAllConnectedDisplayIdentifiers())
                    {
                        Console.WriteLine(@displayId);
                    }
                }
                else if (args[0] == "help" || args[0] == "--help" || args[0] == "-h" || args[0] == "/?" || args[0] == "-?")
                {
                    Console.WriteLine($"CCDInfo is a little program to help test setting display layout and HDR settings in Windows 10 64-bit and later.\n");
                    Console.WriteLine($"You can run it without any command line parameters, and it will print all the information it can find from the \nWindows Display CCD interface.\n");
                    Console.WriteLine($"You can also run it with 'CCDInfo save myfilename.cfg' and it will save the current display configuration into\nthe myfilename.cfg file.\n");
                    Console.WriteLine($"This is most useful when you subsequently use the 'CCDInfo load myfilename.cfg' command, as it will load the\ndisplay configuration from the myfilename.cfg file and make it live.");
                    Console.WriteLine($"In this way, you can make yourself a library of different cfg files with different display layouts, then use\nthe CCDInfo load command to swap between them.");
                    Environment.Exit(1);
                }
            }
            else
            {
                // We're in display current config mode
                WinLibrary.GetLibrary().PrintActiveConfig();
            }
            Environment.Exit(0);
        }

        
        static void saveToFile(string filename)
        {
            Console.WriteLine($"ProfileRepository/SaveProfiles: Attempting to save the profiles repository to the {filename}.");

            myDisplayConfig = WinLibrary.GetLibrary().GetActiveConfig();

            // Save the object to file!
            try
            {
                Console.WriteLine($"ProfileRepository/SaveProfiles: Converting the objects to JSON format.");

                var json = JsonConvert.SerializeObject(myDisplayConfig, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Populate,
                    TypeNameHandling = TypeNameHandling.Auto

                });


                if (!string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine($"ProfileRepository/SaveProfiles: Saving the profile repository to the {filename}.");

                    File.WriteAllText(filename, json, Encoding.Unicode);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProfileRepository/SaveProfiles: Unable to save the profile repository to the {filename}.");
            }
        }

        static void loadFromFile(string filename)
        {
            string json = "";
            try
            {
                json = File.ReadAllText(filename, Encoding.Unicode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProfileRepository/LoadProfiles: Tried to read the JSON file {filename} to memory but File.ReadAllTextthrew an exception.");
            }

            if (!string.IsNullOrWhiteSpace(json))
            {
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ProfileRepository/LoadProfiles: Tried to parse the JSON in the {filename} but the JsonConvert threw an exception.");
                }

                WinLibrary.GetLibrary().SetActiveConfig(myDisplayConfig);

            }
            else
            {
                Console.WriteLine($"ProfileRepository/LoadProfiles: The {filename} profile JSON file exists but is empty! So we're going to treat it as if it didn't exist.");
            }
        }
    }
    
}

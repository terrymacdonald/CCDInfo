using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Desharp;
using System.Collections.Generic;
using DisplayMagicianShared.Windows;
using NLog.Config;
using DisplayMagicianShared;

namespace CCDInfo
{    

    class Program
    {  

        static WINDOWS_DISPLAY_CONFIG myDisplayConfig = new WINDOWS_DISPLAY_CONFIG();

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
                WINDOWS_DISPLAY_CONFIG currentDisplayConfig = WinLibrary.GetActiveConfig();
                WinLibrary.PrintConfig(currentDisplayConfig);
            }
            Environment.Exit(0);
        }

        
        static void saveToFile(string filename)
        {
            Console.WriteLine($"ProfileRepository/SaveProfiles: Attempting to save the profiles repository to the {filename}.");

            myDisplayConfig = WinLibrary.GetActiveConfig();

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

                // Get the current windows display configs to compare to the one we loaded
                WINDOWS_DISPLAY_CONFIG currentWindowsDisplayConfig = getWindowsDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS);
                
                // Check whether the display config is in use now
                Console.WriteLine($"ProfileRepository/LoadProfiles: Checking whether the display configuration is already being used.");
                if (myDisplayConfig.Equals(currentWindowsDisplayConfig))
                {
                    Console.WriteLine($"We have already applied this display config! No need to implement it again. Exiting.");
                    Environment.Exit(0);
                }

                Console.WriteLine($"ProfileRepository/LoadProfiles: The requested display configuration is different to the one in use at present. We need to change to the new one.");

                // Get the all possible windows display configs
                WINDOWS_DISPLAY_CONFIG allWindowsDisplayConfig = getWindowsDisplayConfig(QDC.QDC_ALL_PATHS);


                // Dumping the things
                Desharp.Debug.Configure(new Desharp.DebugConfig
                {
                    Enabled = true,
                    //SourceLocation = true,
                    Directory = "~/Logs",
                    //LogWriteMilisecond = 10000,
                    Depth = 30,
                    // `EnvType.Web` or `EnvType.Windows`, used very rarely:
                    EnvType = EnvType.Windows,
                    // `Desharp.LogFormat.Html` or `Desharp.LogFormat.Text`:
                    LogFormat = Desharp.LogFormat.Text,
                    // for web apps only:
                    //ErrorPage = "~/custom-error-page.html",
                    //Panels = new[] { typeof(Desharp.Panels.SystemInfo), typeof(Desharp.Panels.Session) }
                });

                Console.WriteLine("Dumping the loaded config as loaded");
                /*var dumper = JsonConvert.SerializeObject(myDisplayConfig,  Formatting.Indented, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Include,
                    TypeNameHandling = TypeNameHandling.All,
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                });*/
                Debug.Dump(myDisplayConfig);

                Console.WriteLine("Dumping the current config");
                /*dumper = JsonConvert.SerializeObject(allWindowsDisplayConfig, typeof(WINDOWS_DISPLAY_CONFIG),  Formatting.Indented, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Include,
                    TypeNameHandling = TypeNameHandling.All,
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                });*/
                Debug.Dump(allWindowsDisplayConfig);
                
                // Now we go through the Paths to update the LUIDs as per Soroush's suggestion
                updateAdapterIDs(ref myDisplayConfig, allWindowsDisplayConfig.displayAdapters);

                Console.WriteLine("Dumping the loaded config after modification");
                /*var dumper = JsonConvert.SerializeObject(myDisplayConfig,  Formatting.Indented, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Include,
                    TypeNameHandling = TypeNameHandling.All,
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                });*/
                Debug.Dump(myDisplayConfig);

                Console.WriteLine($"ProfileRepository/LoadProfiles: Testing whether the display configuration is valid (allowing tweaks).");
                // Test whether a specified display configuration is supported on the computer                    
                uint myPathsCount = (uint)myDisplayConfig.displayConfigPaths.Length;
                uint myModesCount = (uint)myDisplayConfig.displayConfigModes.Length;
                //WIN32STATUS err = CCDImport.SetDisplayConfig(myPathsCount, myDisplayConfig.displayConfigPaths, myModesCount, myDisplayConfig.displayConfigModes, SDC.TEST_IF_VALID_DISPLAYCONFIG);
                WIN32STATUS err = CCDImport.SetDisplayConfig(myPathsCount, myDisplayConfig.displayConfigPaths, myModesCount, myDisplayConfig.displayConfigModes, SDC.DISPLAYMAGICIAN_VALIDATE);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - SetDisplayConfig returned WIN32STATUS {err} while testing that the Display Configuration is valid");
                    throw new Win32Exception((int)err);
                }

                Console.WriteLine($"ProfileRepository/LoadProfiles: Yay! The display configuration is valid! Attempting to set the Display Config (with Tweaks)");
                // Now set the specified display configuration for this computer                    
                err = CCDImport.SetDisplayConfig(myPathsCount, myDisplayConfig.displayConfigPaths, myModesCount, myDisplayConfig.displayConfigModes, SDC.DISPLAYMAGICIAN_SET);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - SetDisplayConfig returned WIN32STATUS {err} while trying to set the display configuration");
                    throw new Win32Exception((int)err);
                }

                Console.WriteLine($"ProfileRepository/LoadProfiles: The display configuration has been successfully applied");

                /*// Get the Active Paths and Modes in use now
                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
                err = CCDImport.QueryDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                    throw new Win32Exception((int)err);

                // Now cycle through the paths and grab the HDR state information
                var hdrInfos = new ADVANCED_HDR_INFO_PER_PATH[pathCount];
                int hdrInfoCount = 0;
                foreach (var path in paths)
                {
                    // Get advanced HDR info
                    var colorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                    colorInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
                    colorInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
                    colorInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                    colorInfo.Header.Id = path.TargetInfo.Id;
                    err = CCDImport.DisplayConfigGetDeviceInfo(ref colorInfo);
                    if (err != WIN32STATUS.ERROR_SUCCESS)
                        throw new Win32Exception((int)err);

                    // get SDR white levels
                    var whiteLevelInfo = new DISPLAYCONFIG_SDR_WHITE_LEVEL();
                    whiteLevelInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL;
                    whiteLevelInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_SDR_WHITE_LEVEL>();
                    whiteLevelInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                    whiteLevelInfo.Header.Id = path.TargetInfo.Id;
                    err = CCDImport.DisplayConfigGetDeviceInfo(ref whiteLevelInfo);
                    if (err != WIN32STATUS.ERROR_SUCCESS)
                        throw new Win32Exception((int)err);


                    hdrInfos[hdrInfoCount] = new ADVANCED_HDR_INFO_PER_PATH();
                    hdrInfos[hdrInfoCount].AdapterId = path.TargetInfo.AdapterId;
                    hdrInfos[hdrInfoCount].Id = path.TargetInfo.Id;
                    hdrInfos[hdrInfoCount].AdvancedColorInfo = colorInfo;
                    hdrInfos[hdrInfoCount].SDRWhiteLevel = whiteLevelInfo;
                    hdrInfoCount++;
                }*/


                foreach (ADVANCED_HDR_INFO_PER_PATH myHDRstate in myDisplayConfig.displayHDRStates)
                {
                    Console.WriteLine($"ProfileRepository/LoadProfiles: Trying to get information whether HDR color is in use now on Display {myHDRstate.Id}.");
                    // Get advanced HDR info
                    var colorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                    colorInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
                    colorInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
                    colorInfo.Header.AdapterId = myHDRstate.AdapterId;
                    colorInfo.Header.Id = myHDRstate.Id;
                    err = CCDImport.DisplayConfigGetDeviceInfo(ref colorInfo);
                    if (err != WIN32STATUS.ERROR_SUCCESS)
                    {
                        Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the advanced color info for display #{myHDRstate.Id}");
                        throw new Win32Exception((int)err);
                    }

                    if (myHDRstate.AdvancedColorInfo.AdvancedColorSupported && colorInfo.AdvancedColorEnabled != myHDRstate.AdvancedColorInfo.AdvancedColorEnabled)
                    {
                        Console.WriteLine($"ProfileRepository/LoadProfiles: HDR is available for use on Display {myHDRstate.Id}, and we want it set to {myHDRstate.AdvancedColorInfo.AdvancedColorEnabled} but is currently {colorInfo.AdvancedColorEnabled}.");

                        var setColorState = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE();
                        setColorState.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE;
                        setColorState.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>();
                        setColorState.Header.AdapterId = myHDRstate.AdapterId;
                        setColorState.Header.Id = myHDRstate.Id;
                        setColorState.EnableAdvancedColor = myHDRstate.AdvancedColorInfo.AdvancedColorEnabled;

                        err = CCDImport.DisplayConfigSetDeviceInfo(ref setColorState);
                        if (err != WIN32STATUS.ERROR_SUCCESS)
                        {
                            Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the SDR white level for display #{myHDRstate.Id}");
                            throw new Win32Exception((int)err);
                        }

                        Console.WriteLine($"ProfileRepository/LoadProfiles: HDR successfully set to {myHDRstate.AdvancedColorInfo.AdvancedColorEnabled} on Display {myHDRstate.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"ProfileRepository/LoadProfiles: Skipping setting HDR on Display {myHDRstate.Id} as it does not support HDR");
                    }

                }
                

            }
            else
            {
                Console.WriteLine($"ProfileRepository/LoadProfiles: The {filename} profile JSON file exists but is empty! So we're going to treat it as if it didn't exist.");
            }
        }
    }
    
}

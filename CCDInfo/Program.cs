using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CCDInfo
{
    class Program
    {

        public struct ADVANCED_HDR_INFO_PER_PATH : IEquatable<ADVANCED_HDR_INFO_PER_PATH>
        {
            public LUID AdapterId;
            public uint Id;
            public DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO AdvancedColorInfo;
            public DISPLAYCONFIG_SDR_WHITE_LEVEL SDRWhiteLevel;

            public bool Equals(ADVANCED_HDR_INFO_PER_PATH other)
            => // AdapterId.Equals(other.AdapterId) && // Removed the AdapterId from the Equals, as it changes after reboot.
                Id == other.Id &&
               AdvancedColorInfo.Equals(other.AdvancedColorInfo) &&
               SDRWhiteLevel.Equals(other.SDRWhiteLevel);
            public override int GetHashCode()
            {
                return (AdapterId, Id, AdvancedColorInfo, SDRWhiteLevel).GetHashCode();
            }
        }

        public struct WINDOWS_DISPLAY_CONFIG : IEquatable<WINDOWS_DISPLAY_CONFIG>
        {
            public DISPLAYCONFIG_PATH_INFO[] displayConfigPaths;
            public DISPLAYCONFIG_MODE_INFO[] displayConfigModes;
            public ADVANCED_HDR_INFO_PER_PATH[] displayHDRStates;

            public bool Equals(WINDOWS_DISPLAY_CONFIG other)
            => displayConfigPaths.SequenceEqual(other.displayConfigPaths) &&
               displayConfigModes.SequenceEqual(other.displayConfigModes) &&
               displayHDRStates.SequenceEqual(other.displayHDRStates);

            public override int GetHashCode()
            {
                return (displayConfigPaths, displayConfigModes, displayHDRStates).GetHashCode();
            }
        }

        static WINDOWS_DISPLAY_CONFIG myDisplayConfig = new WINDOWS_DISPLAY_CONFIG();

        static void Main(string[] args)
        {
            Console.WriteLine($"CCDInfo v1.0.4");
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
                    Console.WriteLine($"CCDInfo is a little program to help test setting display layout and HDR settings in Windows 10 64-bit.\n");
                    Console.WriteLine($"You can run it without any command line parameters, and it will print all the information it can find from the \nWindows Display CCD interface.\n");
                    Console.WriteLine($"You can also run it with 'CCDInfo save myfilename.cfg' and it will save the current display configuration into\nthe myfilename.cfg file.\n");
                    Console.WriteLine($"This is most useful when you subsequently use the 'CCDInfo load myfilename.cfg' command, as it will load the\ndisplay configuration from the myfilename.cfg file and make it live.");
                    Console.WriteLine($"In this way, you can make yourself a library of different cfg files with different display layouts, then use\nthe CCDInfo load command to swap between them.");
                    Environment.Exit(1);
                }
            }            

            WIN32STATUS err = CCDImport.GetDisplayConfigBufferSizes(QDC.QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount);
            if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - GetDisplayConfigBufferSizes returned WIN32STATUS {err} when trying to get the maximum path and mode sizes");
                Environment.Exit(1);
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            err = CCDImport.QueryDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - QueryDisplayConfig returned WIN32STATUS {err} when trying to query all avilable displays");
                Environment.Exit(1);
            }

            foreach (var path in paths)
            {
                Console.WriteLine($"----++++==== Path ====++++----");

                // get display source name
                var sourceInfo = new DISPLAYCONFIG_GET_SOURCE_NAME();
                sourceInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                sourceInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_GET_SOURCE_NAME>();
                sourceInfo.Header.AdapterId = path.SourceInfo.AdapterId;
                sourceInfo.Header.Id = path.SourceInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref sourceInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the source info for source adapter #{path.SourceInfo.AdapterId}");
                    Environment.Exit(1);
                }

                Console.WriteLine($"****** Investigating Display Source {sourceInfo.ViewGdiDeviceName} *******");
                Console.WriteLine();

                // get display target name
                var targetInfo = new DISPLAYCONFIG_GET_TARGET_NAME();
                targetInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                targetInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_GET_TARGET_NAME>();
                targetInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                targetInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref targetInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the target info for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);
                }

                Console.WriteLine($"****** Investigating Display Target {targetInfo.MonitorFriendlyDeviceName} *******");
                Console.WriteLine(" Connector Instance: " + targetInfo.ConnectorInstance);
                Console.WriteLine(" EDID Manufacturer ID: " + targetInfo.EdidManufactureId);
                Console.WriteLine(" EDID Product Code ID: " + targetInfo.EdidProductCodeId);
                Console.WriteLine(" Flags Friendly Name from EDID: " + targetInfo.Flags.FriendlyNameFromEdid);
                Console.WriteLine(" Flags Friendly Name Forced: " + targetInfo.Flags.FriendlyNameForced);
                Console.WriteLine(" Flags EDID ID is Valid: " + targetInfo.Flags.EdidIdsValid);
                Console.WriteLine(" Monitor Device Path: " + targetInfo.MonitorDevicePath);
                Console.WriteLine(" Monitor Friendly Device Name: " + targetInfo.MonitorFriendlyDeviceName);
                Console.WriteLine(" Output Technology: " + targetInfo.OutputTechnology);
                Console.WriteLine();

                // get display adapter name
                var adapterInfo = new DISPLAYCONFIG_GET_ADAPTER_NAME();
                adapterInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME;
                adapterInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_GET_ADAPTER_NAME>();
                adapterInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                adapterInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref adapterInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the adapter name for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);
                }

                Console.WriteLine($"****** Investigating Adapter {adapterInfo.AdapterDevicePath} *******");
                Console.WriteLine();


                // get display target preferred mode
                var targetPreferredInfo = new DISPLAYCONFIG_GET_TARGET_PREFERRED_NAME();
                targetPreferredInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_PREFERRED_MODE;
                targetPreferredInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_GET_TARGET_PREFERRED_NAME>();
                targetPreferredInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                targetPreferredInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref targetPreferredInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the preferred target name for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);
                }

                Console.WriteLine($"****** Investigating Display Target Preferred Mode  *******");
                Console.WriteLine(" Width: " + targetPreferredInfo.Width);
                Console.WriteLine(" Height: " + targetPreferredInfo.Height);
                Console.WriteLine($" Target Video Signal Info Active Size: ({targetPreferredInfo.TargetMode.TargetVideoSignalInfo.ActiveSize.Cx}x{targetPreferredInfo.TargetMode.TargetVideoSignalInfo.ActiveSize.Cy})");
                Console.WriteLine($" Target Video Signal Info Total Size: ({targetPreferredInfo.TargetMode.TargetVideoSignalInfo.TotalSize.Cx}x{targetPreferredInfo.TargetMode.TargetVideoSignalInfo.TotalSize.Cy})");
                Console.WriteLine(" Target Video Signal Info HSync Frequency: " + targetPreferredInfo.TargetMode.TargetVideoSignalInfo.HSyncFreq);
                Console.WriteLine(" Target Video Signal Info VSync Frequency: " + targetPreferredInfo.TargetMode.TargetVideoSignalInfo.VSyncFreq);
                Console.WriteLine(" Target Video Signal Info Pixel Rate: " + targetPreferredInfo.TargetMode.TargetVideoSignalInfo.PixelRate);
                Console.WriteLine(" Target Video Signal Info Scan Line Ordering: " + targetPreferredInfo.TargetMode.TargetVideoSignalInfo.ScanLineOrdering);
                Console.WriteLine(" Target Video Signal Info Video Standard: " + targetPreferredInfo.TargetMode.TargetVideoSignalInfo.VideoStandard);
                Console.WriteLine();

                // get display target base type
                var targetBaseTypeInfo = new DISPLAYCONFIG_GET_TARGET_BASE_TYPE();
                targetBaseTypeInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_BASE_TYPE;
                targetBaseTypeInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_GET_TARGET_BASE_TYPE>();
                targetBaseTypeInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                targetBaseTypeInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref targetBaseTypeInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the target base type for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);
                }

                Console.WriteLine($"****** Investigating Target Base Type *******");
                Console.WriteLine(" Base Output Technology: " + targetBaseTypeInfo.BaseOutputTechnology);
                Console.WriteLine();

                // get display support virtual resolution
                var supportVirtResInfo = new DISPLAYCONFIG_SUPPORT_VIRTUAL_RESOLUTION();
                supportVirtResInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SUPPORT_VIRTUAL_RESOLUTION;
                supportVirtResInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_SUPPORT_VIRTUAL_RESOLUTION>();
                supportVirtResInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                supportVirtResInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref supportVirtResInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to find out the virtual resolution support for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);
                }

                Console.WriteLine($"****** Investigating Target Supporting virtual resolution *******");
                Console.WriteLine(" Virtual Resolution is Disabled: " + supportVirtResInfo.IsMonitorVirtualResolutionDisabled);
                Console.WriteLine();


                //get advanced color info
                var colorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                colorInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
                colorInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
                colorInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                colorInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref colorInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the advanced color info for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);
                }

                Console.WriteLine($"****** Investigating Advanced Color Info *******");
                Console.WriteLine(" Advanced Color Supported: " + colorInfo.AdvancedColorSupported);
                Console.WriteLine(" Advanced Color Enabled  : " + colorInfo.AdvancedColorEnabled);
                Console.WriteLine(" Advanced Color Force Disabled: " + colorInfo.AdvancedColorForceDisabled);
                Console.WriteLine(" Bits per Color Channel: " + colorInfo.BitsPerColorChannel);
                Console.WriteLine(" Color Encoding: " + colorInfo.ColorEncoding);
                Console.WriteLine(" Wide Color Enforced: " + colorInfo.WideColorEnforced);
                Console.WriteLine();

                // get SDR white levels
                var whiteLevelInfo = new DISPLAYCONFIG_SDR_WHITE_LEVEL();
                whiteLevelInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL;
                whiteLevelInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_SDR_WHITE_LEVEL>();
                whiteLevelInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                whiteLevelInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref whiteLevelInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the SDR white level for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);
                }

                Console.WriteLine($"****** Investigating SDR White Level  *******");
                Console.WriteLine(" SDR White Level: " + whiteLevelInfo.SDRWhiteLevel);
                Console.WriteLine();
            }

        }

        static void updateAdapterIDs(ref WINDOWS_DISPLAY_CONFIG savedDisplayConfig, ref WINDOWS_DISPLAY_CONFIG currentDisplayConfig)
        {
            // Update the paths with the current adapter id
            for (int i = 0; i < savedDisplayConfig.displayConfigPaths.Length; i++)
            {
                DISPLAYCONFIG_PATH_INFO savedPath = savedDisplayConfig.displayConfigPaths[i];
                var matchingCurrentPaths = currentDisplayConfig.displayConfigPaths.Where(item => item.Equals(savedPath));
                if (matchingCurrentPaths.Count() > 0)
                {
                    savedPath.SourceInfo.AdapterId = matchingCurrentPaths.First().SourceInfo.AdapterId;
                    savedPath.TargetInfo.AdapterId = matchingCurrentPaths.First().TargetInfo.AdapterId;
                    savedDisplayConfig.displayConfigPaths[i] = savedPath;
                }
            }
            // Update the modes with the current adapter id
            for (int i = 0; i < savedDisplayConfig.displayConfigModes.Length; i++)
            {
                DISPLAYCONFIG_MODE_INFO savedMode = savedDisplayConfig.displayConfigModes[i];
                var matchingCurrentModes = currentDisplayConfig.displayConfigModes.Where(item => item.Equals(savedMode));
                if (matchingCurrentModes.Count() > 0)
                {
                    savedMode.AdapterId = matchingCurrentModes.First().AdapterId;
                    savedDisplayConfig.displayConfigModes[i] = savedMode;
                }
            }
            // Update the HDR info with the current adapter id
            for (int i = 0; i < savedDisplayConfig.displayHDRStates.Length; i++)
            //foreach (ADVANCED_HDR_INFO_PER_PATH savedHDRInfo in savedDisplayConfig.displayHDRStates)
            {
                ADVANCED_HDR_INFO_PER_PATH savedHDRInfo = savedDisplayConfig.displayHDRStates[i];
                var matchingCurrentHDRInfos = currentDisplayConfig.displayHDRStates.Where(item => item.Equals(savedHDRInfo));
                if (matchingCurrentHDRInfos.Count() > 0)
                {

                    savedHDRInfo.AdapterId = matchingCurrentHDRInfos.First().AdapterId;
                    savedHDRInfo.AdvancedColorInfo.Header.AdapterId = matchingCurrentHDRInfos.First().AdvancedColorInfo.Header.AdapterId;
                    savedHDRInfo.SDRWhiteLevel.Header.AdapterId = matchingCurrentHDRInfos.First().SDRWhiteLevel.Header.AdapterId;
                    savedDisplayConfig.displayHDRStates[i] = savedHDRInfo;
                }
            }
            //return savedDisplayConfig;
        }

        static WINDOWS_DISPLAY_CONFIG getCurrentWindowsDisplayConfig()
        {
            WINDOWS_DISPLAY_CONFIG windowsDisplayConfig = new WINDOWS_DISPLAY_CONFIG();

            // Get the size of the largest Active Paths and Modes arrays
            WIN32STATUS err = CCDImport.GetDisplayConfigBufferSizes(QDC.QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount);
            if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - GetDisplayConfigBufferSizes returned WIN32STATUS {err} when trying to get the maximum path and mode sizes");
                Environment.Exit(1);
            }

            // Get the Active Paths and Modes in use now
            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            err = CCDImport.QueryDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - QueryDisplayConfig returned WIN32STATUS {err} when trying to get the Display Configuration to save later");
                Environment.Exit(1);
            }

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
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the advanced color info for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);
                }

                // get SDR white levels
                var whiteLevelInfo = new DISPLAYCONFIG_SDR_WHITE_LEVEL();
                whiteLevelInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL;
                whiteLevelInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_SDR_WHITE_LEVEL>();
                whiteLevelInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                whiteLevelInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref whiteLevelInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the SDR white level for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);
                }


                hdrInfos[hdrInfoCount] = new ADVANCED_HDR_INFO_PER_PATH();
                hdrInfos[hdrInfoCount].AdapterId = path.TargetInfo.AdapterId;
                hdrInfos[hdrInfoCount].Id = path.TargetInfo.Id;
                hdrInfos[hdrInfoCount].AdvancedColorInfo = colorInfo;
                hdrInfos[hdrInfoCount].SDRWhiteLevel = whiteLevelInfo;
                hdrInfoCount++;
            }


            // Store the active paths and modes in our display config object
            windowsDisplayConfig.displayConfigPaths = paths;
            windowsDisplayConfig.displayConfigModes = modes;
            windowsDisplayConfig.displayHDRStates = hdrInfos;

            return windowsDisplayConfig;
        }


        static void saveToFile(string filename)
        {
            Console.WriteLine($"ProfileRepository/SaveProfiles: Attempting to save the profiles repository to the {filename}.");

            myDisplayConfig = getCurrentWindowsDisplayConfig();

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

                // Get the current windows display config
                WINDOWS_DISPLAY_CONFIG currentWindowsDisplayConfig = getCurrentWindowsDisplayConfig();
                
                // Check whether the display config is in use now
                Console.WriteLine($"ProfileRepository/LoadProfiles: Checking whether the display configuration is already being used.");
                if (myDisplayConfig.Equals(currentWindowsDisplayConfig))
                {
                    Console.WriteLine($"We have already applied this display config! No need to implement it again. Exiting.");
                    Environment.Exit(0);
                }

                Console.WriteLine($"ProfileRepository/LoadProfiles: The requested display configuration is different to the one in use at present. We need to change to the new one.");

                // Now we go through the Paths to update the LUIDs as per Soroush's suggestion
                updateAdapterIDs(ref myDisplayConfig, ref currentWindowsDisplayConfig);

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
                    colorInfo.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
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
                        setColorState.Header.Size = Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>();
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

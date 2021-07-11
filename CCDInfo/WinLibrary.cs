using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using DisplayMagicianShared;
using System.ComponentModel;

namespace DisplayMagicianShared.Windows
{
    [StructLayout(LayoutKind.Sequential)]
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
            return (Id, AdvancedColorInfo, SDRWhiteLevel).GetHashCode();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWS_DISPLAY_CONFIG : IEquatable<WINDOWS_DISPLAY_CONFIG>
    {
        public Dictionary<ulong, string> displayAdapters;
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

    class WinLibrary : IDisposable
    {
        
        // Static members are 'eagerly initialized', that is, 
        // immediately when class is loaded for the first time.
        // .NET guarantees thread safety for static initialization
        private static WinLibrary _instance = new WinLibrary();

        private bool _initialised = false;

        // To detect redundant calls
        private bool _disposed = false;

        // Instantiate a SafeHandle instance.
        private SafeHandle _safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        private IntPtr _adlContextHandle = IntPtr.Zero;

        static WinLibrary() { }
        public WinLibrary()
        {
            SharedLogger.logger.Trace("WinLibrary/WinLibrary: Intialising Windows CCD library interface");
            _initialised = true;
            SharedLogger.logger.Trace("WinLibrary/WinLibrary: ADL2 library was initialised successfully");

        }

        ~WinLibrary()
        {
            // The WinLibrary was initialised, but doesn't need to be freed.
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose() => Dispose(true);

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {                
                // Dispose managed state (managed objects).
                _safeHandle?.Dispose();
            }

            _disposed = true;
        }


        public bool IsInstalled
        {
            get { return _initialised; }
        }

        public static WinLibrary GetLibrary()
        {
            return _instance;
        }

        private void PatchAdapterIDs(ref WINDOWS_DISPLAY_CONFIG savedDisplayConfig, Dictionary<ulong, string> currentAdapterMap)
        {

            Dictionary<ulong, ulong> adapterOldToNewMap = new Dictionary<ulong, ulong>();

            foreach (KeyValuePair<ulong, string> savedAdapter in savedDisplayConfig.displayAdapters)
            {
                foreach (KeyValuePair<ulong, string> currentAdapter in currentAdapterMap)
                {
                    if (currentAdapter.Value.Equals(savedAdapter.Value))
                    {
                        // we have found the new LUID Value for the same adapter
                        // So we want to store it
                        adapterOldToNewMap.Add(savedAdapter.Key, currentAdapter.Key);
                    }
                }
            }

            ulong newAdapterValue = 0;
            // Update the paths with the current adapter id
            for (int i = 0; i < savedDisplayConfig.displayConfigPaths.Length; i++)
            {
                // Change the Path SourceInfo and TargetInfo AdapterIDs
                if (adapterOldToNewMap.ContainsKey(savedDisplayConfig.displayConfigPaths[i].SourceInfo.AdapterId.Value))
                {
                    // We get here if there is a matching adapter
                    newAdapterValue = adapterOldToNewMap[savedDisplayConfig.displayConfigPaths[i].SourceInfo.AdapterId.Value];
                    savedDisplayConfig.displayConfigPaths[i].SourceInfo.AdapterId = AdapterValueToLUID(newAdapterValue);
                    newAdapterValue = adapterOldToNewMap[savedDisplayConfig.displayConfigPaths[i].TargetInfo.AdapterId.Value];
                    savedDisplayConfig.displayConfigPaths[i].TargetInfo.AdapterId = AdapterValueToLUID(newAdapterValue);
                }
                else
                {
                    // if there isn't a matching adapter, then we just pick the first current one and hope that works!
                    // (it is highly likely to... its only if the user has multiple graphics cards with some weird config it may break)
                    newAdapterValue = currentAdapterMap.First().Key;
                    savedDisplayConfig.displayConfigPaths[i].SourceInfo.AdapterId = AdapterValueToLUID(newAdapterValue);
                    savedDisplayConfig.displayConfigPaths[i].TargetInfo.AdapterId = AdapterValueToLUID(newAdapterValue);
                }
            }

            // Update the modes with the current adapter id
            for (int i = 0; i < savedDisplayConfig.displayConfigModes.Length; i++)
            {
                // Change the Mode AdapterID
                if (adapterOldToNewMap.ContainsKey(savedDisplayConfig.displayConfigModes[i].AdapterId.Value))
                {
                    // We get here if there is a matching adapter
                    newAdapterValue = adapterOldToNewMap[savedDisplayConfig.displayConfigModes[i].AdapterId.Value];
                    savedDisplayConfig.displayConfigModes[i].AdapterId = AdapterValueToLUID(newAdapterValue);
                }
                else
                {
                    // if there isn't a matching adapter, then we just pick the first current one and hope that works!
                    // (it is highly likely to... its only if the user has multiple graphics cards with some weird config it may break)
                    newAdapterValue = currentAdapterMap.First().Key;
                    savedDisplayConfig.displayConfigModes[i].AdapterId = AdapterValueToLUID(newAdapterValue);
                }
            }

            // Update the HDRInfo with the current adapter id
            for (int i = 0; i < savedDisplayConfig.displayHDRStates.Length; i++)
            {
                // Change the Mode AdapterID
                if (adapterOldToNewMap.ContainsKey(savedDisplayConfig.displayHDRStates[i].AdapterId.Value))
                {
                    // We get here if there is a matching adapter
                    newAdapterValue = adapterOldToNewMap[savedDisplayConfig.displayHDRStates[i].AdapterId.Value];
                    savedDisplayConfig.displayHDRStates[i].AdapterId = AdapterValueToLUID(newAdapterValue);
                    newAdapterValue = adapterOldToNewMap[savedDisplayConfig.displayHDRStates[i].AdvancedColorInfo.Header.AdapterId.Value];
                    savedDisplayConfig.displayHDRStates[i].AdvancedColorInfo.Header.AdapterId = AdapterValueToLUID(newAdapterValue);
                    newAdapterValue = adapterOldToNewMap[savedDisplayConfig.displayHDRStates[i].SDRWhiteLevel.Header.AdapterId.Value];
                    savedDisplayConfig.displayHDRStates[i].SDRWhiteLevel.Header.AdapterId = AdapterValueToLUID(newAdapterValue);
                }
                else
                {
                    // if there isn't a matching adapter, then we just pick the first current one and hope that works!
                    // (it is highly likely to... its only if the user has multiple graphics cards with some weird config it may break)
                    newAdapterValue = currentAdapterMap.First().Key;
                    savedDisplayConfig.displayHDRStates[i].AdapterId = AdapterValueToLUID(newAdapterValue);
                    savedDisplayConfig.displayHDRStates[i].AdvancedColorInfo.Header.AdapterId = AdapterValueToLUID(newAdapterValue);
                    savedDisplayConfig.displayHDRStates[i].SDRWhiteLevel.Header.AdapterId = AdapterValueToLUID(newAdapterValue);
                }
            }

        }

        public WINDOWS_DISPLAY_CONFIG GetActiveConfig()
        {
            return GetWindowsDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS);
        }

        private WINDOWS_DISPLAY_CONFIG GetWindowsDisplayConfig(QDC selector = QDC.QDC_ONLY_ACTIVE_PATHS)
        {

            // Get the size of the largest Active Paths and Modes arrays
            int pathCount = 0;
            int modeCount = 0;
            WIN32STATUS err = CCDImport.GetDisplayConfigBufferSizes(QDC.QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount);
            if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - GetDisplayConfigBufferSizes returned WIN32STATUS {err} when trying to get the maximum path and mode sizes");
                Environment.Exit(1);
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            err = CCDImport.QueryDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (err == WIN32STATUS.ERROR_INSUFFICIENT_BUFFER)
            {
                // Screen changed in between GetDisplayConfigBufferSizes and QueryDisplayConfig, so we need to get buffer sizes again
                // as per https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig 
                err = CCDImport.GetDisplayConfigBufferSizes(QDC.QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - GetDisplayConfigBufferSizes returned WIN32STATUS {err} when trying to get the maximum path and mode sizes");
                    Environment.Exit(1);
                }
                paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
                err = CCDImport.QueryDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (err == WIN32STATUS.ERROR_INSUFFICIENT_BUFFER)
                {
                    Console.WriteLine($"ERROR - QueryDisplayConfig returned WIN32STATUS {err} ERROR_INSUFFICIENT_BUFFER twice when trying to query all available displays");
                }
                else if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - QueryDisplayConfig returned WIN32STATUS {err} when trying to query all available displays");
                    Environment.Exit(1);
                }
            }
            else if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - QueryDisplayConfig returned WIN32STATUS {err} when trying to query all available displays");
                Environment.Exit(1);
            }

            // Prepare the empty windows display config
            WINDOWS_DISPLAY_CONFIG windowsDisplayConfig = new WINDOWS_DISPLAY_CONFIG();
            windowsDisplayConfig.displayAdapters = new Dictionary<ulong, string>();
            windowsDisplayConfig.displayHDRStates = new ADVANCED_HDR_INFO_PER_PATH[pathCount];

            // Now cycle through the paths and grab the HDR state information
            // and map the adapter name to adapter id
            var hdrInfos = new ADVANCED_HDR_INFO_PER_PATH[pathCount];
            int hdrInfoCount = 0;
            foreach (var path in paths)
            {
                // Get adapter ID for later
                if (!windowsDisplayConfig.displayAdapters.ContainsKey(path.TargetInfo.AdapterId.Value))
                {
                    var adapterInfo = new DISPLAYCONFIG_ADAPTER_NAME();
                    adapterInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME;
                    adapterInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_ADAPTER_NAME>();
                    adapterInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                    adapterInfo.Header.Id = path.TargetInfo.Id;
                    err = CCDImport.DisplayConfigGetDeviceInfo(ref adapterInfo);
                    if (err == WIN32STATUS.ERROR_SUCCESS)
                    {
                        // Store it for later
                        windowsDisplayConfig.displayAdapters.Add(path.TargetInfo.AdapterId.Value, adapterInfo.AdapterDevicePath);
                    }
                }

                // Get advanced HDR info
                var colorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
                colorInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
                colorInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
                colorInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                colorInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref colorInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    //Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the advanced color info for display #{path.TargetInfo.Id}");
                    //Environment.Exit(1);
                }

                // get SDR white levels
                var whiteLevelInfo = new DISPLAYCONFIG_SDR_WHITE_LEVEL();
                whiteLevelInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL;
                whiteLevelInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SDR_WHITE_LEVEL>();
                whiteLevelInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                whiteLevelInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref whiteLevelInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    //Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the SDR white level for display #{path.TargetInfo.Id}");
                    //Environment.Exit(1);
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


        private LUID AdapterValueToLUID(ulong adapterValue)
        {
            LUID luid = new LUID();
            luid.LowPart = (uint)(adapterValue & uint.MaxValue);
            luid.HighPart = (uint)(adapterValue >> 32);
            return luid;
        }

        public void PrintActiveConfig()
        {

            // Get the size of the largest Active Paths and Modes arrays
            int pathCount = 0;
            int modeCount = 0;
            WIN32STATUS err = CCDImport.GetDisplayConfigBufferSizes(QDC.QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount);
            if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - GetDisplayConfigBufferSizes returned WIN32STATUS {err} when trying to get the maximum path and mode sizes");
                Environment.Exit(1);
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            err = CCDImport.QueryDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (err == WIN32STATUS.ERROR_INSUFFICIENT_BUFFER)
            {
                // Screen changed in between GetDisplayConfigBufferSizes and QueryDisplayConfig, so we need to get buffer sizes again
                // as per https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig 
                err = CCDImport.GetDisplayConfigBufferSizes(QDC.QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - GetDisplayConfigBufferSizes returned WIN32STATUS {err} when trying to get the maximum path and mode sizes");
                    Environment.Exit(1);
                }
                paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
                err = CCDImport.QueryDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (err == WIN32STATUS.ERROR_INSUFFICIENT_BUFFER)
                {
                    Console.WriteLine($"ERROR - QueryDisplayConfig returned WIN32STATUS {err} ERROR_INSUFFICIENT_BUFFER twice when trying to query all available displays");
                }
                else if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - QueryDisplayConfig returned WIN32STATUS {err} when trying to query all available displays");
                    Environment.Exit(1);
                }
            }
            else if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - QueryDisplayConfig returned WIN32STATUS {err} when trying to query all available displays");
                Environment.Exit(1);
            }

            foreach (var path in paths)
            {
                Console.WriteLine($"----++++==== Path ====++++----");

                // get display source name
                var sourceInfo = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                sourceInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                sourceInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
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
                var targetInfo = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                targetInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                targetInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
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
                var adapterInfo = new DISPLAYCONFIG_ADAPTER_NAME();
                adapterInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME;
                adapterInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_ADAPTER_NAME>();
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
                var targetPreferredInfo = new DISPLAYCONFIG_TARGET_PREFERRED_MODE();
                targetPreferredInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_PREFERRED_MODE;
                targetPreferredInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_PREFERRED_MODE>();
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
                var targetBaseTypeInfo = new DISPLAYCONFIG_TARGET_BASE_TYPE();
                targetBaseTypeInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_BASE_TYPE;
                targetBaseTypeInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_BASE_TYPE>();
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
                supportVirtResInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SUPPORT_VIRTUAL_RESOLUTION>();
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
                colorInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
                colorInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                colorInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref colorInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    /*Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the advanced color info for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);*/
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
                whiteLevelInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SDR_WHITE_LEVEL>();
                whiteLevelInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                whiteLevelInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref whiteLevelInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    /*Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the SDR white level for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);*/
                }

                Console.WriteLine($"****** Investigating SDR White Level  *******");
                Console.WriteLine(" SDR White Level: " + whiteLevelInfo.SDRWhiteLevel);
                Console.WriteLine();
            }
        }
       
        public bool SetActiveConfig(WINDOWS_DISPLAY_CONFIG displayConfig)
        {

            // Get the current windows display configs to compare to the one we loaded
            WINDOWS_DISPLAY_CONFIG currentWindowsDisplayConfig = GetWindowsDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS);

            // Check whether the display config is in use now
            Console.WriteLine($"ProfileRepository/LoadProfiles: Checking whether the display configuration is already being used.");
            if (displayConfig.Equals(currentWindowsDisplayConfig))
            {
                Console.WriteLine($"We have already applied this display config! No need to implement it again. Exiting.");
                Environment.Exit(0);
            }

            Console.WriteLine($"ProfileRepository/LoadProfiles: The requested display configuration is different to the one in use at present. We need to change to the new one.");

            // Get the all possible windows display configs
            WINDOWS_DISPLAY_CONFIG allWindowsDisplayConfig = GetWindowsDisplayConfig(QDC.QDC_ALL_PATHS);

            // Now we go through the Paths to update the LUIDs as per Soroush's suggestion
            PatchAdapterIDs(ref displayConfig, allWindowsDisplayConfig.displayAdapters);

            Console.WriteLine($"ProfileRepository/LoadProfiles: Testing whether the display configuration is valid (allowing tweaks).");
            // Test whether a specified display configuration is supported on the computer                    
            uint myPathsCount = (uint)displayConfig.displayConfigPaths.Length;
            uint myModesCount = (uint)displayConfig.displayConfigModes.Length;
            //WIN32STATUS err = CCDImport.SetDisplayConfig(myPathsCount, myDisplayConfig.displayConfigPaths, myModesCount, myDisplayConfig.displayConfigModes, SDC.TEST_IF_VALID_DISPLAYCONFIG);
            WIN32STATUS err = CCDImport.SetDisplayConfig(myPathsCount, displayConfig.displayConfigPaths, myModesCount, displayConfig.displayConfigModes, SDC.DISPLAYMAGICIAN_VALIDATE);
            if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - SetDisplayConfig returned WIN32STATUS {err} while testing that the Display Configuration is valid");
                throw new Win32Exception((int)err);
            }

            Console.WriteLine($"ProfileRepository/LoadProfiles: Yay! The display configuration is valid! Attempting to set the Display Config (with Tweaks)");
            // Now set the specified display configuration for this computer                    
            err = CCDImport.SetDisplayConfig(myPathsCount, displayConfig.displayConfigPaths, myModesCount, displayConfig.displayConfigModes, SDC.DISPLAYMAGICIAN_SET);
            if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - SetDisplayConfig returned WIN32STATUS {err} while trying to set the display configuration");
                throw new Win32Exception((int)err);
            }

            Console.WriteLine($"ProfileRepository/LoadProfiles: The display configuration has been successfully applied");

            foreach (ADVANCED_HDR_INFO_PER_PATH myHDRstate in displayConfig.displayHDRStates)
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
            return true;
        }

        public bool IsActiveConfig(WINDOWS_DISPLAY_CONFIG displayConfig)
        {
            // Get the current windows display configs to compare to the one we loaded
            WINDOWS_DISPLAY_CONFIG currentWindowsDisplayConfig = GetWindowsDisplayConfig(QDC.QDC_ONLY_ACTIVE_PATHS);

            // Check whether the display config is in use now
            Console.WriteLine($"ProfileRepository/LoadProfiles: Checking whether the display configuration is already being used.");
            if (displayConfig.Equals(currentWindowsDisplayConfig))
            {
                return true;
            }
            else
            {
                return false;
            }
            
        }

        public bool IsPossibleConfig(WINDOWS_DISPLAY_CONFIG displayConfig)
        {
            // Get the all possible windows display configs
            WINDOWS_DISPLAY_CONFIG allWindowsDisplayConfig = GetWindowsDisplayConfig(QDC.QDC_ALL_PATHS);

            // Firstly check that the Adapter Names are still currently available (i.e. the adapter hasn't been replaced).
            foreach (string savedAdapterName in displayConfig.displayAdapters.Values)
            {
                // If there is even one of the saved adapters that has changed, then it's no longer possible
                // to use this display config!
                if (!allWindowsDisplayConfig.displayAdapters.Values.Contains(savedAdapterName))
                {
                    return false;
                }
            }

            // Now we go through the Paths to update the LUIDs as per Soroush's suggestion
            PatchAdapterIDs(ref displayConfig, allWindowsDisplayConfig.displayAdapters);

            Console.WriteLine($"ProfileRepository/LoadProfiles: Testing whether the display configuration is valid (allowing tweaks).");
            // Test whether a specified display configuration is supported on the computer                    
            uint myPathsCount = (uint)displayConfig.displayConfigPaths.Length;
            uint myModesCount = (uint)displayConfig.displayConfigModes.Length;
            WIN32STATUS err = CCDImport.SetDisplayConfig(myPathsCount, displayConfig.displayConfigPaths, myModesCount, displayConfig.displayConfigModes, SDC.DISPLAYMAGICIAN_VALIDATE);
            if (err == WIN32STATUS.ERROR_SUCCESS)
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        public List<string> GetCurrentDisplayIdentifiers()
        {
            return GetSomeDisplayIdentifiers(QDC.QDC_ONLY_ACTIVE_PATHS);
        }

        public List<string> GetAllConnectedDisplayIdentifiers()            
        {
            return GetSomeDisplayIdentifiers(QDC.QDC_ALL_PATHS);
        }

        private List<string> GetSomeDisplayIdentifiers(QDC selector = QDC.QDC_ONLY_ACTIVE_PATHS)
        {
            SharedLogger.logger.Debug($"WinLibrary/GetCurrentDisplayIdentifiers: Generating the unique Display Identifiers for the currently active configuration");

            List<string> displayIdentifiers = new List<string>();

            SharedLogger.logger.Trace($"WinLibrary/GetCurrentDisplayIdentifiers: Testing whether the display configuration is valid (allowing tweaks).");
            // Get the size of the largest Active Paths and Modes arrays
            int pathCount = 0;
            int modeCount = 0;
            WIN32STATUS err = CCDImport.GetDisplayConfigBufferSizes(selector, out pathCount, out modeCount);
            if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - GetDisplayConfigBufferSizes returned WIN32STATUS {err} when trying to get the maximum path and mode sizes");
                Environment.Exit(1);
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            err = CCDImport.QueryDisplayConfig(selector, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (err == WIN32STATUS.ERROR_INSUFFICIENT_BUFFER)
            {
                // Screen changed in between GetDisplayConfigBufferSizes and QueryDisplayConfig, so we need to get buffer sizes again
                // as per https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig 
                err = CCDImport.GetDisplayConfigBufferSizes(selector, out pathCount, out modeCount);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - GetDisplayConfigBufferSizes returned WIN32STATUS {err} when trying to get the maximum path and mode sizes");
                    Environment.Exit(1);
                }
                paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
                err = CCDImport.QueryDisplayConfig(selector, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (err == WIN32STATUS.ERROR_INSUFFICIENT_BUFFER)
                {
                    Console.WriteLine($"ERROR - QueryDisplayConfig returned WIN32STATUS {err} ERROR_INSUFFICIENT_BUFFER twice when trying to query all available displays");
                }
                else if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - QueryDisplayConfig returned WIN32STATUS {err} when trying to query all available displays");
                    Environment.Exit(1);
                }
            }
            else if (err != WIN32STATUS.ERROR_SUCCESS)
            {
                Console.WriteLine($"ERROR - QueryDisplayConfig returned WIN32STATUS {err} when trying to query all available displays");
                Environment.Exit(1);
            }

            foreach (var path in paths)
            {
                if (path.TargetInfo.TargetAvailable == false)
                {
                    // We want to skip this one cause it's not valid
                    continue;
                }

                /*if (path.TargetInfo.TargetInUse == false)
                {
                    // We want to skip this one cause it's not valid
                    continue;
                }*/

                // get display source name
                var sourceInfo = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                sourceInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                sourceInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                sourceInfo.Header.AdapterId = path.SourceInfo.AdapterId;
                sourceInfo.Header.Id = path.SourceInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref sourceInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the source info for source adapter #{path.SourceInfo.AdapterId}");
                    Environment.Exit(1);
                }

                // get display target name
                var targetInfo = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                targetInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                targetInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                targetInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                targetInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref targetInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the target info for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);
                }

                // get display adapter name
                var adapterInfo = new DISPLAYCONFIG_ADAPTER_NAME();
                adapterInfo.Header.Type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME;
                adapterInfo.Header.Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_ADAPTER_NAME>();
                adapterInfo.Header.AdapterId = path.TargetInfo.AdapterId;
                adapterInfo.Header.Id = path.TargetInfo.Id;
                err = CCDImport.DisplayConfigGetDeviceInfo(ref adapterInfo);
                if (err != WIN32STATUS.ERROR_SUCCESS)
                {
                    Console.WriteLine($"ERROR - DisplayConfigGetDeviceInfo returned WIN32STATUS {err} when trying to get the adapter name for display #{path.TargetInfo.Id}");
                    Environment.Exit(1);
                }

                // Create an array of all the important display info we need to record
                List<string> displayInfo = new List<string>();
                displayInfo.Add("WINAPI");
                try
                {
                    displayInfo.Add(adapterInfo.AdapterDevicePath.ToString());
                }
                catch (Exception ex)
                {
                    SharedLogger.logger.Warn(ex, $"ProfileRepository/GenerateProfileDisplayIdentifiers: Exception getting Windows Display Adapter Device Path from video card. Substituting with a # instead");
                    displayInfo.Add("#");
                }
                try
                {
                    displayInfo.Add(targetInfo.OutputTechnology.ToString());
                }
                catch (Exception ex)
                {
                    SharedLogger.logger.Warn(ex, $"ProfileRepository/GenerateProfileDisplayIdentifiers: Exception getting Windows Display Connector Instance from video card. Substituting with a # instead");
                    displayInfo.Add("#");
                }
                try
                {
                    displayInfo.Add(targetInfo.EdidManufactureId.ToString());
                }
                catch (Exception ex)
                {
                    SharedLogger.logger.Warn(ex, $"ProfileRepository/GenerateProfileDisplayIdentifiers: Exception getting Windows Display EDID Manufacturer Code from video card. Substituting with a # instead");
                    displayInfo.Add("#");
                }
                try
                {
                    displayInfo.Add(targetInfo.EdidProductCodeId.ToString());
                }
                catch (Exception ex)
                {
                    SharedLogger.logger.Warn(ex, $"ProfileRepository/GenerateProfileDisplayIdentifiers: Exception getting Windows Display EDID Product Code from video card. Substituting with a # instead");
                    displayInfo.Add("#");
                }
                try
                {
                    displayInfo.Add(targetInfo.MonitorFriendlyDeviceName.ToString());
                }
                catch (Exception ex)
                {
                    SharedLogger.logger.Warn(ex, $"ProfileRepository/GenerateProfileDisplayIdentifiers2: Exception getting Windows Display Target Friendly name from video card. Substituting with a # instead");
                    displayInfo.Add("#");
                }
                /*try
                {
                    displayInfo.Add(pathDisplayTarget.TargetId.ToString());
                }
                catch (Exception ex)
                {
                    SharedLogger.logger.Warn(ex, $"ProfileRepository/GenerateProfileDisplayIdentifiers: Exception getting Windows Display Target ID from video card. Substituting with a # instead");
                    displayInfo.Add("#");
                }*/

                // Create a display identifier out of it
                string displayIdentifier = String.Join("|", displayInfo);
                // Add it to the list of display identifiers so we can return it
                // but only add it if it doesn't already exist. Otherwise we get duplicates :/
                if (!displayIdentifiers.Contains(displayIdentifier))
                {
                    displayIdentifiers.Add(displayIdentifier);
                    SharedLogger.logger.Debug($"ProfileRepository/GenerateProfileDisplayIdentifiers: DisplayIdentifier: {displayIdentifier}");
                }                
                

            }

            // Sort the display identifiers
            displayIdentifiers.Sort();

            return displayIdentifiers;
        }        

    }

    [global::System.Serializable]
    public class ProfileRepositoryException : Exception
    {
        public ProfileRepositoryException() { }
        public ProfileRepositoryException(string message) : base(message) { }
        public ProfileRepositoryException(string message, Exception inner) : base(message, inner) { }
        protected ProfileRepositoryException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}

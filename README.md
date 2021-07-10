# CCDInfo

CCDInfo is a test programme designed to exercise the CCD library that I developed for DisplayMagician. This little programme helps me validate that the library is working properly, and that it will work when added to the main DisplayMagician code.

This codebase is unlikely to be supported once DisplayMagician is working, but feel free to fork if you would like. Also feel free to send in suggestions for fixes to the C# CCD library interface. Any help is appreciated!

CCDInfo works using the Windows Display CCD interface to configure your display settings for you. You can set up your display settings exactly how you like them using Windows Display Setup, and then use CCDInfo to save those settings to a file.

CCDInfo records exactly how you setup your display settings, including screen position, resolution, HDR settings, and even which screen is your main one, and then CCDInfo saves those settings to a file. 

NOTE: CCDInfo doesn't handle NVIDIA Surround or AMD Eyefinity. 

You can store a unique CCDInfo settings file for each of your display configurations. Then you can use CCDInfo to load an apply those settings! 

Command line examples:

- Show what settings you currently are using: `CCDInfo`
- Save the settings you currently are using to a file to use later: `CCDInfo save my-cool-settings.cfg`
- Load the settings you saved earlier and use them now: `CCDInfo load my-cool-settings.cfg`


# (c) 2021 by Terry MacDonald
# Based on Set-Power.cfg by Rob Willis (admin@robwillis.info)
 
# If you want to add a new menu entry, just do the following:
# - Copy one of the existing Write-Host lines in the menu, paste it underneath the line you just copied, and change the number assigned to it.
# - Give the line a description for what sort of config file you want to load.
# - Copy one of the 'if("$input" -like "1"){ Run-CCDInfo file1.cfg}' lines and change the number to be the same as you put in the Write-Host line.
# - Also change the filename next to the Run-CCDInfo to point to the new config file you wish to use.

<#
.SYNOPSIS

Allows you to easily choose a CCDInfo display configuration from a menu and apply it.

.DESCRIPTION

CCDInfo works using the Windows Display CCD interface to configure your display settings for you. You can set up your display settings exactly how you like them using Windows Display Setup, and then use CCDInfo to save those settings to a file.
CCDInfo records exactly how you setup your display settings, including screen position, resolution, HDR settings, and even which screen is your main one, and then CCDInfo saves those settings to a file.

You can store a unique CCDInfo settings file for each of your display configurations. Then you can use this file to get CCDInfo to load and apply those settings!

Author: Terry MacDonald (terry.macdonald@gmail.com)
 
.EXAMPLE

./RunCCDInfo.ps1

.NOTES
This script must be placed in the same folder as CCDInfo.exe to work.

This script is menu driven. Just run it and select a menu option.

To save a CCDInfo configuration to use with this script, setup your displays how you want them, then run 'CCDInfo save <nameoffile>'
e.g.
    CCDInfo save triplescreen.cfg

Then you can edit this script to add the confguration to the menu so you can use it.

#>

# Supress errors
# $ErrorActionPreference= 'silentlycontinue'

# Pause
Function Pause {
	Read-Host “Press ENTER to continue...”
}

# Attempt to load and apply a CCDInfo display config file
Function Run-CCDInfo([string]$cfgFileIncludingPath) {
    $cmdToRun = CCDInfo load $cfgFileIncludingPath
    Out-Host "$cmdToRun"
    if($?) {            
        Write-Host "CCDInfo succcessfully applied the display configuration in " $cfgFileIncludingPath
    } else {            
        Write-Host "CCDInfo was unable to apply the display configuration in " $cfgFileIncludingPath
    }
	Pause
	Menu
}

# Quit
Function quit {
	exit
}

# Main Menu
Function Menu {
	# Clear the screen
	Clear

	# Write the options out so the user knows what to select
    Write-Host "Use CCDInfo to apply a display configuration"
    Write-Host "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"
	Write-Host " "
	Write-Host "Please select an option.`n"
	Write-Host "1 = Apply file1.cfg"
    Write-Host "2 = Apply file2.cfg"
	Write-Host "Q = Quit`n"
	
    # Read what the user selected and then do what the user wants
	$input = Read-Host -Prompt "Selection"
	Write-Host "`n"
	if("$input" -like "1"){ Run-CCDInfo file1.cfg}
	if("$input" -like "2"){ Run-CCDInfo file2.cfg}
	if("$input" -like "Q"){ quit }
	else {
	    Menu
	}
}

# Check that CCDInfo is in the path and fail if it's not
if (-not (Test-Path -Path "CCDInfo.exe" -PathType Leaf)){
    Write-Host "ERROR - This script will only work if it is in the same folder as CCDInfo.exe."
    Pause
    Exit 1
}

# Start the Main Menu when the script is run
Menu
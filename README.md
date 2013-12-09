FS Addon Installer
================

A command line utility to help install and uninstall Flight Simulator addons that otherwise require manual installation.

Usage
---

#### To install an addon

Drag a directory matching the file structure of Flight Simulator onto the program executable.  The files in the directory will be copied to Flight Simulator and an XML log will be created.

#### To uninstall an addon

Drag an FS Addon Installer XML log onto the program executable, and the the program will revert the changes (i.e. delete previously copied files and restore backups) as recorded in the installation log.

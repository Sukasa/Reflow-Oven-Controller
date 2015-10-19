# Reflow-Oven-Controller

  This is a .NET Micro Framework 4.3-based project meant to control a toaster reflow oven with Quartz upper elements and resistive lower elements.  A Web GUI for monitoring is included, along with a graphical user interface on the device itself.
  
## Setup

  This system is relatively easy to set up on the microcontroller side.  The included SDCard.zip file should be extracted to the root of a MicroSD card, 2GB or smaller.  Then, load the binaries onto a Netduino Plus 2, insert the MicroSD card, and the controller will be complete.  Implementing the hardware is left as an exercise for the reader.
  
## Profiles

  The oven is designed to operate using pre-configured 'Profiles' which direct it on what temperature to maintain inside the oven.  The pre-set profiles include a leaded reflow profile, rework preparation to pre-heat the board, and a pair of drying profiles used for moisture-sensitive components.  Profiles are converted from textual representations to binary data on each boot.
  
## User Interface

  The user interface of the oven allows you to select a profile to execute, set 'quick' buttons (up to 6 profiles that can be quickly loaded from the main or profile screens), check the IP address of the web GUI if active, and gain information about the design and firmware, including software contribution credits.
  
## Web Interface

  The web interface of the oven is a simple primarily static interface that allows users to remotely monitor the performance of the oven, and includes a REST Json endpoint to gain more detailed information about the operation of the oven.  No control of the oven is possible through the web interface.

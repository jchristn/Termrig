You will build Termrig, an Avalonia app that allows a user to create and save terminal profiles, and access terminal windows through the app.  Imagine a single window with several tabs, one per terminal window, where each tab has a terminal into the underlying machine.  The user should be able to specify the terminal type (e.g. bash, cmd.exe, PowerShell), dependent upon what is supported in the current host, a name for the tab, color scheme for the tab, and the starting directory, and a startup script.  The user should be able to save the collection of tabs as a profile so that when they open Termrig again, they can re-open the exact same set of terminal tabs in a new multi-tab terminal window again.  

It needs to be written in C#, and the MVP needs to support:
- Both cmd.exe and PowerShell
- an Avalonia app with profile management and a payload/tabbed terminal window
- the ability from the tabbed terminal window to "Save profile" and overwrite existing profiles
- From the main app page, the ability to manage profiles, create new, delete profiles, rename, and apply global color scheme to a profile (color scheme should be overridable in the terminal profile tab's settings)

Follow the code best practices in c:\code\agents for backend, testing, repository requirements, and UI/UX.  Repository is at https://github.com/jchristn/Termrig.  MIT license.  

Net-net - I want a terminal program that allows me to spawn multiple tabs, and save the collection of tabs as a profile so that when I open the terminal program I can say "open that profile back up and VOILA all of my tabs open in the right directories".

Ask any questions before beginning.

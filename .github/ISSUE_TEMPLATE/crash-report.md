---
name: Crash report
about: Report a crash in MatterControl

---

Crash Report
============

<!-- 🚨 STOP 🚨 𝗦𝗧𝗢𝗣 🚨 𝑺𝑻𝑶𝑷 🚨 -->

<!--
Before filing, check if the issue already exists (either open or closed) by using the search bar on the issues page. If it does, comment on the original issue. Even if it's closed, we can reopen it based on your comment.

Please COMPLETELY fill in the information below. You are must also upload a crash log.
-->

**MatterControl Build Number**
<!-- Example: 2.0.0-9999. To find this, go to the MatterControl menu in the top left and click "About MatterControl" -->

**Operating System Version**
<!-- Examples: Windows 10, macOS 10.14, Ubuntu 18.10, Android -->

**Printer Make/Model**
<!-- Example: Pulse C-232 -->

**Steps to Reproduce**
<!-- Describe how to make the crash happen. Start at the very beginning (opening MatterControl) and explain everything you did to make the crash occur, step by step. Follow the steps yourself and make sure that you can get the crash to happen again. If it is difficult to explain, then you are encouraged to include screenshots or a screen recording. If the crash is related to a particular file you are working with, include it in the next section. -->

1. 
2. 
3. 

**Other Attachments**
<!-- Include any other files related to the crash here. You may have to pack them in a .zip in order to upload them to GitHub. For instance, this might include the STL file you were trying to print. If the crash is related to slicing, then also include your exported printer settings. To export your settings, go to the three dots menu in the top right of MatterControl (next to the hot end controls) and choose "Export All Settings". -->

**Crash Log**
<!--
A crash log is required for us to determine what went wrong. If you do not include a crash log, your report will be closed. Follow the instructions for your platform to collect the log.

WINDOWS

    1. Right click on ‘My Computer’ and select ‘Manage.’
    2. In the menu on the left, navigate to ‘System Tools’ > ‘Event Viewer’ > ‘Windows Logs’.
    3. Click on ‘Application’ to show logs in the middle window pane, then click ‘Filter Current Log’ in the right window pane, then check ‘Error’ and hit ‘OK’.
    4. In the right window pane, choose ‘Save Filtered Log File As’. Give the file (should be in .evtx format) a descriptive name.
    5. Attach the .evtx file to this report.

MAC

    1. Close any open instances of MatterControl.
    2. Go to the 'Applications' folder.
    3. Locate the MatterControl.app file.
    4. Right-click and select "Show Package Contents".
    5. Navigate to 'Contents' > 'MacOS'.
    6. Double click the 'MatterControlMac' file (this will open a terminal window and start the application).
    7. Copy the output. If MatterControl crashes upon startup copy the message that appears in the terminal window immediately. If the crash is triggered by a specific event, recreate the event and then copy the message that appears.
    8. Paste the output below, between the triple tick marks.

LINUX

    Run `mattercontrol` from a terminal. Copy all output and paste it below, between the triple tick marks.

ANDROID (MatterControl T10, T7X)

    Crash logs are difficult to collect from Android. If possible, try to make the same crash happen on a PC and collect the log from there.
-->

```
Paste crash log here
```

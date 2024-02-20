# WSJTX-Controller-v2
<br>Assistant for WSJT-X (the ham radio weak-signal digital modes program).
<br>Modified WSJT-X is required, see installer releases at
<br>https://github.com/avantol/WSJT-X_2.7.0/releases/tag/v2.7.0-172
<br>More details at https://www.qrz.com/db/WM8Q
<br><br>At first run, basic mode:
<br><img src="https://github.com/avantol/WSJTX-Controller-v2/blob/main/ctrlv2_Init.JPG">
<br><br>Later, advanced mode:
<br><img src="https://github.com/avantol/WSJTX-Controller-v2/blob/main/ctrlv2.JPG">
<br><br>Tips:
<br><br>The Controller and the modified WSJT-X program run as a "versioned" pair, and the Controller checks for the correct WSJT-X version when it starts. Be sure to download and install both programs!
<br><br>If you already have another WSJT-X version installed: You can install the required (modified) WSJT-X 2.7.0 program in an alternate destination folder if you like. Neither WSJT-X version will interfere with the other, and they share the same settings and preferences... convenient!
<br><br>When the Controller is not running, the modified WSJT-X 2.7.0 "forgets" its modifications and runs like the standard unmodified version. 
<br><br>The UDP address/port for the WSJT-X Controller "UDP Server" is detected automatically by the Controller.
<br><br>For best results, set the WSJT-X "UDP Server" (Settings | Reporting tab) to address 239.255.0.0 and port 2237, with all "Outgoing interfaces" selected.
<br><br>If you experience any problems with the Controller, close all other programs that interface with WSJT-X, then re-open them one at a time to determine which one causes the problem.
<br><br>It's best to use JTAlert in a passive mode, where it does not forward log data or control WSJT-X. This has caused problems for several users. 
<br><br>HRD is reported to not inter-operate well with the Controller, when JTAlert is used to forward log data to HRD.
<br><br>DX Aggregator is reported to conflict with the communication between the Controller and WSJT-X.
<br><br>Logger32 does not inter-operate with the Controller, since it is (apparently) designed to "run" WSJT-X by itself.
<br><br>To get QSOs with stations "per mode" (ex: work an FT8 station, and later work the same station using FT4), in WSJT-X on the "Colors" tab, select "Highlight by Mode".
<br><br>The Controller does not work for contests, since contests are designed as tests of skill, not automated assistance. Contest calls and modes are specifically and purposely ignored.
<br><br>The Controller is disabled when Hound is selected, since call traffic management is done properly by the Fox station.

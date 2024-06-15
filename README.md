# Otto
<br>Assistant for WSJT-X (the extremely popular amateur radio weak-signal digital modes program).
<br>Modified WSJT-X is required, see installer releases at
<br>https://github.com/avantol/WSJT-X_2.7.0/releases/tag/v2.7.0-185
<br>More details at https://www.qrz.com/db/WM8Q
<br><br><b><i>Use all appropriate caution since transmit can start at any time once you select "Enable Tx" in WSJT-X!!! Whenever the "Enable Tx" button is red, your antenna can be energized.</i></b>
<br><br>At first run, basic mode:
<br><img src="https://github.com/avantol/WSJTX-Controller-v2/blob/main/ctrlv2_Init.JPG">
<br><br>Later, advanced mode:
<br><img src="https://github.com/avantol/WSJTX-Controller-v2/blob/main/ctrlv2.JPG">
<br><br>As a start, Otto stores up calls that are interesting to you that come in while you're working another call, then replies to each in turn.
<br><br>You can also do things like:
<br>- call CQ, or listen for interesting stations (listening conserves bandwidth)
<br>- reply exclusively and repeatedly to calls from "rare DX" stations and expeditions
<br>- call "CQ DX" and ignore non-DX replies
<br>- reply automatically to "CQ DX" from stations that are actually DX to you
<br>- reply automatically to local or DX stations you haven't worked yet
<br>- reply automatically to CQs from POTA or SOTA stations
<br>- reply automatically to multi-stream stations
<br>- automatically detect and transmit on the clearest section of a congested band
<br>- manually queue up the interesting calls for automatic reply
<br>- prioritize replies by distance, SNR, azimuth, or order received
<br>- bypass WSJT-X's clunky way of skipping the grid message or using RR73
<br>- transmit multiple directed CQs of your choice, for those hard-to-get QSOs
<br>- optimize your success rate by logging as soon as 2-way signal reports are confirmed
<br>- sound an alert when someone wants your state or country
<br>- reply automatically to CQs directed to your state or country
<br>- never miss a "late" 73 again... instead, it gets logged
<br>- automatically start and/or stop transmitting at specified time(s)
<br>- reply to new countries "by mode" (FT8, FT4, etc.)
<br>- call whoever someone else is calling (like that rare DX!) with just one action
<br>- have much more time to research callers on QRZ, PSKReporter, and LOTW...
<br><br>Tips:
<br><br>Otto and the modified WSJT-X program run as a "versioned" pair, and Otto checks for the correct WSJT-X version when it starts. Be sure to download and install both programs!
<br><br>If you already have another WSJT-X version installed: You can install the required (modified) WSJT-X 2.7.0 program in an alternate destination folder if you like. Neither WSJT-X version will interfere with the other, and they share the same settings and preferences... convenient!
<br><br>When Otto is not running, the modified WSJT-X 2.7.0 "forgets" its modifications and runs like the standard unmodified version. 
<br><br>The UDP address/port for the WSJT-X Controller "UDP Server" is detected automatically by Otto.
<br><br>For best results, set the WSJT-X "UDP Server" (Settings | Reporting tab) to address 239.255.0.0 and port 2237, with all "Outgoing interfaces" selected.
<br><br>If you experience any problems with Otto, close all other programs that interface with WSJT-X, then re-open them one at a time to determine which one causes the problem.
<br><br>It's best to use JTAlert in a passive mode, where it does not forward log data or control WSJT-X. This has caused problems for several users. 
<br><br>HRD is reported to not inter-operate well with Otto, when JTAlert is used to forward log data to HRD.
<br><br>DX Aggregator is reported to conflict with the communication between Otto and WSJT-X.
<br><br>Logger32 does not inter-operate with Otto, since it is (apparently) designed to "run" WSJT-X by itself.
<br><br>To get QSOs with stations "per mode" (ex: work an FT8 station, and later work the same station using FT4), in WSJT-X on the "Colors" tab, select "Highlight by Mode".
<br><br>Otto does not work for contests, since contests are designed as tests of skill, not automated assistance. Contest calls and modes are specifically and purposely ignored.
<br><br>Otto is disabled when Hound is selected, since call traffic management is done properly by the Fox station.

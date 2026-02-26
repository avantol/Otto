# Otto (add-on assistant for WSJT-X and Decodium "Raptor")
<br>Otto is your assistant for WSJT-X (the extremely popular amateur radio weak-signal digital modes program).
<br>Click here > <b>https://github.com/avantol/Otto/releases/latest</b> for download and installation instructions. 
<br><br><b><i>Use all appropriate caution since transmit can start at any time once you select "Enable Tx" in WSJT-X!!! Whenever the "Enable Tx" button is red, your antenna can be energized.</i></b>
<br><br><a href="https://github.com/avantol/Otto/releases/latest">
<img src="https://github.com/avantol/WSJTX-Controller-v2/blob/main/otto_ft2.JPG"></a>
<br><br>Otto stores up calls that are interesting to you that come in while you're working another call, then replies to each in turn.
<br><br>You can also do things like:
<br>- call CQ, or listen for interesting stations (listening conserves bandwidth)
<br>- reply exclusively and repeatedly to calls from "rare DX" stations and expeditions
<br>- work around Decodium Raptor's logging to Logbook of the World until "FT2" is accepted there
<br>- call "CQ DX" and ignore non-DX replies
<br>- reply automatically to "CQ DX" from stations that are actually DX to you
<br>- reply automatically to local or DX stations you haven't worked yet
<br>- reply automatically to CQs from POTA stations
<br>- reply automatically to multi-stream stations
<br>- automatically detect and transmit on the clearest section of a congested band
<br>- manually queue up any other interesting calls for automatic reply
<br>- prioritize replies by distance, SNR, azimuth, or order received
<br>- bypass WSJT-X's clunky way of skipping the grid message or using RR73
<br>- skip replies to RR73 (since RR73 is defined as "roger and out")
<br>- transmit multiple different directed CQs
<br>- log as soon as 2-way signal reports are confirmed
<br>- reply automatically to CQs directed to your state or country
<br>- never miss a "late" 73 again... instead, it gets logged
<br>- automatically start and/or stop transmitting at specified time(s)
<br>- reply to calls that are new for a specific band in addition to new for any band 
<br>- gain a huge advantage in QSOs with new DXCCs and DXpeditions by instantly and persistently replying 
<br><br>Click here for the full feature list for all WM8Q projects -> https://docs.google.com/spreadsheets/d/e/2PACX-1vRYuHHO7EpAjSYfSCHZycaMdFZfYVkFyqPz9biFtmnS9uukw43fYYqEBZyhV0WtQyde50OqF96b1Ao_/pubhtml?gid=0&single=true
<br>The feature list also contains download links for all projects.
<br><br>Tips:
<br><br>Otto and a modified WSJT-X program (or Decodium "Raptor") run as a "versioned" pair, and Otto checks for the correct WSJT-X/Raptor version when it starts. If you're not using Raptor, be sure to download the Modified WSJT-X at the <a href="https://github.com/avantol/Otto/releases/latest">"Releases" page</a>!
<br><br>If you already have another WSJT-X version installed: You can install the required (modified) WSJT-X (v3.0.0 RC1 or v2.7.0 GA or Decodium "Raptor") program in an alternate destination folder if you like. Neither WSJT-X version will interfere with the other, and they share the same settings and preferences... convenient!
<br><br>When Otto is not running, the modified WSJT-X (3.0.0 RC1 or 2.7.0 GA or "Raptor") "forgets" its modifications and runs like the standard unmodified version. 
<br><br>The UDP address/port for the WSJT-X "UDP Server" is detected automatically by Otto.
<br><br>For best results inter-operating with other WSJT-X helper programs (JTalert, GridTracker, etc.), set the WSJT-X "UDP Server" (Settings | Reporting tab) to address 239.255.0.0 and port 2237, with all "Outgoing interfaces" selected.
<br><br>If you experience any problems with Otto, close all other programs that interface with WSJT-X, then re-open each one at a time to determine which one causes the problem.
<br><br>It's best to use JTAlert in a passive mode, where it does not forward log data or control WSJT-X. This has caused problems for several users. 
<br><br>HRD is reported to not inter-operate well with Otto, when JTAlert is used to forward log data to HRD.
<br><br>DX Aggregator is reported to conflict with the communication between Otto and WSJT-X.
<br><br>Logger32 does not inter-operate with Otto, since it is (apparently) designed to "run" WSJT-X by itself.
<br><br>To get QSOs with stations "per mode" (ex: work an FT8 station, and later allow working the same station using FT4), in WSJT-X on the "Colors" tab, select "Highlight by Mode".
<br><br>Otto does not work for contests, since contests are designed as tests of skill, not automated assistance. Contest calls and modes are specifically and purposely ignored.
<br><br>Otto is disabled when Hound is selected, since call traffic management is done properly by the Fox station.

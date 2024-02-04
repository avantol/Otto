# WSJTX-Controller
 Provides automation for repetitive manual tasks in WSJT-X

WSJTX Controller is a "helper" program that runs alongside WSJT-X, apparently the most popular ham radio program of all time. This "helper" does a lot of the boring WSJT-X manual work for you. (You will need to install a slightly-modified WSJT-X program, available at https://github.com/avantol/WSJT-X2-3-0/blob/main/wsjtx-2.3.0-win64.exe, more info below)

Main project status page is at https://www.qrz.com/db/WM8Q

![image](https://user-images.githubusercontent.com/5302633/103505545-b7374880-4e17-11eb-816f-cec81104d97c.png)

Now you can do things like:
- reply to CQs from caller you haven't worked yet,
- queue up the interesting calls,
- never miss a "late" 73 again,
- bypass WSJT-X's clunky way of skipping the grid message or using RR73, or
- sequence randomly through your choice of directed CQs, for those hard-to-get QSOs, or
- optimize your success rate by logging as soon as both signal reports are confirmed, or
- sound an alert when someone wants your state or country, or 
- (best of all) call whoever someone else is calling (like that rare DX!) with just one click.

That last option sure beats copying and pasting a call sign, changing the Tx period, and selecting the QSO phase in WSJT-X. While you were doing all that, someone else got their call answered!

Have you ever had a "73" finally come in from a DX you'd been working, but you've long since moved on to other calls? How do you log that? You could scramble like crazy and try clicking on old decodes and try to get the signal reports right, but it doesn't really work, and it's a mess. But now, never again... the Controller saves everything, and accurately logs that late "73" decode, automatically... while you're working on other calls!

The Controller can keep busy when not answering replies by replying to CQs, with priority given to directed CQs you specify.

As you run WSJT-X manually, your "helper" gets out of the way. But, if you need a break from the action, just stop and kick back.... your "helper" takes over for you... go get a snack or rest your eyes. 

The "helper" also works perfectly with JTAlert, GridTracker, and logging add-ons (the helper uses the now-popular UDP multicast protocol).

You can continue to use the familiar WSJT-X user interface (and keep up with with all the new features, like FST4 and FST4W), instead of learning a confusing "alternate" FT8 program with it's predictably badly-designed screens.

Interested? Let me know. If you use it, and want something additional, I'm open to possibly modifying it *for you* (not all hams write code!). (moar.avantol at xoxy.net)

About WSJT-X automation: My take is that FT8 is  no different from "two-way WSPR", except you can advance toward your goals (like WAS and DXCC) as you test and improve your system!!! Isn't the only real goal of FT8 to collect call signs? Isn't it helpful to other hams when you can do it more efficiently? It cetainly matters to me, being in Wyoming... I'm flooded with constant calls and requests.

There's no better way to optimize your antenna than by testing the transmit and receive performance simultaneously as you make those experimental changes. In contrast, WSPR analytics are intended to analyze only your transmissions (see DXplorer.net for these very valuable analytics). 

Note: The rules about "control operator present" apply, of course. This program helps you, it doesn't replace you. I run it while I'm doing more-productive QSO tasks, like scanning the list of callers (and those called), deciding who gets priority, looking at PSKreporter or QRZ.com, responding to WAS requests, improving this program, etc. If you have strong opinions either way, please email me! (moar.avantol at xoxy.net)

Notes on installing the modified WSJT-X program:
- If you already have WSJT-X installed, you may want to install the modified WSJT-X program in an alternate destination folder. Neither version will interfere with the other, you can run both at will, and they share the same settings and preferences.
- The UDP address/port for the WSJT-X Controller "listener" role (about the same as a "server" role here) is set to 239.255.0.0 and port 2237, same as GridTracker uses. Note that JTAlert configures the listener port dynamically. Be sure to set the WSJT-X "UDP Server" (Settings|Reporting tab) to address 239.255.0.0 and port 2237, all check boxes ticked.
- Be sure to set your Tx Watchdog in WSJT-X to something like 30 minutes or more (use File | Settings, or F2 key). The new meaning of the Tx watchdog is: In case WSJT-X Controller loses contact with WSJT-X, after 30 minutes (for example), WSJT-X will stop transmitting.
- The source code for the modified WSJTY-X is supplied for your inspection, or modification, if you like: https://github.com/avantol/WSJT-X2-3-0
- The modifications to WSJT-X are minimal, allowing easy modification to future versions of WSJT-X as they are released (release candidates are not included in this, they change fast and break things).
- Modifying WSJT-X as *lightly as possible* was the whole idea behind the QSJT-X Controller. I *heavily* modified WSJT-X 2.0.0 two years ago (https://sourceforge.net/u/k9avt/wsjt/ci/master/tree/), and knew that I would not want to port the changes to later versions. So, using UDP to link the controller and a lightly-modified WSJT-X makes this effort "future-proof".

--end--



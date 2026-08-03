' --------------------------------------------------------------------------------
' ASCOM Camera driver for Lumix

'This driver provides an interface to the Lumix http over wifi remote control protocol
'in order to present lumix cameras as ASCOM cameras and be used by astro photo SW like APT or Indi 
'The camera believes that it is connected to the Panasonic ImageApp

'Driver has been tested with the G80 but shouldwork with all Wifi Lumix using the same sensor size.

'16MP sensor was the prototype. Now it is possible to work with a range of 
' - 10M (GH5S) 
' - 12MP(GH1) 
' - 16MP (GH4, G80)
' - 20MP (GH5, G9 etc).


'To connect to the camera:
'1) On the camera (similar to what is needed with the Panasonic ImageApp)
'	a) set it to "M"
'	b) connect to a wifi network (best if local hotspot_
'	c) Camera waits for an app to connect 
'2) on the PC
'	a) launch the Imaging SW (e.g. APT)
'	b) chose the Lumix Ascom from the chooser window
'	c) click properties
'	d) the driver will look for the Lumix camera on the local network and connect to it (the camera should say "under remote control")
'	e) set the ISO, Speed and Transfer mode (JPG or Raw): read below for details
'   f) select the correct resolution for your camera. I hope to make it "discoverable" soon)  
'	g) Temp folder to store the file from the camera.
''	h) hit ok.

'The driver allows to set the speed,iso and format (RAW or RAW+JPG) of the camera  
'transfers the image (Raw or JPG) on the PC and exposes the image array in RGB.

'It relies on LibRaw to handle the Raw format, or the native VB.NET imaging for JPG
'Images are then translated into Tiff and then passed to the image array.

'RAW would be preferred but the file is substantially larger and therefore longer to tranfer.
'therefore the download is often interrupted. the driver tries to recover/continue the DL but it does not always works
'this leaves with an incomplete RAW file that is still passed on but not ideal. 

'Given the longer transfer time it substantially cuts into the active shooting since all this process is sequential
'So if you have a 1mn exposure and it takes 40s to get it onto your driver that is 40s you are not shooting...

'Hence the jpg transfer option. file is smaller and transfer faster and should still be valuable for the Astro SW.
'in any case the camera keeps the RAW or the RAW+jpg on the SD card and the Astro SW should have a fits file from the driver.
'the transfered files (jpg or raw) and intermediary tiff files are deleted as soon as needed in order to save disk space.
'code is quite nasty and could use some factoring into further utility classes/methods etc.

'I added a "thumb" transfer mode ehich takes a large thumbnail of the image  (1440x1080) in order to further reduce the trnasfer size. 
' not sure if this helps much and if it will screw up the platesolving since now resolution is different from the actual sensor size. 
'in this case though the pixelpitch is changed in the driver so to help in that process.

' Implements:	ASCOM Camera interface version: 1.0
' Author:		robert hasson robert_hasson@yahoo.com
'
' Edit Log:
'
' Date			Who	Vers	Description
' -----------	---	-----	-------------------------------------------------------
' 01-03-2019	RHA	1.0.0	Initial edit, from Camera template
' ---------------------------------------------------------------------------------
'
'
' Your driver's ID is ASCOM.Lumix.Camera
'
' The Guid attribute sets the CLSID for ASCOM.DeviceName.Camera
' The ClassInterface/None addribute prevents an empty interface called
' _Camera from being created and used as the [default] interface
'

' This definition is used to select code that's only applicable for one device type
#Const Device = "Camera"

Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Net
Imports System.Windows.Media.Imaging
Imports System.Xml
Imports System.Xml.Linq
Imports ASCOM.Astrometry
Imports ASCOM.Astrometry.AstroUtils
Imports ASCOM.DeviceInterface
Imports ASCOM.Utilities
Imports System.Threading
Imports System.Runtime.Remoting.Messaging
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography.X509Certificates

'Imports UPNPLib

<Guid("08832ede-d16d-4090-b661-91f670d95f4d")>
<ClassInterface(ClassInterfaceType.None)>
Public Class Camera

    ' The Guid attribute sets the CLSID for ASCOM.Lumix.Camera
    ' The ClassInterface/None addribute prevents an empty interface called
    ' _Lumix from being created and used as the [default] interface

    ' TODO Replace the not implemented exceptions with code to implement the function or
    ' throw the appropriate ASCOM exception.
    '
    Implements ICameraV2

    '
    ' Driver ID and descriptive string that shows in the Chooser
    '
    Public Const driverID As String = "ASCOM.Lumix.Camera"
    Public Const driverDescription As String = "Lumix Camera"


    '----- Lumix constants ------
    ' Per-connection state — instance, NOT Shared, so two Camera objects in one
    ' process don't clobber each other's IP / model / settings.
    Public MODEL As String = "LUMIX"

    '----- HTTP ------------
    Private ReadOnly USER_AGENT As String = "Mozilla/5.0"

    'list of html commads to talk to the lumix camera
    'Cam Info
    Public Shared DEVICE As String = "cam.cgi??mode=setsetting&type=device_name&value=SM-G9350"
    Public Shared SECURITY As String = "cam.cgi?mode=accctrl&type=req_acc&value=4D454930-0100-1000-8001-024500021C98&value2=SM-G903F"
    Public Shared STATE As String = "cam.cgi?mode=getstate"
    Public Shared CAPABILITY As String = "cam.cgi?mode=getinfo&type=capability"
    Public Shared ALLMENU As String = "cam.cgi?mode=getinfo&type=allmenu"
    Public Shared CURMENU As String = "cam.cgi?mode=getinfo&type=curmenu"
    Private LENSINFO As String = "cam.cgi?mode=getinfo&type=lens"

    'Cam Network
    Private STARTSTREAM As String = "cam.cgi?mode=startstream&value=49199"

    'Cam Settinggs
    Public Shared ISO As String = "cam.cgi?mode=setsetting&type=iso&value="
    Public Shared SHUTTERSPEED As String = "cam.cgi?mode=setsetting&type=shtrspeed&value="
    Public Shared CDS_Control As String = ":60606/Server0/CDS_control"

    'Soap Envelop for UPNP
    Public Shared Function SoapEnvelop(start, num) As String
        Dim Envelop As String = "<?xml version=""1.0"" encoding=""utf-8""?>" &
"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
        <s:Body> 
        <u:Browse xmlns:u=""urn:schemas-upnp-org:service:ContentDirectory:1"" xmlns:pana=""urn:schemas-panasonic-com:pana""> 
        <ObjectID>0</ObjectID> 
        <BrowseFlag>BrowseDirectChildren</BrowseFlag> 
        <Filter>*</Filter> 
        <StartingIndex>" & start & "</StartingIndex><RequestedCount>" & num & "</RequestedCount><SortCriteria></SortCriteria>
        <pana:X_FromCP>LumixLink2.0</pana:X_FromCP>
        </u:Browse>
        </s:Body>
        </s:Envelope>"

        Return Envelop
    End Function

    'Cam Commands
    Public Shared NUMPIX As String = "cam.cgi?mode=get_content_info"
    Public Shared GETSTATE As String = "cam.cgi?mode=getstate"

    Private RECMODE As String = "cam.cgi?mode=camcmd&value=recmode"
    Public Shared PLAYMODE As String = "cam.cgi?mode=camcmd&value=playmode"
    Private SHUTTERSTART As String = "cam.cgi?mode=camcmd&value=capture"
    Private SHUTTERSTOP As String = "cam.cgi?mode=camcmd&value=capture_cancel"
    Private SETAPERTURE As String = "cam.cgi?mode=setsetting&type=focal&value="
    Public Shared QUALITY As String = "cam.cgi?mode=setsetting&type=quality&value="


    Friend Shared traceState As Boolean
    Public TransferFormat As String
    Friend IPAddress As String

    Private connectedState As Boolean ' Private variable to hold the connected state
    Private utilities As Util ' Private variable to hold an ASCOM Utilities object
    Private astroUtilities As AstroUtils ' Private variable to hold an AstroUtils object to provide the Range method
    Private TL As TraceLogger ' Private variable to hold the trace logger object (creates a diagnostic log file with information that you specify)
    Private TiffFileName As String

    Friend Shared DCrawPath As String '= "C:\Users\robert.hasson\source\repos\LumixCamera\packages\NDCRaw.0.5.2\lib\net461\dcraw-9.27-ms-64-bit.exe"
    Friend TempPath As String '= "C:\Users\robert.hasson\Documents\XMLLumix\"
    Friend Shared IPAddressDefault As String = "localhost"
    '  Public Shared outputarray As New NDCRaw.DCRawResult
    Public ROM = {"JPG", "RAW", "Thumb"}
    Private JPEGPixelOffset As Int16 = 20
    Public ROMAL As New ArrayList
    Public ISOTableAL As New ArrayList
    Public Shared Models As New Hashtable
    Public CurrentROM As UShort
    Public CurrentISO As UShort
    Public CurrentSpeed As String
    Private CurrentState As CameraStates = CameraStates.cameraIdle
    Private CurrentPercentCompleted As Int32 = 0


    Public Shared ISOTable = {"auto", "i_iso", "80", "100", "125", "160", "200", "250", "320", "400", "500", "640", "800", "1000", "1250", "1600", "2000", "2500", "3200", "4000", "5000", "6400", "8000", "10000", "12800", "16000", "20000", "25600"}
    Public Shared ShutterTable =
  {{"3328/256", "8000"},
    {"3243/256", "6400"},
    {"3158/256", "5000"},
    {"3072/256", "4000"},
    {"2987/256", "3200"},
    {"2902/256", "2500"},
    {"2816/256", "2000"},
    {"2731/256", "1600"},
    {"2646/256", "1300"},
    {"2560/256", "1000"},
    {"2475/256", "800"},
    {"2390/256", "640"},
    {"2304/256", "500"},
    {"2219/256", "400"},
    {"2134/256", "320"},
    {"2048/256", "250"},
    {"1963/256", "200"},
    {"1878/256", "160"},
    {"1792/256", "125"},
    {"1707/256", "100"},
    {"1622/256", "80"},
    {"1536/256", "60"},
    {"1451/256", "50"},
    {"1366/256", "40"},
    {"1280/256", "30"},
    {"1195/256", "25"},
    {"1110/256", "20"},
    {"1024/256", "15"},
    {"939/256", "13"},
    {"854/256", "10"},
    {"768/256", "8"},
    {"683/256", "6"},
    {"598/256", "5"},
    {"512/256", "4"},
    {"427/256", "3.2"},
    {"342/256", "2.5"},
    {"256/256", "2"},
    {"171/256", "1.6"},
    {"86/256", "1.3"},
    {"0/256", "1"},
    {"-85/256", "1.3s"},
    {"-170/256", "1.6s"},
    {"-256/256", "2s"},
    {"-341/256", "2.5s"},
    {"-426/256", "3.2s"},
    {"-512/256", "4s"},
    {"-598/256", "5s"},
    {"-682/256", "6s"},
    {"-768/256", "8s"},
    {"-853/256", "10s"},
    {"-938/256", "13s"},
    {"-1024/256", "15s"},
    {"-1109/256", "20s"},
    {"-1194/256", "25s"},
    {"-1280/256", "30s"},
    {"-1365/256", "40s"},
    {"-1450/256", "50s"},
    {"-1536/256", "60s"},
    {"16384/256", "B"}
    }


    '    {{"3072/256", 4000},
    '        {"2987/256", 3200},
    '        {"2902/256", 2500},
    '        {"2816/256", 2000},
    '        {"2731/256", 1600},
    '        {"2646/256", 1300},
    '        {"2560/256", 1000},
    '        {"2475/256", 800},
    '        {"2390/256", 640},
    '        {"2304/256", 500},
    '        {"2219/256", 400},
    '        {"2134/256", 320},
    '        {"2048/256", 250},
    '        {"1963/256", 200},
    '        {"1878/256", 160},
    '        {"1792/256", 125},
    '        {"1707/256", 100},
    '        {"1622/256", 80},
    '        {"1536/256", 60},
    '        {"1451/256", 50},
    '        {"1366/256", 40},
    '        {"1280/256", 30},
    '        {"1195/256", 25},
    '        {"1110/256", 20},
    '        {"1024/256", 15},
    '        {"939/256", 13},
    '        {"854/256", 10},
    '        {"768/256", 8},
    '        {"683/256", 6},
    '        {"598/256", 5},
    '        {"512/256", 4},
    '        {"427/256", 3.2},
    '        {"342/256", 2.5},
    '        {"256/256", 2},
    '        {"171/256", 1.6},
    '        {"86/256", 1.3},
    '        {"0/256", 1},
    '        {"-85/256", "1.3s"},
    '        {"-170/256", "1.6s"},
    '        {"-256/256", "2s"},
    '        {"-341/256", "2.5s"},
    '        {"-426/256", "3.2s"},
    '        {"-512/256", "4s"},
    '        {"-512/256", "5s"},
    '        {"-682/256", "6s"},
    '        {"-768/256", "8s"},
    '        {"-853/256", "10s"},
    '        {"-938/256", "13s"},
    '        {"-1024/256", "15s"},
    '        {"-1109/256", "20s"},
    '        {"-1194/256", "25s"},
    '        {"-1280/256", "30s"},
    '        {"-1365/256", "40s"},
    '        {"-1450/256", "50s"},
    '        {"-1536/256", "60s"},
    '        {"16384/256", "B"}
    '}

    Public Shared ResolutionTable = {"10.2", "12.1", "16.0", "16.05", "20.0", "20.3", "24.2", "47.3", "25.2", "20.1", "44.3"}


    '
    ' Constructor - Must be public for COM registration!
    '
    ''' <summary>
    ''' 
    ''' </summary>
    Public Sub New()

        connectedState = False ' Initialise connected to false
        utilities = New Util() ' Initialise util object
        astroUtilities = New AstroUtils 'Initialise new astro utiliites object
        IPAddressDefault = "localhost"

        TL = New TraceLogger("", "Lumix")
        TL.Enabled = My.Settings.TraceEnabled 'traceState
        TL.LogMessage("Camera", "Starting initialisation")

        ROMAL.Add("JPG")
        ROMAL.Add("RAW")
        ROMAL.Add("Thumb")

        ' Derive the gain list from the single ISOTable source so the two lists can
        ' never diverge. The old hand-maintained copy was missing "1250" and used
        ' "i_auto" where ISOTable (and the value actually sent to the camera) uses
        ' "auto" — that mismatch made Auto/1250 map to the wrong gain index.
        ' Skip the non-numeric entries ("auto", "i_iso"): ASCOM Gain is an index into
        ' Gains and every entry must be a real, selectable gain. With them present,
        ' gain index 0 selected Auto ISO - the camera then picks its own sensitivity,
        ' which is never what an imaging client asking for a specific gain wants.
        For Each isoValue As String In ISOTable
            Dim numericIso As Integer
            If Integer.TryParse(isoValue, numericIso) Then ISOTableAL.Add(isoValue)
        Next

        If Models.Contains("GX9") = False Then
            Models.Add("GX9", "20.3")
            Models.Add("GF10", "16.0")
            Models.Add("GH5S", "10.2")
            Models.Add("G9", "20.0")
            Models.Add("G90", "20.0")
            Models.Add("GH5", "20.3")
            Models.Add("GF9", "16.0")
            Models.Add("GX800", "16.0")
            Models.Add("GX850", "16.0")
            Models.Add("G80", "16.0")
            Models.Add("G81", "16.0")
            Models.Add("G85", "16.0")
            Models.Add("GX80", "16.0")
            Models.Add("GX85", "16.0")
            Models.Add("G95", "20.0")
            Models.Add("GF8", "16.0")
            Models.Add("GX8", "20.3")
            Models.Add("G7", "16.0")
            Models.Add("GF7", "16.0")
            Models.Add("GM5", "16.0")
            Models.Add("GM1", "16.0")
            Models.Add("GX7", "16.0")
            Models.Add("GH4", "16.05")
            Models.Add("GH6", "25.2")
            Models.Add("GH3", "16.05")
            Models.Add("GH2", "16.05")
            Models.Add("GH1", "12.1")
            Models.Add("G6", "16.05")
            Models.Add("GF6", "16.0")
            Models.Add("S1", "24.2")
            Models.Add("S5", "24.2")
            Models.Add("S1R", "47.3")
            Models.Add("S1H", "24.2")
            Models.Add("BGH1", "10.2")
            Models.Add("G100", "20.3")
            Models.Add("FZ1000", "20.1")

            ' 2023-2025 bodies. Keys are the cam.cgi model string (Panasonic "DC-"
            ' prefix stripped, matching the entries above). Both the "M2" and "II"
            ' spellings are registered because the exact string a body reports is
            ' not confirmed for all of them; a wrong key is harmless (the connect
            ' path falls back to a default resolution rather than crashing).
            '   24.2MP full-frame sensor (S5II family / S9 / S1II / BS1H)
            Models.Add("S5M2", "24.2") : Models.Add("S5II", "24.2")    ' S5 II
            Models.Add("S5M2X", "24.2") : Models.Add("S5IIX", "24.2")  ' S5 IIX
            Models.Add("S9", "24.2")
            Models.Add("S1M2", "24.2") : Models.Add("S1II", "24.2")    ' S1 II (24.1MP)
            Models.Add("S1M2E", "24.2") : Models.Add("S1IIE", "24.2")  ' S1 IIE
            Models.Add("BS1H", "24.2")
            '   25.2MP Micro Four Thirds sensor (G9 II / GH7)
            Models.Add("G9M2", "25.2") : Models.Add("G9II", "25.2")    ' G9 II
            Models.Add("GH7", "25.2")
            '   44.3MP full-frame sensor (S1R II) - dims from digicamdb
            Models.Add("S1RM2", "44.3") : Models.Add("S1RII", "44.3")  ' S1R II
            '   20.3MP Micro Four Thirds bodies previously missing from the list
            Models.Add("GH5M2", "20.3") : Models.Add("GH5II", "20.3")  ' GH5 II
            Models.Add("G110", "20.3")
            Models.Add("G100D", "20.3")
            ' NOTE: FZ82/FZ80 (issue #8) is an 18.1MP 1/2.3" bridge compact - it
            ' needs its own resolution bucket and sensor size, not added here; its
            ' crash-on-select is handled by the connect-path guard regardless.

        End If
        '"10.2", "12.1", "16.0", "16.05", "20.0", "20.3", "24.2", "47.3"

        Resolutions(0)._resolution = "12.1"
        Resolutions(0)._X = 4011
        Resolutions(0)._Y = 3016

        Resolutions(1)._resolution = "16.0"
        Resolutions(1)._X = 4612
        Resolutions(1)._Y = 3468

        Resolutions(2)._resolution = "20.0"
        Resolutions(2)._X = 5200
        Resolutions(2)._Y = 3910

        Resolutions(3)._resolution = "10.2"
        Resolutions(3)._X = 3697
        Resolutions(3)._Y = 2780

        Resolutions(4)._resolution = "16.05"
        Resolutions(4)._X = 4620
        Resolutions(4)._Y = 3474

        Resolutions(5)._resolution = "20.3"
        Resolutions(5)._X = 5196
        Resolutions(5)._Y = 3907

        Resolutions(6)._resolution = "24.2"
        Resolutions(6)._X = 6026
        Resolutions(6)._Y = 4017

        Resolutions(7)._resolution = "47.3"
        Resolutions(7)._X = 8368
        Resolutions(7)._Y = 5584

        Resolutions(8)._resolution = "20.1"
        Resolutions(8)._X = 5492
        Resolutions(8)._Y = 3661

        Resolutions(9)._resolution = "25.2"
        Resolutions(9)._X = 5791
        Resolutions(9)._Y = 4354

        Resolutions(9)._resolution = "25.2"
        Resolutions(9)._X = 5791
        Resolutions(9)._Y = 4354


        Resolutions(10)._resolution = "44.3" ' S1R II full sensor resolution
        Resolutions(10)._X = 8151
        Resolutions(10)._Y = 5434

        ResolutionsJPG(0)._resolution = "12.1"
        ResolutionsJPG(0)._X = 3991
        ResolutionsJPG(0)._Y = 2998

        ResolutionsJPG(1)._resolution = "16.0"
        ResolutionsJPG(1)._X = 4592
        ResolutionsJPG(1)._Y = 3448  '3468

        ResolutionsJPG(2)._resolution = "20.0"
        ResolutionsJPG(2)._X = 5184
        ResolutionsJPG(2)._Y = 3888

        ResolutionsJPG(3)._resolution = "10.2"
        ResolutionsJPG(3)._X = 3680
        ResolutionsJPG(3)._Y = 2760


        ResolutionsJPG(4)._resolution = "16.05"
        ResolutionsJPG(4)._X = 4608
        ResolutionsJPG(4)._Y = 3456

        ResolutionsJPG(5)._resolution = "20.3"
        ResolutionsJPG(5)._X = 5196
        ResolutionsJPG(5)._Y = 3907

        ResolutionsJPG(6)._resolution = "24.2"
        ResolutionsJPG(6)._X = 6000
        ResolutionsJPG(6)._Y = 4000

        ResolutionsJPG(7)._resolution = "47.3"
        ResolutionsJPG(7)._X = 8368
        ResolutionsJPG(7)._Y = 5584

        ResolutionsJPG(8)._resolution = "20.1"
        ResolutionsJPG(8)._X = 5472
        ResolutionsJPG(8)._Y = 3648

        ResolutionsJPG(9)._resolution = "25.2"
        ResolutionsJPG(9)._X = 5776
        ResolutionsJPG(9)._Y = 4336


        ResolutionsJPG(10)._resolution = "44.3" ' S1R II max image resolution
        ResolutionsJPG(10)._X = 8144
        ResolutionsJPG(10)._Y = 5424

        ResolutionsThumb(0)._resolution = "12.1"
        ResolutionsThumb(0)._X = 1440
        ResolutionsThumb(0)._Y = 1080

        ResolutionsThumb(1)._resolution = "16.0"
        ResolutionsThumb(1)._X = 1440
        ResolutionsThumb(1)._Y = 1080

        ResolutionsThumb(2)._resolution = "20.0"
        ResolutionsThumb(2)._X = 1440
        ResolutionsThumb(2)._Y = 1080

        ResolutionsThumb(3)._resolution = "10M"
        ResolutionsThumb(3)._X = 1440
        ResolutionsThumb(3)._Y = 1080

        ResolutionsThumb(4)._resolution = "16.05"
        ResolutionsThumb(4)._X = 1440
        ResolutionsThumb(4)._Y = 1080

        ResolutionsThumb(5)._resolution = "20.3"
        ResolutionsThumb(5)._X = 1440
        ResolutionsThumb(5)._Y = 1080

        ResolutionsThumb(6)._resolution = "24.2"
        ResolutionsThumb(6)._X = 1440
        ResolutionsThumb(6)._Y = 1080

        ResolutionsThumb(7)._resolution = "47.3"
        ResolutionsThumb(7)._X = 1440
        ResolutionsThumb(7)._Y = 1080

        ResolutionsThumb(8)._resolution = "20.1"
        ResolutionsThumb(8)._X = 1440
        ResolutionsThumb(8)._Y = 1080


        ResolutionsThumb(9)._resolution = "25.2"
        ResolutionsThumb(9)._X = 1440
        ResolutionsThumb(9)._Y = 1080

        ResolutionsThumb(10)._resolution = "44.3"
        ResolutionsThumb(10)._X = 1440
        ResolutionsThumb(10)._Y = 1080


        'TODO: Implement your additional construction here

        TL.LogMessage("Camera", "Completed initialisation")
    End Sub

    '
    ' PUBLIC COM INTERFACE ICameraV2 IMPLEMENTATION
    '

#Region "Common properties and methods"
    ''' <summary>
    ''' Displays the Setup Dialog form.
    ''' If the user clicks the OK button to dismiss the form, then
    ''' the new settings are saved, otherwise the old values are reloaded.
    ''' THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
    ''' </summary>
    Public Sub SetupDialog() Implements ICameraV2.SetupDialog
        ' consider only showing the setup dialog if not connected
        ' or call a different dialog if connected
        'If IsConnected Then
        '    System.Windows.Forms.MessageBox.Show("Already connected, just press OK")
        'End If

        Using F As SetupDialogForm = New SetupDialogForm(Me, IsConnected)
            Dim result As System.Windows.Forms.DialogResult = F.ShowDialog()
            If result = DialogResult.OK Then
                My.Settings.Save()
                If IsConnected Then
                    ' Already connected: adopt the new settings on the live connection.
                    ' Assigning Connected = True here would re-run the whole connect path
                    ' and start a SECOND polling thread (one more per OK), while looking
                    ' like it worked.
                    ApplyLiveSettings()
                End If
                ' Deliberately do NOT connect when we were disconnected. Configuring is
                ' not connecting: in ASCOM the client opens the connection, and it does so
                ' immediately after this returns. Connecting here left a live session on
                ' the instance the client is about to throw away - and over USB, where the
                ' SDK allows one session per process, the client's own Connected = True
                ' then failed with "OpenSession failed (err 0x00000000)".
                ' The settings the dialog changed are already applied: it sends ISO,
                ' shutter and quality to the camera itself when you press OK.
            Else
                My.Settings.Reload()
            End If
        End Using
    End Sub

    ''' <summary>
    ''' Adopt the settings the dialog just saved, without reconnecting. Only covers what
    ''' can genuinely change mid-session: the dialog disables the IP and resolution
    ''' controls while connected, because those define the connection and the reported
    ''' sensor size.
    ''' </summary>
    Private Sub ApplyLiveSettings()
        TempPath = NormalisePath(My.Settings.TempPath)
        CurrentSpeed = My.Settings.Speed

        Dim romIndex As Integer = ROMAL.IndexOf(My.Settings.TransferFormat)
        If romIndex < 0 Then romIndex = 1 ' RAW
        ReadoutMode = CShort(romIndex)    ' setter pushes the quality to the camera

        Dim isoIndex As Integer = ISOTableAL.IndexOf(My.Settings.ISO)
        If isoIndex >= 0 Then Gain = CShort(isoIndex) ' setter pushes the ISO

        SendLumixMessage(SHUTTERSPEED + ShutterRaw(CurrentSpeed))
        TL.LogMessage("SetupDialog", "applied settings to the live connection (no reconnect)")
    End Sub

    Public ReadOnly Property SupportedActions() As ArrayList Implements ICameraV2.SupportedActions
        Get
            TL.LogMessage("SupportedActions Get", "Returning empty arraylist")
            Return New ArrayList()
        End Get
    End Property

    Public Function Action(ByVal ActionName As String, ByVal ActionParameters As String) As String Implements ICameraV2.Action
        Throw New ActionNotImplementedException("Action " & ActionName & " is not supported by this driver")
    End Function

    Public Sub CommandBlind(ByVal Command As String, Optional ByVal Raw As Boolean = False) Implements ICameraV2.CommandBlind
        CheckConnected("CommandBlind")
        ' Call CommandString and return as soon as it finishes
        Me.CommandString(Command, Raw)
        ' or
        Throw New MethodNotImplementedException("CommandBlind")
    End Sub

    Public Function CommandBool(ByVal Command As String, Optional ByVal Raw As Boolean = False) As Boolean _
        Implements ICameraV2.CommandBool
        CheckConnected("CommandBool")
        Dim ret As String = CommandString(Command, Raw)
        ' TODO decode the return string and return true or false
        ' or
        Throw New MethodNotImplementedException("CommandBool")
    End Function

    Public Function CommandString(ByVal Command As String, Optional ByVal Raw As Boolean = False) As String _
        Implements ICameraV2.CommandString
        CheckConnected("CommandString")
        ' it's a good idea to put all the low level communication with the device here,
        ' then all communication calls this function
        ' you need something to ensure that only one command is in progress at a time

        Throw New MethodNotImplementedException("CommandString")
    End Function

    ''' <summary>
    ''' Local filename for a camera download URL. This was
    ''' Images.Substring(Images.Length - 13) - a fixed 13-character tail that assumed
    ''' one exact name length. "DO02648350.RW2" is 14 characters, so the leading "D"
    ''' was silently dropped on every download; any other naming (a longer stem, a
    ''' different extension, a query string) would slice mid-name instead.
    ''' All four call sites must agree on the name or the convert/delete steps miss
    ''' the file, which is the other reason to have one function for it.
    ''' </summary>
    Private Shared Function LocalNameFor(url As String) As String
        If String.IsNullOrEmpty(url) Then Return url
        Dim name As String = url
        Dim q As Integer = name.IndexOfAny(New Char() {"?"c, "#"c})
        If q >= 0 Then name = name.Substring(0, q)
        Dim slash As Integer = name.LastIndexOfAny(New Char() {"/"c, "\"c})
        If slash >= 0 Then name = name.Substring(slash + 1)
        If name = "" Then Return "capture.dat"
        Return name
    End Function

    ''' <summary>
    ''' Map a shutter speed as the setup dialog displays it ("B", "2s", "125") to the raw
    ''' cam.cgi code in ShutterTable column 0 ("16384/256", "-256/256", "1792/256").
    ''' My.Settings.Speed is data-bound to the combo's Text, so it holds the DISPLAY
    ''' string. The dialog's OK handler sends column 0, but connect re-sent the display
    ''' string, which the camera rejects with err_param (verified on a GH5S: value=B and
    ''' value=2s both err_param; 16384/256 and -256/256 both ok) - so the saved speed was
    ''' silently never applied on connect. Returns the input unchanged if not in the table.
    ''' </summary>
    Private Shared Function ShutterRaw(displaySpeed As String) As String
        If String.IsNullOrEmpty(displaySpeed) Then Return displaySpeed
        For i As Integer = 0 To ShutterTable.GetLength(0) - 1
            If ShutterTable(i, 1) = displaySpeed Then Return ShutterTable(i, 0)
        Next
        Return displaySpeed
    End Function

    ''' <summary>
    ''' The download path builds filenames as TempPath &amp; name, so a folder without a
    ''' trailing separator writes into the PARENT with the folder name glued onto the file
    ''' ("C:\pics" + "DO1234.RW2" -> "C:\picsDO1234.RW2"). The dialog appends a separator;
    ''' a hand-edited profile value does not.
    ''' </summary>
    Private Shared Function NormalisePath(p As String) As String
        If String.IsNullOrEmpty(p) Then Return p
        If p.EndsWith(IO.Path.DirectorySeparatorChar) OrElse p.EndsWith(IO.Path.AltDirectorySeparatorChar) Then Return p
        Return p & IO.Path.DirectorySeparatorChar
    End Function

    Public Property Connected() As Boolean Implements ICameraV2.Connected
        Get
            TL.LogMessage("Connected Get", IsConnected.ToString())
            Return IsConnected
        End Get
        Set(value As Boolean)
            TL.LogMessage("Connected Set", value.ToString())
            Dim d As MyDelegate2 = AddressOf Polling

            If value Then
                TL.LogMessage("Connected Set", "Connecting to IP Address " + IPAddress)
                ' TODO connect to the device
                IPAddress = My.Settings.IPAddress
                TempPath = NormalisePath(My.Settings.TempPath)
                CurrentSpeed = My.Settings.Speed
                ' Resolve and clamp BEFORE assigning: TransferFormat can be unset or stale,
                ' and IndexOf then returns -1. Assigning that to the property first and
                ' checking afterwards pushes -1 through the setter, which indexes ROM()
                ' with it - so an unset TransferFormat made Connected throw instead of
                ' falling back to RAW.
                Dim romIndex As Integer = ROMAL.IndexOf(My.Settings.TransferFormat)
                If romIndex < 0 Then romIndex = 1 ' default to RAW (else sensor size stays 0)
                ReadoutMode = CShort(romIndex)
                Gain = Math.Max(0, ISOTableAL.IndexOf(My.Settings.ISO))
                SendLumixMessage(SHUTTERSPEED + ShutterRaw(CurrentSpeed))
                If MODEL.Contains("S1") Then 'full frame bodies.
                    sensormmx = 36
                    sensormmy = 24
                End If

                Dim index As Integer = Array.FindIndex(Resolutions, Function(f) f._resolution = My.Settings.Resolution)
                If index < 0 Then
                    ' Resolution not set/matched (e.g. connected from SharpCap without
                    ' opening the setup dialog, or an unlisted body such as FZ82). Fall
                    ' back to the model's known resolution, then to the first entry, so
                    ' the arrays below are never indexed with -1 (previously a hard crash).
                    Dim modelRes As String = TryCast(Models(MODEL), String)
                    If Not String.IsNullOrEmpty(modelRes) Then
                        index = Array.FindIndex(Resolutions, Function(f) f._resolution = modelRes)
                    End If
                    If index < 0 Then index = 0
                End If

                Select Case ReadoutMode
                    Case 0 'jpg
                        ccdWidth = ResolutionsJPG(index)._X
                        ccdHeight = ResolutionsJPG(index)._Y

                    Case 1  'raw
                        ccdWidth = Resolutions(index)._X
                        ccdHeight = Resolutions(index)._Y
                    Case 2  'thumb
                        ccdWidth = ResolutionsThumb(index)._X
                        ccdHeight = ResolutionsThumb(index)._Y
                End Select

                pixelSize = Math.Round(1000 * sensormmx / ccdWidth, 2) 'should be 3.75 if 16MP... 

                cameraNumX = ccdWidth
                cameraNumY = ccdHeight
                ' Mark connected only once the setup above succeeded (was set at the
                ' top, so a throw left IsConnected reporting True on a half-open driver).
                connectedState = True
                d.BeginInvoke(True, Nothing, Nothing)



            Else
                connectedState = False
                TL.LogMessage("Connected Set", "Disconnecting from IP Address " + IPAddress)
                ' TODO disconnect from the device
                ' Setting connectedState = False above signals the Polling loop to exit.
            End If
        End Set
    End Property



    Private Delegate Function MyDelegate2(ByVal Bool As Boolean) As Boolean

    Function Polling(Bool As Boolean) As Boolean
        ' Loop on the live connected flag, NOT the by-value 'Bool' argument (which
        ' stayed True forever, so the polling thread never stopped on disconnect and
        ' kept hitting the camera every 10s / leaked a thread per reconnect).
        While connectedState
            System.Threading.Thread.Sleep(10000) ' Sleep for 10 sec
            SendLumixMessage(STATE)
            ' System.Threading.Thread.Sleep(1000) ' Sleep for 1s after the capture so the camera can breath a bit.
        End While
        Return True
    End Function













    Public ReadOnly Property Description As String Implements ICameraV2.Description
        Get
            ' this pattern seems to be needed to allow a public property to return a private field
            Dim d As String = driverDescription
            TL.LogMessage("Description Get", d)
            Return d
        End Get
    End Property

    Public ReadOnly Property DriverInfo As String Implements ICameraV2.DriverInfo
        Get
            Dim m_version As Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
            ' TODO customise this driver description
            Dim s_driverInfo As String = "Lumix Wifi Ascom driver. Version: " + m_version.Major.ToString() + "." + m_version.Minor.ToString()
            TL.LogMessage("DriverInfo Get", s_driverInfo)
            Return s_driverInfo
        End Get
    End Property

    Public ReadOnly Property DriverVersion() As String Implements ICameraV2.DriverVersion
        Get
            ' Get our own assembly and report its version number
            TL.LogMessage("DriverVersion Get", Reflection.Assembly.GetExecutingAssembly.GetName.Version.ToString(2))
            Return Reflection.Assembly.GetExecutingAssembly.GetName.Version.ToString(2)
        End Get
    End Property

    Public ReadOnly Property InterfaceVersion() As Short Implements ICameraV2.InterfaceVersion
        Get
            TL.LogMessage("InterfaceVersion Get", "2")
            Return 2
        End Get
    End Property

    Public ReadOnly Property Name As String Implements ICameraV2.Name
        Get
            Dim s_name As String = "Lumix Ascom Driver"
            TL.LogMessage("Name Get", s_name)
            Return s_name
        End Get
    End Property

    Public Sub Dispose() Implements ICameraV2.Dispose
        ' Clean up the tracelogger and util objects
        TL.Enabled = False
        TL.Dispose()
        TL = Nothing
        utilities.Dispose()
        utilities = Nothing
        astroUtilities.Dispose()
        astroUtilities = Nothing
    End Sub

#End Region

#Region "ICamera Implementation"

    Private ccdWidth As Integer = 4612 ' Constants to define the ccd pixel dimenstions
    Private ccdHeight As Integer = 3468 ' (default for 16MP)

    Private sensormmx As Double = 17.3
    Private sensormmy As Double = 13

    Private pixelSize As Double = 1000 * sensormmx / ccdWidth '3.75  Constant for the pixel physical dimension
    '     Private MFTresolutions = {{"10M", 3697, 2780}, {"12M", 4011, 3016}, {"16M", 4612, 3468}, {"20M", 5200, 3910}} '5200 x 3910 4011 x 3016


    Private Structure MFTResolution
        Dim _resolution As String
        Dim _X As Int32
        Dim _Y As Int32
    End Structure

    Private ReadOnly Resolutions(ResolutionTable.Length) As MFTResolution ' = New MFTResolution(("12M", 4011, 3016), ("16M", 4612, 3468}, {"20M", 5200, 3910}) '5200 x 3910 4011 x 3016
    Private ReadOnly ResolutionsJPG(ResolutionTable.Length) As MFTResolution ' = New MFTResolution(("12M", 4011, 3016), ("16M", 4612, 3468}, {"20M", 5200, 3910}) '5200 x 3910 4011 x 3016
    Private ReadOnly ResolutionsThumb(ResolutionTable.Length) As MFTResolution ' = New MFTResolution(("12M", 4011, 3016), ("16M", 4612, 3468}, {"20M", 5200, 3910}) '5200 x 3910 4011 x 3016



    Private cameraNumX As Integer = ccdWidth ' Initialise variables to hold values required for functionality tested by Conform
    Private cameraNumY As Integer = ccdHeight 'note that somehow the JPG file is not exactly this size but smaller. However we ignore that for now
    Private cameraStartX As Integer = 0
    Private cameraStartY As Integer = 0
    Private exposureStart As DateTime = DateTime.MinValue
    Private cameraLastExposureDuration As Double = 0.0
    Private cameraImageReady As Boolean = False
    Private cameraAborted As Boolean = False ' set by AbortExposure so the async capture chain stops
    'Private cameraImageArray As Integer(,,)
    'Private cameraImageArrayVariant As Object(,,)
    Private cameraImageArray As Integer(,)
    Private cameraImageArrayVariant As Object(,)

    Public Sub AbortExposure() Implements ICameraV2.AbortExposure
        cameraAborted = True ' the in-flight WaitBulb -> ReadImageFromCamera chain checks this and bails
        StopExposure()
        TL.LogMessage("AbortExposure", "Exposure Aborted")
        CurrentState = CameraStates.cameraIdle
        cameraImageReady = False
        TL.LogMessage("Camera ImageReady", "False")


    End Sub

    Public ReadOnly Property BayerOffsetX() As Short Implements ICameraV2.BayerOffsetX
        Get
            TL.LogMessage("BayerOffsetX Get", "0")
            Return 0
            'Throw New PropertyNotImplementedException("BayerOffsetX", False)
        End Get
    End Property

    Public ReadOnly Property BayerOffsetY() As Short Implements ICameraV2.BayerOffsetY
        Get
            TL.LogMessage("BayerOffsetY Get", "0")
            Return 0
            'Throw New ASCOM.PropertyNotImplementedException("BayerOffsetY", False)
        End Get
    End Property

    Public Property BinX() As Short Implements ICameraV2.BinX
        Get
            TL.LogMessage("BinX Get", "1")
            Return 1
        End Get
        Set(value As Short)
            TL.LogMessage("BinX Set", value.ToString())
            If (Not (value = 1)) Then
                TL.LogMessage("BinX Set", "Value out of range, throwing InvalidValueException")
                Throw New ASCOM.InvalidValueException("BinX", value.ToString(), "1") ' Only 1 is valid in this simple template
            End If
        End Set
    End Property

    Public Property BinY() As Short Implements ICameraV2.BinY
        Get
            TL.LogMessage("BinY Get", "1")
            Return 1
        End Get
        Set(value As Short)
            TL.LogMessage("BinY Set", value.ToString())
            If (Not (value = 1)) Then
                TL.LogMessage("BinX Set", "Value out of range, throwing InvalidValueException")
                Throw New ASCOM.InvalidValueException("BinY", value.ToString(), "1") ' Only 1 is valid in this simple template
            End If
        End Set
    End Property

    Public ReadOnly Property CCDTemperature() As Double Implements ICameraV2.CCDTemperature
        Get
            'TL.LogMessage("CCDTemperature Get", "Not implemented")
            'Throw New ASCOM.PropertyNotImplementedException("CCDTemperature", False)
            Return 25
        End Get
    End Property

    Public ReadOnly Property CameraState() As CameraStates Implements ICameraV2.CameraState
        Get
            TL.LogMessage("CameraState Get", CurrentState.ToString())
            Return CurrentState
        End Get
    End Property

    Public ReadOnly Property CameraXSize() As Integer Implements ICameraV2.CameraXSize
        Get
            TL.LogMessage("CameraXSize Get", ccdWidth.ToString())
            Return ccdWidth
        End Get
    End Property

    Public ReadOnly Property CameraYSize() As Integer Implements ICameraV2.CameraYSize
        Get
            TL.LogMessage("CameraYSize Get", ccdHeight.ToString())
            Return ccdHeight
        End Get
    End Property

    Public ReadOnly Property CanAbortExposure() As Boolean Implements ICameraV2.CanAbortExposure
        Get
            TL.LogMessage("CanAbortExposure Get", True.ToString())
            Return True
        End Get
    End Property

    Public ReadOnly Property CanAsymmetricBin() As Boolean Implements ICameraV2.CanAsymmetricBin
        Get
            TL.LogMessage("CanAsymmetricBin Get", False.ToString())
            Return False
        End Get
    End Property

    Public ReadOnly Property CanFastReadout() As Boolean Implements ICameraV2.CanFastReadout
        Get
            TL.LogMessage("CanFastReadout Get", False.ToString())
            Return False
        End Get
    End Property

    Public ReadOnly Property CanGetCoolerPower() As Boolean Implements ICameraV2.CanGetCoolerPower
        Get
            TL.LogMessage("CanGetCoolerPower Get", False.ToString())
            Return False
        End Get
    End Property

    Public ReadOnly Property CanPulseGuide() As Boolean Implements ICameraV2.CanPulseGuide
        Get
            TL.LogMessage("CanPulseGuide Get", False.ToString())
            Return False
        End Get
    End Property

    Public ReadOnly Property CanSetCCDTemperature() As Boolean Implements ICameraV2.CanSetCCDTemperature
        Get
            TL.LogMessage("CanSetCCDTemperature Get", False.ToString())
            Return False
        End Get
    End Property

    Public ReadOnly Property CanStopExposure() As Boolean Implements ICameraV2.CanStopExposure
        Get
            TL.LogMessage("CanStopExposure Get", True.ToString())
            Return True
        End Get
    End Property

    Public Property CoolerOn() As Boolean Implements ICameraV2.CoolerOn
        Get
            TL.LogMessage("CoolerOn Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("CoolerOn", False)
        End Get
        Set(value As Boolean)
            TL.LogMessage("CoolerOn Set", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("CoolerOn", True)
        End Set
    End Property

    Public ReadOnly Property CoolerPower() As Double Implements ICameraV2.CoolerPower
        Get
            TL.LogMessage("AbortExposure Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("CoolerPower", False)
        End Get
    End Property

    Public ReadOnly Property ElectronsPerADU() As Double Implements ICameraV2.ElectronsPerADU
        Get
            TL.LogMessage("ElectronsPerADU Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ElectronsPerADU", False)
        End Get
    End Property

    Public ReadOnly Property ExposureMax() As Double Implements ICameraV2.ExposureMax
        Get
            If MODEL = "G80" Then
                TL.LogMessage("ExposureMax Get", "120 secs  this is true for G80 only") 'this is true for G80 only
                Return (120)
            Else
                TL.LogMessage("ExposureMax Get", "1800 secs - 30 min in Bulb") '
                Return (1800)

            End If

            'Throw New ASCOM.PropertyNotImplementedException("ExposureMax", False)
        End Get
    End Property

    Public ReadOnly Property ExposureMin() As Double Implements ICameraV2.ExposureMin
        Get
            TL.LogMessage("ExposureMin Get", "1/4000")
            Return (1 / 4000)
            'Throw New ASCOM.PropertyNotImplementedException("ExposureMin", False)
        End Get
    End Property

    Public ReadOnly Property ExposureResolution() As Double Implements ICameraV2.ExposureResolution
        Get
            TL.LogMessage("ExposureResolution Get", "1/1000")
            Return (1 / 1000)
            'Throw New ASCOM.PropertyNotImplementedException("ExposureResolution", False)
        End Get
    End Property

    Public Property FastReadout() As Boolean Implements ICameraV2.FastReadout
        Get
            TL.LogMessage("FastReadout Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("FastReadout", False)
        End Get
        Set(value As Boolean)
            TL.LogMessage("FastReadout Set", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("FastReadout", True)
        End Set
    End Property

    Public ReadOnly Property FullWellCapacity() As Double Implements ICameraV2.FullWellCapacity
        Get
            TL.LogMessage("FullWellCapacity Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("FullWellCapacity", False)
        End Get
    End Property

    Public Property Gain() As Short Implements ICameraV2.Gain
        Get
            TL.LogMessage("Gain Get", "reading the current ISO value" + CurrentISO.ToString)
            Return CurrentISO
            'Throw New ASCOM.PropertyNotImplementedException("Gain", False)
        End Get
        Set(value As Short)
            ' Gain is an index into Gains (= ISOTableAL); send the ISO value AT that
            ' index, not the index number itself (was 'ISO + value' -> e.g. value=17,
            ' which the camera rejects). Connect sets Gain = IndexOf(saved ISO), so
            ' this also stops connect from clobbering the ISO the setup dialog sent.
            If value >= 0 AndAlso value < ISOTableAL.Count Then
                SendLumixMessage(ISO + ISOTableAL(value).ToString())
                CurrentISO = value
                TL.LogMessage("Gain Set", "Setting ISO to " & ISOTableAL(value).ToString())
            End If
            'Throw New ASCOM.PropertyNotImplementedException("Gain", True)
        End Set
    End Property

    Public ReadOnly Property GainMax() As Short Implements ICameraV2.GainMax
        Get
            'TL.LogMessage("GainMax Get", 25000)
            Throw New ASCOM.PropertyNotImplementedException("GainMax", False)
            'Return 25000

        End Get
    End Property

    Public ReadOnly Property GainMin() As Short Implements ICameraV2.GainMin
        Get
            'TL.LogMessage("GainMin Get", "80")
            'Return 80
            Throw New ASCOM.PropertyNotImplementedException("GainMin", False)
        End Get
    End Property

    Public ReadOnly Property Gains() As ArrayList Implements ICameraV2.Gains
        Get
            TL.LogMessage("Gains Get", "returning the list of ISO values")
            Return ISOTableAL
            'Throw New ASCOM.PropertyNotImplementedException("Gains", False)
        End Get
    End Property

    Public ReadOnly Property HasShutter() As Boolean Implements ICameraV2.HasShutter
        Get
            TL.LogMessage("HasShutter Get", True.ToString())
            Return True
        End Get
    End Property

    Public ReadOnly Property HeatSinkTemperature() As Double Implements ICameraV2.HeatSinkTemperature
        Get
            TL.LogMessage("HeatSinkTemperature Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("HeatSinkTemperature", False)
        End Get
    End Property
    ''' <summary>
    ''' this was the code for multiplane imagearray
    ''' </summary>
    ''' <returns></returns>
    '
    'Public ReadOnly Property ImageArray() As Object Implements ICameraV2.ImageArray
    '    Get
    '        If (Not cameraImageReady) Then
    '            TL.LogMessage("ImageArray Get", "Throwing InvalidOperationException because of a call to ImageArray before the first image has been taken!")
    '            Throw New ASCOM.InvalidOperationException("Call to ImageArray before the first image has been taken!")
    '        End If
    '        Dim Tiffimagefile As IO.FileStream
    '        Tiffimagefile = New FileStream(TiffFileName, IO.FileMode.Open)
    '        ReDim cameraImageArray(cameraNumX - 1, cameraNumY - 1, 2) ' there are 3 channels: RVB. 

    '        Dim decoder As New TiffBitmapDecoder(Tiffimagefile, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default)
    '        Dim stride As Int32
    '        Dim index As Int32
    '        Dim bitmapSource As BitmapSource = decoder.Frames(0)
    '        Dim bytesPerPixel As UShort
    '        bytesPerPixel = bitmapSource.Format.BitsPerPixel / 8 '3 for JPG and 6 for RAW
    '        stride = bitmapSource.PixelWidth * bytesPerPixel

    '        If CurrentROM = 1 Then
    '            Dim pixels(bitmapSource.PixelHeight * stride) As UShort
    '            bitmapSource.CopyPixels(pixels, stride, 0)
    '            For y = 0 To (cameraNumY - 1)
    '                For x = 0 To (cameraNumX - 1)
    '                    index = x * 3 + (y * stride / 2) 'because of the 16 bit instead of the 8 bit per channel this /2 is needed.
    '                    cameraImageArray(x, cameraNumY - y - 1, 0) = pixels(index)
    '                    cameraImageArray(x, cameraNumY - y - 1, 1) = pixels(index + 1)
    '                    cameraImageArray(x, cameraNumY - y - 1, 2) = pixels(index + 2)

    '                Next x
    '            Next y
    '        Else
    '            Dim pixels(bitmapSource.PixelHeight * stride) As Byte
    '            bitmapSource.CopyPixels(pixels, stride, 0)
    '            For y = 0 To (cameraNumY - 1)
    '                For x = 0 To (cameraNumX - 1)
    '                    index = x * 3 + (y * stride)
    '                    cameraImageArray(x, cameraNumY - y - 1, 0) = pixels(index + 2) 'R and B are reversed
    '                    cameraImageArray(x, cameraNumY - y - 1, 1) = pixels(index + 1)
    '                    cameraImageArray(x, cameraNumY - y - 1, 2) = pixels(index)

    '                Next x
    '            Next y

    '        End If
    '        Tiffimagefile.Dispose() 'cleaning up aftermyself and removing the Tiff file once it is used
    '        My.Computer.FileSystem.DeleteFile(TiffFileName)

    '        TL.LogMessage("ImageArray Get", "getting the Array")

    '        Return cameraImageArray
    '    End Get
    'End Property

    'Public ReadOnly Property ImageArrayVariant() As Object Implements ICameraV2.ImageArrayVariant
    '    Get
    '        If (Not cameraImageReady) Then
    '            TL.LogMessage("ImageArrayVariant Get", "Throwing InvalidOperationException because of a call to ImageArrayVariant before the first image has been taken!")
    '            Throw New ASCOM.InvalidOperationException("Call to ImageArrayVariant before the first image has been taken!")
    '        End If

    '        ReDim cameraImageArrayVariant(cameraNumX - 1, cameraNumY - 1, 2)
    '        For i As Integer = 0 To cameraImageArray.GetLength(1) - 1
    '            For j As Integer = 0 To cameraImageArray.GetLength(0) - 1
    '                cameraImageArrayVariant(j, i, 0) = cameraImageArray(j, i, 0)
    '                cameraImageArrayVariant(j, i, 1) = cameraImageArray(j, i, 1)
    '                cameraImageArrayVariant(j, i, 2) = cameraImageArray(j, i, 2)
    '            Next
    '        Next
    '        TL.LogMessage("ImageArray Variant Get", "getting the Array Variant")
    '        Return cameraImageArrayVariant
    '    End Get
    'End Property


    Public ReadOnly Property ImageArray() As Object Implements ICameraV2.ImageArray
        Get
            If (Not cameraImageReady) Then
                TL.LogMessage("ImageArray Get", "Throwing InvalidOperationException because of a call to ImageArray before the first image has been taken!")
                Throw New ASCOM.InvalidOperationException("Call to ImageArray before the first image has been taken!")
            End If
            ' Already built for this exposure: hand back the same array. The TIFF was
            ' deleted after the first read, so re-decoding it is impossible - and ASCOM
            ' allows ImageArray to be read more than once per exposure.
            If cameraImageArray IsNot Nothing Then
                TL.LogMessage("ImageArray Get", "returning the array already built for this exposure")
                Return cameraImageArray
            End If
            CurrentState = CameraStates.cameraDownload
            Dim Tiffimagefile As IO.FileStream
            Tiffimagefile = New FileStream(TiffFileName, IO.FileMode.Open)
            ReDim cameraImageArray(cameraNumX - 1, cameraNumY - 1) ' there are 3 channels: RVB. 

            Dim decoder As New TiffBitmapDecoder(Tiffimagefile, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default)
            Dim stride As Int32
            Dim index As Int32
            Dim bitmapSource As BitmapSource = decoder.Frames(0)
            Dim bytesPerPixel As UShort
            bytesPerPixel = bitmapSource.Format.BitsPerPixel / 8
            stride = bitmapSource.PixelWidth * bytesPerPixel
            ' Clamp the copy to the smaller of the table size and the decoded image
            ' (the JPG/thumb is often smaller), so we never index past the pixel
            ' buffer or the output array.
            Dim imgW As Integer = Math.Min(cameraNumX, bitmapSource.PixelWidth)
            Dim imgH As Integer = Math.Min(cameraNumY, bitmapSource.PixelHeight)

            If CurrentROM = 1 Then  'RAW
                Dim pixels(bitmapSource.PixelHeight * stride * 2) As Byte
                bitmapSource.CopyPixels(pixels, stride, 0)
                For y = 0 To (imgH - 2)
                    For x = 0 To (imgW - 2)
                        index = x * 3 + (y * stride)
                        cameraImageArray(x, y) = pixels(index + 2) * 256 'R and B are reversed
                        cameraImageArray(x + 1, y + 1) = pixels(index) * 256 'R and B are reversed
                        cameraImageArray(x + 1, y) = pixels(index + 1) * 256
                        cameraImageArray(x, y + 1) = pixels(index + 1) * 256
                        x += 1
                    Next x
                    y += 1
                Next y
            Else
                Dim pixels(bitmapSource.PixelHeight * stride) As Byte
                bitmapSource.CopyPixels(pixels, stride, 0)
                For y = 0 To (imgH - 2)
                    For x = 0 To (imgW - 2)
                        index = x * 3 + (y * stride)
                        cameraImageArray(x, y) = pixels(index + 2) * 256 'R 
                        cameraImageArray(x + 1, y + 1) = pixels(index) * 256 'B 
                        cameraImageArray(x + 1, y) = pixels(index + 1) * 256 'G
                        cameraImageArray(x, y + 1) = pixels(index + 1) * 256 'G
                        x += 1

                    Next x
                    y += 1
                Next y

            End If

            Try
                Tiffimagefile.Dispose() 'cleaning up aftermyself and removing the Tiff file once it is used
                My.Computer.FileSystem.DeleteFile(TiffFileName)
            Catch e As Exception
                TL.LogMessage("ImageArray Get", "error in deleting the imagefile")
            End Try

            TL.LogMessage("ImageArray Get", "getting the Array")
            ' Do NOT clear cameraImageReady here. Reading the image must not consume it:
            ' ASCOM keeps ImageReady true until the next StartExposure, and a client may
            ' read ImageArray and ImageArrayVariant for the same exposure. Clearing it
            ' made the very next ImageArrayVariant throw "before the first image has been
            ' taken" (ConformU flagged exactly that). StartExposure resets the flag.
            CurrentState = CameraStates.cameraIdle

            Return cameraImageArray
        End Get
    End Property

    Public ReadOnly Property ImageArrayVariant() As Object Implements ICameraV2.ImageArrayVariant
        Get
            If (Not cameraImageReady) Then
                TL.LogMessage("ImageArrayVariant Get", "Throwing InvalidOperationException because of a call to ImageArrayVariant before the first image has been taken!")
                Throw New ASCOM.InvalidOperationException("Call to ImageArrayVariant before the first image has been taken!")
            End If
            CurrentState = CameraStates.cameraDownload
            ' A client may read the variant without reading ImageArray first; build it
            ' then, rather than dereferencing a Nothing array (which surfaced as a raw
            ' NullReferenceException instead of an ASCOM exception).
            If cameraImageArray Is Nothing Then
                Dim ignored As Object = Me.ImageArray
            End If
            ReDim cameraImageArrayVariant(cameraNumX - 1, cameraNumY - 1)
            For i As Integer = 0 To cameraNumY - 1
                For j As Integer = 0 To cameraNumX - 1
                    cameraImageArrayVariant(j, i) = cameraImageArray(j, i)
                Next
            Next
            TL.LogMessage("ImageArray Variant Get", "getting the Array Variant")
            CurrentState = CameraStates.cameraIdle
            Return cameraImageArrayVariant
        End Get
    End Property


    Public ReadOnly Property ImageReady() As Boolean Implements ICameraV2.ImageReady
        Get
            TL.LogMessage("ImageReady Get", cameraImageReady.ToString())
            Return cameraImageReady
        End Get
    End Property

    Public ReadOnly Property IsPulseGuiding() As Boolean Implements ICameraV2.IsPulseGuiding
        Get
            TL.LogMessage("IsPulseGuiding Get", "False")
            Return False
            'Throw New ASCOM.PropertyNotImplementedException("IsPulseGuiding", False)
        End Get
    End Property

    Public ReadOnly Property LastExposureDuration() As Double Implements ICameraV2.LastExposureDuration
        Get
            If (Not cameraImageReady) Then
                TL.LogMessage("LastExposureDuration Get", "Throwing InvalidOperationException because of a call to LastExposureDuration before the first image has been taken!")
                Throw New ASCOM.InvalidOperationException("Call to LastExposureDuration before the first image has been taken!")
            End If
            TL.LogMessage("LastExposureDuration Get", cameraLastExposureDuration.ToString())
            Return cameraLastExposureDuration
        End Get
    End Property

    Public ReadOnly Property LastExposureStartTime() As String Implements ICameraV2.LastExposureStartTime
        Get
            If (Not cameraImageReady) Then
                TL.LogMessage("LastExposureStartTime Get", "Throwing InvalidOperationException because of a call to LastExposureStartTime before the first image has been taken!")
                Throw New ASCOM.InvalidOperationException("Call to LastExposureStartTime before the first image has been taken!")
            End If
            Dim exposureStartString As String = exposureStart.ToString("yyyy-MM-ddTHH:mm:ss")
            TL.LogMessage("LastExposureStartTime Get", exposureStartString.ToString())
            Return exposureStartString
        End Get
    End Property

    Public ReadOnly Property MaxADU() As Integer Implements ICameraV2.MaxADU
        Get
            ' ImageArray delivers byte values scaled x256 (16-bit range), so report a
            ' 16-bit ceiling rather than 4096.
            TL.LogMessage("MaxADU Get", "65535")
            Return 65535
        End Get
    End Property

    Public ReadOnly Property MaxBinX() As Short Implements ICameraV2.MaxBinX
        Get
            TL.LogMessage("MaxBinX Get", "1")
            Return 1
        End Get
    End Property

    Public ReadOnly Property MaxBinY() As Short Implements ICameraV2.MaxBinY
        Get
            TL.LogMessage("MaxBinY Get", "1")
            Return 1
        End Get
    End Property

    Public Property NumX() As Integer Implements ICameraV2.NumX
        Get
            TL.LogMessage("NumX Get", cameraNumX.ToString())
            Return cameraNumX
        End Get
        Set(value As Integer)
            cameraNumX = value
            TL.LogMessage("NumX set", value.ToString())
        End Set
    End Property

    Public Property NumY() As Integer Implements ICameraV2.NumY
        Get
            TL.LogMessage("NumY Get", cameraNumY.ToString())
            Return cameraNumY
        End Get
        Set(value As Integer)
            cameraNumY = value
            TL.LogMessage("NumY set", value.ToString())
        End Set
    End Property

    Public ReadOnly Property PercentCompleted() As Short Implements ICameraV2.PercentCompleted
        Get
            TL.LogMessage("PercentCompleted Get", CurrentPercentCompleted.ToString())
            Return CurrentPercentCompleted
            'Throw New ASCOM.PropertyNotImplementedException("PercentCompleted", False)
        End Get
    End Property

    Public ReadOnly Property PixelSizeX() As Double Implements ICameraV2.PixelSizeX
        Get
            TL.LogMessage("PixelSizeX Get", pixelSize.ToString())
            Return pixelSize
        End Get
    End Property

    Public ReadOnly Property PixelSizeY() As Double Implements ICameraV2.PixelSizeY
        Get
            TL.LogMessage("PixelSizeY Get", pixelSize.ToString())
            Return pixelSize
        End Get
    End Property

    Public Sub PulseGuide(Direction As GuideDirections, Duration As Integer) Implements ICameraV2.PulseGuide
        TL.LogMessage("PulseGuide", "Not implemented - " & Direction.ToString)
        Throw New ASCOM.MethodNotImplementedException("Direction")
    End Sub

    Public Property ReadoutMode() As Short Implements ICameraV2.ReadoutMode
        Get
            If CurrentROM >= ROM.Length Then Return 0 ' never index ROM() out of range
            TL.LogMessage("ReadoutMode Get", ROM(CurrentROM))
            Return CurrentROM
            'Throw New ASCOM.PropertyNotImplementedException("ReadoutMode", False)
        End Get
        Set(value As Short)
            ' ReadoutMode is an index into ReadoutModes. Validate before using it to
            ' index ROM(): an out-of-range value used to raise a raw
            ' IndexOutOfRangeException out of the logging line, where ASCOM requires
            ' InvalidValueException (and any conformance checker sets one deliberately).
            If value < 0 OrElse value >= ROM.Length Then
                TL.LogMessage("ReadoutMode Set", "rejected out-of-range value " & value.ToString())
                Throw New ASCOM.InvalidValueException("ReadoutMode", value.ToString(), "0.." & (ROM.Length - 1).ToString())
            End If
            TL.LogMessage("ReadoutMode Set", ROM(value).ToString)
            SendLumixMessage(QUALITY + "raw_fine")
            'Select Case value
            '    Case 0, 2
            '        SendLumixMessage(QUALITY + "raw_fine")
            '    Case 1
            '        SendLumixMessage(QUALITY + "raw")
            'End Select
            CurrentROM = value
            'Throw New ASCOM.PropertyNotImplementedException("ReadoutMode", True)
        End Set
    End Property

    Public ReadOnly Property ReadoutModes() As ArrayList Implements ICameraV2.ReadoutModes
        Get
            TL.LogMessage("ReadoutModes Get", "JPG, RAW or Thumb")
            Return ROMAL
            'Throw New ASCOM.PropertyNotImplementedException("ReadoutModes", False)
        End Get
    End Property

    Public ReadOnly Property SensorName() As String Implements ICameraV2.SensorName
        Get
            TL.LogMessage("SensorName Get", "Panasonic Lumix" + MODEL)
            Return "Panasonic Lumix" + MODEL
            'Throw New ASCOM.PropertyNotImplementedException("SensorName", False)
        End Get
    End Property

    Public ReadOnly Property SensorType() As SensorType Implements ICameraV2.SensorType
        Get
            TL.LogMessage("SensorType Get", "RGGB")
            'Throw New ASCOM.PropertyNotImplementedException("SensorType", False)
            'Return SensorType.Color
            Return SensorType.RGGB

        End Get
    End Property

    Public Property SetCCDTemperature() As Double Implements ICameraV2.SetCCDTemperature
        Get
            TL.LogMessage("SetCCDTemperature Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("SetCCDTemperature", False)
        End Get
        Set(value As Double)
            TL.LogMessage("SetCCDTemperature Set", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("SetCCDTemperature", True)
        End Set
    End Property

    ' The camera reports err_busy on get_content_info until it has finished writing the
    ' frame; ~20s is comfortably longer than a RAW commit observed on a GH5S over WiFi.
    Private Const CONTENT_READY_WAIT_MS As Integer = 500
    Private Const CONTENT_READY_RETRIES As Integer = 40

    Private Function NumberPix() As String
        Dim response As String = SendLumixMessage(NUMPIX)
        ' err_busy / err_param etc: not ready (or refused) - report "not available".
        If response IsNot Nothing AndAlso response.Contains("err") Then Return ""
        ' SendLumixMessage returns "" (or Nothing) whenever the request fails, and
        ' XElement.Parse("") throws "XmlException: Root element is missing". That escaped
        ' as the reported cause of every failed download, hiding the real one: the
        ' camera simply had not answered get_content_info yet.
        If String.IsNullOrWhiteSpace(response) Then Return ""
        Dim doc As XElement = XElement.Parse(response)
        ' Return the count text if present. Was 'If <element>.Value Then' which does
        ' a CBool on the XML text and throws InvalidCastException on a numeric/empty value.
        Dim total As String = doc...<total_content_number>.Value
        If Not String.IsNullOrEmpty(total) Then
            Return total
        End If
        Return ""
    End Function


    'function that asks the camera the list in XML of the last num of images it has in store
    'typically num is 1 but was useful to have it as a variable when building the dialogue
    'got some issues with dealing the various XML formats so in the end the response from the camerais turned into a string but inside it is an XML...

    Private Function GetPix(num As Int16) As String
        SendLumixMessage(PLAYMODE)
        Dim Start As Int16 = 0
        ' Right after a capture the camera answers get_content_info with
        ' <result>err_busy</result> while it is still committing the file - longest for
        ' RAW. NumberPix() then returns "", and 'NumPix - num' threw
        ' InvalidCastException ("" -> Double). Wait for a real count instead; give up
        ' with "" so the caller raises a proper DriverException.
        Dim NumPix As String = NumberPix()
        Dim waits As Integer = 0
        Do While String.IsNullOrEmpty(NumPix) AndAlso waits < CONTENT_READY_RETRIES
            Thread.Sleep(CONTENT_READY_WAIT_MS)
            waits += 1
            NumPix = NumberPix()
        Loop
        If String.IsNullOrEmpty(NumPix) Then Return ""
        Dim total As Integer
        If Not Integer.TryParse(NumPix, total) Then Return ""
        Dim SoapMsg As String = SoapEnvelop(Math.Max(total - num, 0), num)
        Dim Stream As System.IO.StreamWriter
        Dim HTTPReq As HttpWebRequest

        HTTPReq = WebRequest.Create("http://" + IPAddress + CDS_Control)
        HTTPReq.ContentType = "text/xml; charset=""utf-8"""
        HTTPReq.Method = "POST"
        HTTPReq.Accept = "text/xml"
        HTTPReq.Headers.Add("soapaction", "urn:schemas-upnp-org:service:ContentDirectory:1#Browse")

        Stream = New StreamWriter(HTTPReq.GetRequestStream(), Encoding.UTF8)
        Stream.Write(SoapMsg)
        Stream.Flush()
        Stream.Close()

        Dim myStreamReader As StreamReader
        Dim statusCode As HttpStatusCode
        Dim ResponseText As String

        Try
            Dim myWebResponse = CType(HTTPReq.GetResponse(), HttpWebResponse)
            myStreamReader = New StreamReader(myWebResponse.GetResponseStream())
            If myWebResponse.StatusCode = HttpStatusCode.Accepted Or myWebResponse.StatusCode = 200 Then
                ResponseText = myStreamReader.ReadToEnd
                Dim answer As String = ResponseText.Replace("&amp;", "&").Replace("&apos;", "'").Replace("&quot;", """").Replace("&lt;", "<").Replace("&gt;", ">")
                Return answer

            Else
                Return ""
            End If
        Catch e As WebException
            If (e.Status = WebExceptionStatus.ProtocolError) Then
                Dim response As WebResponse = e.Response
                Using (response)
                    Dim httpResponse As HttpWebResponse = CType(response, HttpWebResponse)
                    statusCode = httpResponse.StatusCode
                    Try
                        myStreamReader = New StreamReader(response.GetResponseStream())
                        Using (myStreamReader)
                            ResponseText = myStreamReader.ReadToEnd & "Status Description = " & httpResponse.StatusDescription ' HttpWebResponse.StatusDescription
                            Return ""
                        End Using
                    Catch ex As Exception
                        'TL.LogMessage("Message" + LumixMessage + " Sent Failed", LumixMessage + " failed")
                    End Try
                End Using
            End If
            Return ""
        End Try
    End Function



    'formats a message to be sent to the maera
    Public Function SendLumixMessage(LumixMessage As String) As String
        ' No IP configured yet (e.g. a host connected without opening setup):
        ' don't build an invalid "http:///..." URI, which throws an *uncaught*
        ' UriFormatException. Just no-op with an empty response.
        If String.IsNullOrEmpty(IPAddress) Then Return ""
        Dim request = WebRequest.Create("http://" + IPAddress + "/" + LumixMessage)
        Dim myStreamReader As StreamReader
        Dim SendStatus As Integer = -1
        Dim statusCode As HttpStatusCode
        Dim ResponseText As String = ""
        Try
            Dim myWebResponse = CType(request.GetResponse(), HttpWebResponse)
            myStreamReader = New StreamReader(myWebResponse.GetResponseStream())
            ResponseText = myStreamReader.ReadToEnd
            If myWebResponse.StatusCode = HttpStatusCode.Accepted Or myWebResponse.StatusCode = 200 Then
                SendStatus = 1 'message sent successfully
                Return ResponseText
            Else
                SendStatus = 2 'message processed but not sent successfully
            End If
        Catch e As WebException
            If (e.Status = WebExceptionStatus.ProtocolError) Then
                Dim response As WebResponse = e.Response
                Using (response)
                    Dim httpResponse As HttpWebResponse = CType(response, HttpWebResponse)
                    statusCode = httpResponse.StatusCode
                    Try
                        myStreamReader = New StreamReader(response.GetResponseStream())
                        Using (myStreamReader)
                            ResponseText = myStreamReader.ReadToEnd & "Status Description = " & httpResponse.StatusDescription ' HttpWebResponse.StatusDescription
                        End Using
                    Catch ex As Exception
                        'TL.LogMessage("Message" + LumixMessage + " Sent Failed", LumixMessage + " failed")
                    End Try
                End Using
            End If
        End Try
        ' Always return a string (never Nothing) so callers doing .Contains(...) /
        ' XElement.Parse(...) don't hit a NullReferenceException. ResponseText is
        ' only assigned on some paths, hence the coalesce.
        Return If(ResponseText, "")
    End Function




    'this is the meety method.
    'takes a picture of Duration
    'and gets the image back via http from the camera
    'depending on the transfer format the img is fetched either in RAW or in JPG


    Public Sub StartExposure(Duration As Double, Light As Boolean) Implements ICameraV2.StartExposure
        If (Duration < 0.0) Then Throw New InvalidValueException("StartExposure", Duration.ToString(), "0.0 upwards")
        If (cameraStartX + cameraNumX > ccdWidth) Then Throw New InvalidValueException("StartExposure", cameraNumX.ToString(), ccdWidth.ToString())
        If (cameraStartY + cameraNumY > ccdHeight) Then Throw New InvalidValueException("StartExposure", cameraNumY.ToString(), ccdHeight.ToString())
        If (cameraStartX > ccdWidth) Then Throw New InvalidValueException("StartExposure", cameraStartX.ToString(), ccdWidth.ToString())
        If (cameraStartY > ccdHeight) Then Throw New InvalidValueException("StartExposure", cameraStartY.ToString(), ccdHeight.ToString())

        cameraImageReady = False
        cameraAborted = False
        ' Drop the previous exposure's decoded array so ImageArray rebuilds from the new
        ' TIFF rather than returning the cached one.
        cameraImageArray = Nothing
        cameraImageArrayVariant = Nothing
        cameraLastExposureDuration = Duration
        ' The camera-start HTTP round-trips now happen in WaitBulb (the async part) so
        ' StartExposure returns promptly, as ASCOM expects.
        TL.LogMessage("StartExposure", Duration.ToString() + " " + Light.ToString())
        CurrentState = CameraStates.cameraExposing
        Dim d As MyDelegate = AddressOf WaitBulb
        d.BeginInvoke(Duration, New AsyncCallback(AddressOf ReadImageFromCamera), Nothing)
    End Sub

    Sub MyCallback(ByVal result As IAsyncResult)
        Dim resultClass = CType(result, AsyncResult)
        Dim d As MyDelegate = CType(resultClass.AsyncDelegate, MyDelegate)
        TL.LogMessage("Callback from the Bulbcapture also know that the result is: ", d.EndInvoke(result).ToString)
        CurrentState = CameraStates.cameraIdle

    End Sub

    Private Delegate Function MyDelegate(ByVal Duration As Double) As Boolean

    Function WaitBulb(ByVal Duration As Double) As Boolean
        ' Begin the exposure here (moved out of StartExposure so it returns promptly).
        exposureStart = DateTime.Now
        SendLumixMessage(RECMODE) 'makes sure it is not in playmode...
        SendLumixMessage(SHUTTERSTART)
        TL.LogMessage("waiting while capturing", Duration.ToString)
        System.Threading.Thread.Sleep(Duration * 1000) ' Sleep for the duration to simulate exposure, if this is in Bulb mode
        StopExposure()
        ' The shutter is closed: the exposure is over. Say so now, rather than staying in
        ' cameraExposing through the PLAYMODE round-trip and the DLNA browse that follow -
        ' those took ~8s on a GH5S over WiFi, during which a client (and ConformU, which
        ' abandons the test) still saw "Exposing" and could not tell the exposure had ended.
        CurrentState = CameraStates.cameraReading
        ' System.Threading.Thread.Sleep(1000) ' Sleep for 1s after the capture so the camera can breath a bit.
        Return True
    End Function

    <DllImport("libraw.dll", EntryPoint:="libraw_init", ThrowOnUnmappableChar:=False, CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_init64(ByVal flag As Integer) As <MarshalAs(UnmanagedType.SysUInt)> IntPtr

    End Function

    <DllImport("libraw.dll", EntryPoint:="libraw_open_file", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_open_file64(ByVal libraw_data As IntPtr, ByVal filename As String) As <MarshalAs(UnmanagedType.U4)> Int32

    End Function

    <DllImport("libraw.dll", EntryPoint:="libraw_dcraw_ppm_tiff_writer", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_dcraw_ppm_tiff_writer64(ByVal libraw_data As IntPtr, ByVal outfile As String) As <MarshalAs(UnmanagedType.U4)> Int32
    End Function

    <DllImport("libraw.dll", EntryPoint:="libraw_unpack", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_unpack64(ByVal libraw_data As IntPtr) As <MarshalAs(UnmanagedType.U4)> Int32
    End Function

    <DllImport("libraw.dll", EntryPoint:="libraw_dcraw_process", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_dcraw_process64(ByVal libraw_data As IntPtr) As <MarshalAs(UnmanagedType.U4)> Int32
    End Function

    <DllImport("libraw.dll", EntryPoint:="libraw_close", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_close64(ByVal libraw_data As IntPtr) As <MarshalAs(UnmanagedType.U4)> Int32
    End Function

    <DllImport("libraw.dll", EntryPoint:="libraw_set_output_tif", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Sub libraw_set_output_tif64(ByVal libraw_data As IntPtr, ByVal value As Integer)
    End Sub


    <DllImport("libraw32.dll", EntryPoint:="libraw_init", ThrowOnUnmappableChar:=False, CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_init32(ByVal flag As Integer) As <MarshalAs(UnmanagedType.SysUInt)> IntPtr

    End Function

    <DllImport("libraw32.dll", EntryPoint:="libraw_open_file", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_open_file32(ByVal libraw_data As IntPtr, ByVal filename As String) As <MarshalAs(UnmanagedType.U4)> Int32
    End Function

    <DllImport("libraw32.dll", EntryPoint:="libraw_dcraw_ppm_tiff_writer", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_dcraw_ppm_tiff_writer32(ByVal libraw_data As IntPtr, ByVal outfile As String) As <MarshalAs(UnmanagedType.U4)> Int32
    End Function

    <DllImport("libraw32.dll", EntryPoint:="libraw_unpack", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_unpack32(ByVal libraw_data As IntPtr) As <MarshalAs(UnmanagedType.U4)> Int32
    End Function

    <DllImport("libraw32.dll", EntryPoint:="libraw_dcraw_process", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_dcraw_process32(ByVal libraw_data As IntPtr) As <MarshalAs(UnmanagedType.U4)> Int32
    End Function

    <DllImport("libraw32.dll", EntryPoint:="libraw_close", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_close32(ByVal libraw_data As IntPtr) As <MarshalAs(UnmanagedType.U4)> Int32
    End Function


    <DllImport("libraw32.dll", EntryPoint:="libraw_set_output_tif", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Sub libraw_set_output_tif32(ByVal libraw_data As IntPtr, ByVal value As Integer)
    End Sub


    Private Sub ReadImageFromCamera(ar As IAsyncResult)
        ' Reap the WaitBulb async call (this runs as its completion callback) so the
        ' delegate's async handle isn't leaked.
        Try
            CType(CType(ar, AsyncResult).AsyncDelegate, MyDelegate).EndInvoke(ar)
        Catch
        End Try
        Dim Pictures As XmlDocument                     'the XML with all the results from the camea
        Dim XPictures As XElement
        Dim Images As String = "" 'the array of the urls in the camera
        Dim nRead As Integer
        Dim SendStatus As Integer = -1
        Dim length As Integer = 0
        Dim buflen As Integer = 1024

        cameraImageReady = False
        If cameraAborted Then ' aborted during the exposure wait - don't download
            CurrentState = CameraStates.cameraIdle
            Exit Sub
        End If
        Pictures = New XmlDocument
        Dim PictureString As String
        Dim LookupImgtag As String = ""
        Dim tries As Int16 = 5
        Dim temp As String = ""

        Select Case ReadoutMode
            Case 0 'jpg
                LookupImgtag = "CAM_RAW_JPG"
            Case 1  'raw
                LookupImgtag = "CAM_RAW"
            Case 2  'thumb
                LookupImgtag = "CAM_LRGTN"
        End Select
        Try
            Do
                temp = SendLumixMessage(PLAYMODE)   'making sure the camera is in Playmode
                If temp.Contains("err") Then
                    Thread.Sleep(1000)
                End If
                tries -= 1
            Loop While (tries > 0 And temp.Contains("err"))

            PictureString = GetPix(1)
            ' Value comparison, not reference: 'IsNot ""' compares references and is
            ' therefore always True, so GetPix's documented "" failure return sailed
            ' through the guard into XElement.Parse("") and surfaced as
            ' "XmlException: Root element is missing" instead of the DriverException
            ' intended here. (Same reference-vs-value mistake fixed in the setup dialog.)
            If Not String.IsNullOrEmpty(PictureString) Then
                XPictures = XElement.Parse(PictureString)
            Else
                TL.LogMessage("ReadImageFromCamera", "the camera returned an empty content-browse response")
                Throw New ASCOM.DriverException("The camera returned an empty image-list response")
            End If

            Dim items As IEnumerable(Of XElement) =
        DirectCast(DirectCast(DirectCast(DirectCast(DirectCast(XPictures.FirstNode, System.[Xml].Linq.XContainer).FirstNode, System.[Xml].Linq.XContainer).FirstNode, System.[Xml].Linq.XContainer).FirstNode, System.[Xml].Linq.XContainer).FirstNode, System.[Xml].Linq.XContainer).Elements

            For Each it In items
                If it.HasAttributes Then
                    If it.LastAttribute.Name = "protocolInfo" Or it.FirstAttribute.Name = "protocolInfo" Then
                        If it.@protocolInfo.EndsWith(LookupImgtag) Then
                            Images = it.Value
                            Exit For
                        End If
                    End If
                End If
            Next


            If Images = "" Then
                Throw New ASCOM.DriverException
            End If

            SendLumixMessage(PLAYMODE)                'making sure the camera is in Playmode
            CurrentState = CameraStates.cameraReading
            CurrentPercentCompleted = 0

            nRead = 0

            Dim theResponse As HttpWebResponse
            Dim theRequest As HttpWebRequest
            Dim bytesread As Integer = 0
            Dim start_time As DateTime = Now
            Dim stop_time As DateTime
            Dim elapsed_time As TimeSpan
            Do
                theRequest = HttpWebRequest.Create(Images)
                TL.LogMessage("reading stream ", Images & " position " & nRead)
                theRequest.KeepAlive = True
                theRequest.ProtocolVersion = HttpVersion.Version11
                theRequest.ServicePoint.ConnectionLimit = 1
                If nRead > 0 Then
                    theRequest.AddRange(nRead)
                    GetPix(1) 'if the file not found happened then this trick is to get the camera in a readmode again and making sure it remembers the filename
                    TL.LogMessage("continuing the read where it stopped", Images & " position " & nRead)

                End If

                Try
                    theResponse = theRequest.GetResponse()

                Catch ex As Exception

                    TL.LogMessage("error in reading stream ", Images & " position " & nRead)
                    Exit Do

                End Try
                Dim writeStream As IO.FileStream
                writeStream = New FileStream(TempPath & LocalNameFor(Images), IO.FileMode.OpenOrCreate)
                If nRead > 0 Then
                    writeStream.Position = nRead
                End If

                TL.LogMessage("opening or creating  file", Images)
                Try
                    Do
                        Dim readBytes(buflen - 1) As Byte
                        CurrentPercentCompleted = Math.Min(nRead \ 80000, 100) 'assuming a jpg is not longer than 8MB (nRead\80000 avoids the Int32 overflow of 100*nRead on >21MB RAW)
                        bytesread = theResponse.GetResponseStream.Read(readBytes, 0, buflen)

                        nRead = nRead + bytesread
                        If bytesread = 0 Then
                            TL.LogMessage("reached end of stream ", Images & " position " & nRead)
                            Exit Do
                        End If
                        writeStream.Write(readBytes, 0, bytesread)
                        writeStream.Flush()
                        stop_time = Now
                        elapsed_time = stop_time.Subtract(start_time)
                        If elapsed_time.TotalSeconds > 30 Then
                            Throw New ASCOM.DriverException
                        End If

                    Loop
                    theResponse.GetResponseStream.Close()
                    writeStream.Flush()
                    writeStream.Close()
                    stop_time = Now
                    elapsed_time = stop_time.Subtract(start_time)
                    If elapsed_time.TotalSeconds > 30 Then
                        Throw New ASCOM.DriverException
                    End If

                Catch e As System.IO.IOException
                    TL.LogMessage("camera stopped streaming  ", Images & " position  " & nRead)
                    nRead -= 8 * buflen
                    theResponse.GetResponseStream.Close()
                    writeStream.Flush()
                    writeStream.Close()
                End Try
                stop_time = Now
                elapsed_time = stop_time.Subtract(start_time)
                If elapsed_time.TotalSeconds > 30 Then
                    Throw New ASCOM.DriverException
                End If
            Loop While bytesread > 0

            If ReadoutMode = 1 Then 'RAW . needs libraw conversion
                Try

                    Dim imagepath = TempPath & LocalNameFor(Images)
                    TiffFileName = imagepath.Substring(0, imagepath.Length() - 3) + "tif"

                    Dim libraw_data_t As IntPtr

                    If (IntPtr.Size = 8) Then

                        libraw_data_t = libraw_init64(1)
                        libraw_open_file64(libraw_data_t, imagepath)
                        libraw_unpack64(libraw_data_t)
                        libraw_set_output_tif64(libraw_data_t, 1)
                        libraw_dcraw_process64(libraw_data_t)
                        libraw_dcraw_ppm_tiff_writer64(libraw_data_t, TiffFileName)
                        libraw_close64(libraw_data_t)
                    Else
                        libraw_data_t = libraw_init32(1)
                        libraw_open_file32(libraw_data_t, imagepath)
                        libraw_unpack32(libraw_data_t)
                        libraw_set_output_tif32(libraw_data_t, 1)
                        libraw_dcraw_process32(libraw_data_t)
                        libraw_dcraw_ppm_tiff_writer32(libraw_data_t, TiffFileName)
                        libraw_close32(libraw_data_t)
                    End If
                    My.Computer.FileSystem.DeleteFile(TempPath & LocalNameFor(Images))
                Catch e As Exception
                    TL.LogMessage("Converting to tiff via DCRAW", Images & " file not found")
                End Try
            Else 'JPG image. VB can translate into TIFF natively
                Try

                    Dim imagepath = TempPath & LocalNameFor(Images)
                    Dim jpg = Image.FromFile(imagepath)

                    TiffFileName = imagepath.Substring(0, imagepath.Length() - 3) + "tif"
                    jpg.Save(TiffFileName, System.Drawing.Imaging.ImageFormat.Tiff)
                    jpg.Dispose() 'cleaning up aftermyself and removing the jpg file once it is used and transformed into a tiff
                    My.Computer.FileSystem.DeleteFile(imagepath)

                Catch e As Exception
                    TL.LogMessage("Converting to tiff via vb", Images & " file not found")
                End Try
            End If

        Catch ex As Exception
            ' Log what actually failed. This used to log the fixed string "error in
            ' reading image" and drop ex entirely, so a download failure left no way to
            ' tell a DLNA browse miss from an HTTP error from a disk write - the state
            ' went to cameraError with no diagnosis anywhere.
            TL.LogMessage("error in reading image", ex.GetType().Name & ": " & ex.Message)
            If ex.InnerException IsNot Nothing Then
                TL.LogMessage("error in reading image", "inner: " & ex.InnerException.GetType().Name & ": " & ex.InnerException.Message)
            End If
            cameraImageReady = False
            TL.LogMessage("Imageready", "False")
            ' Surface the failure as cameraError (was cameraIdle) so a client polling
            ' CameraState sees the error instead of hanging on ImageReady=False.
            CurrentState = CameraStates.cameraError
            Exit Sub
        End Try

        If cameraAborted Then ' aborted while downloading/converting - don't hand back an image
            cameraImageReady = False
            CurrentState = CameraStates.cameraIdle
            TL.LogMessage("Imageready", "False (aborted)")
        ElseIf Not String.IsNullOrEmpty(TiffFileName) AndAlso IO.File.Exists(TiffFileName) Then
            CurrentState = CameraStates.cameraIdle
            cameraImageReady = True
            TL.LogMessage("Imageready", "true")
        Else
            ' Conversion failed (no TIFF): don't report ImageReady=True and then throw
            ' FileNotFound from ImageArray - report the error state instead.
            cameraImageReady = False
            CurrentState = CameraStates.cameraError
            TL.LogMessage("Imageready", "False (no TIFF produced)")
        End If

    End Sub

    Private Shared Function GetEncoderInfo(ByVal mimeType As String) As ImageCodecInfo
        Dim j As Integer
        Dim encoders() As ImageCodecInfo
        encoders = ImageCodecInfo.GetImageEncoders()

        j = 0
        While j < encoders.Length
            If encoders(j).MimeType = mimeType Then
                Return encoders(j)
            End If
            j += 1
        End While
        Return Nothing

    End Function

    Public Property StartX() As Integer Implements ICameraV2.StartX
        Get
            TL.LogMessage("StartX Get", cameraStartX.ToString())
            Return cameraStartX
        End Get
        Set(value As Integer)
            cameraStartX = value
            TL.LogMessage("StartX set", value.ToString())
        End Set
    End Property

    Public Property StartY() As Integer Implements ICameraV2.StartY
        Get
            TL.LogMessage("StartY Get", cameraStartY.ToString())
            Return cameraStartY
        End Get
        Set(value As Integer)
            cameraStartY = value
            TL.LogMessage("StartY set", value.ToString())
        End Set
    End Property

    Public Sub StopExposure() Implements ICameraV2.StopExposure
        SendLumixMessage(SHUTTERSTOP)
    End Sub

#End Region

#Region "Private properties and methods"
    ' here are some useful properties and methods that can be used as required
    ' to help with

#Region "ASCOM Registration"

    Private Shared Sub RegUnregASCOM(ByVal bRegister As Boolean)

        Using P As New Profile() With {.DeviceType = "Camera"}
            If bRegister Then
                P.Register(driverID, driverDescription)
            Else
                P.Unregister(driverID)
            End If
        End Using

    End Sub

    <ComRegisterFunction()>
    Public Shared Sub RegisterASCOM(ByVal T As Type)

        RegUnregASCOM(True)

    End Sub

    <ComUnregisterFunction()>
    Public Shared Sub UnregisterASCOM(ByVal T As Type)

        RegUnregASCOM(False)

    End Sub

#End Region

    ''' <summary>
    ''' Returns true if there is a valid connection to the driver hardware
    ''' </summary>
    Private ReadOnly Property IsConnected As Boolean
        Get
            ' TODO check that the driver hardware connection exists and is connected to the hardware
            Return connectedState
        End Get
    End Property

    ''' <summary>
    ''' Use this function to throw an exception if we aren't connected to the hardware
    ''' </summary>
    ''' <param name="message"></param>
    Private Sub CheckConnected(ByVal message As String)
        If Not IsConnected Then
            Throw New NotConnectedException(message)
        End If
    End Sub



#End Region



End Class

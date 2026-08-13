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
Imports System.Web.Script.Serialization

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

    ' Populated from cameras.json by LoadCameraTable() (was a hardcoded literal).
    Public Shared ResolutionTable As String() = New String() {}


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

        LoadCameraTable()


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
        ' An unset temp folder used to be returned as-is, and the download then built its
        ' filenames as "" & name - a bare relative name, written into whatever the host
        ' application's working directory happens to be (NINA's install folder, say).
        ' A fresh profile is exactly this state, and it is reached without the user doing
        ' anything wrong: connecting without opening the setup dialog first.
        If String.IsNullOrWhiteSpace(p) Then Return NormalisePath(IO.Path.GetTempPath())
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
                If My.Settings.ConnectionMode.StartsWith("USB") Then
                    ConnectUsb()
                    Return
                End If
                ' Read the target first, then log it - logging before the assignment
                ' reported whatever the previous session left behind, which is what made
                ' a connect-to-nothing look like a normal connect in the trace.
                IPAddress = My.Settings.IPAddress
                TL.LogMessage("Connected Set", "Connecting to IP Address " + IPAddress)

                ' A profile that has never been through the setup dialog - a fresh install,
                ' or one reset by a COM re-registration - leaves IPAddress empty. Connecting
                ' anyway returned success and left the client holding a camera with nothing
                ' behind it: every later cam.cgi failed silently and the first exposure was
                ' the first symptom. Refuse here, and say what to do about it.
                If String.IsNullOrWhiteSpace(IPAddress) OrElse IPAddress = IPAddressDefault Then
                    connectedState = False
                    TL.LogMessage("Connected Set", "no camera IP configured - refusing to connect")
                    Throw New ASCOM.DriverException(
                        "No camera IP address is configured. Open the driver's Setup dialog, " &
                        "let it find the camera on the network (or switch to USB), and press OK.")
                End If
                TempPath = NormalisePath(My.Settings.TempPath)
                CurrentSpeed = My.Settings.Speed
                LogLibRawVersion()
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
                If My.Settings.ConnectionMode.StartsWith("USB") Then
                    connectedState = False
                    UsbTransport.Disconnect()
                    Return
                End If
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
            ' Not "Wifi" any more - the driver also talks to the camera over USB.
            Dim s_driverInfo As String = "Lumix ASCOM driver (WiFi + USB). Version: " + m_version.Major.ToString() + "." + m_version.Minor.ToString()
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

    ' Populated from cameras.json by LoadCameraTable() (was fixed-size, hardcoded).
    Private Resolutions() As MFTResolution = New MFTResolution() {}
    Private ResolutionsJPG() As MFTResolution = New MFTResolution() {}
    Private ResolutionsThumb() As MFTResolution = New MFTResolution() {}

    ''' <summary>
    ''' Populate the camera/resolution tables from cameras.json. Building all three
    ''' resolution arrays from one ordered source keeps them index-aligned by
    ''' construction, so there are no hand-maintained parallel arrays to drift.
    ''' </summary>
    Private Sub LoadCameraTable()
        Try
            Dim json As String = LoadCameraJson()
            If String.IsNullOrEmpty(json) Then Return
            Dim root = DirectCast(New JavaScriptSerializer().DeserializeObject(json), Dictionary(Of String, Object))

            Dim thumb = DirectCast(root("thumb"), Dictionary(Of String, Object))
            Dim tX As Integer = CInt(thumb("x")), tY As Integer = CInt(thumb("y"))

            Dim resArr = DirectCast(root("resolutions"), Object())
            Dim n As Integer = resArr.Length
            ReDim Resolutions(n - 1)
            ReDim ResolutionsJPG(n - 1)
            ReDim ResolutionsThumb(n - 1)
            Dim table(n - 1) As String
            For i As Integer = 0 To n - 1
                Dim e = DirectCast(resArr(i), Dictionary(Of String, Object))
                Dim cls As String = e("class").ToString()
                table(i) = cls
                Resolutions(i) = New MFTResolution With {._resolution = cls, ._X = CInt(e("rawX")), ._Y = CInt(e("rawY"))}
                ResolutionsJPG(i) = New MFTResolution With {._resolution = cls, ._X = CInt(e("jpgX")), ._Y = CInt(e("jpgY"))}
                ResolutionsThumb(i) = New MFTResolution With {._resolution = cls, ._X = tX, ._Y = tY}
            Next
            ResolutionTable = table

            Dim modelsD = DirectCast(root("models"), Dictionary(Of String, Object))
            Models.Clear()
            For Each kv As KeyValuePair(Of String, Object) In modelsD
                Models(kv.Key) = kv.Value.ToString()
            Next
        Catch ex As Exception
            Try : TL.LogMessage("LoadCameraTable", "Failed: " & ex.Message) : Catch : End Try
        End Try
    End Sub

    ''' <summary>Read cameras.json: an editable override next to the DLL, else the embedded copy.</summary>
    Private Shared Function LoadCameraJson() As String
        Try
            Dim dllDir As String = IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
            Dim ext As String = IO.Path.Combine(dllDir, "cameras.json")
            If IO.File.Exists(ext) Then Return IO.File.ReadAllText(ext)
        Catch
        End Try
        Try
            Dim asm = System.Reflection.Assembly.GetExecutingAssembly()
            Dim resName As String = asm.GetManifestResourceNames().FirstOrDefault(Function(nm) nm.EndsWith("cameras.json"))
            If resName IsNot Nothing Then
                Using s = asm.GetManifestResourceStream(resName)
                    Using r As New IO.StreamReader(s)
                        Return r.ReadToEnd()
                    End Using
                End Using
            End If
        Catch
        End Try
        Return ""
    End Function



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
            If My.Settings.ConnectionMode.StartsWith("USB") Then
                UsbTransport.SetIsoIndex(value) ' index into the camera's supported ISO list
                CurrentISO = value
                Return
            End If
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
            If My.Settings.ConnectionMode.StartsWith("USB") AndAlso UsbTransport.IsConnected Then
                ' Gain is an index, so the advertised list must be the same one SetIsoIndex
                ' indexes into — the camera's own ISO capability list, not the WiFi table.
                Return New ArrayList(UsbTransport.IsoDisplay())
            End If
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
            ReDim cameraImageArray(cameraNumX - 1, cameraNumY - 1) ' there are 3 channels: RVB.

            ' Pixels come either straight from LibRaw (RAW, decoded in memory) or from the
            ' TIFF the JPG path still produces. Both are 8-bit BGR, so everything below is
            ' the same code it always was.
            Dim stride As Int32
            Dim index As Int32
            Dim pixels As Byte()
            Dim srcW As Integer, srcH As Integer
            Dim Tiffimagefile As IO.FileStream = Nothing
            If _rgbPixels IsNot Nothing Then
                pixels = _rgbPixels
                stride = _rgbStride
                srcW = _rgbW
                srcH = _rgbH
            Else
                Tiffimagefile = New FileStream(TiffFileName, IO.FileMode.Open)
                Dim decoder As New TiffBitmapDecoder(Tiffimagefile, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default)
                Dim bitmapSource As BitmapSource = decoder.Frames(0)
                Dim bytesPerPixel As UShort = bitmapSource.Format.BitsPerPixel / 8
                stride = bitmapSource.PixelWidth * bytesPerPixel
                srcW = bitmapSource.PixelWidth
                srcH = bitmapSource.PixelHeight
                ReDim pixels(srcH * stride * 2)
                bitmapSource.CopyPixels(pixels, stride, 0)
            End If

            ' Clamp the copy to the smaller of the table size and the decoded image
            ' (the JPG/thumb is often smaller), so we never index past the pixel
            ' buffer or the output array.
            Dim imgW As Integer = Math.Min(cameraNumX, srcW)
            Dim imgH As Integer = Math.Min(cameraNumY, srcH)

            If ReadoutIsRaw() Then  'RAW
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

            ' The in-memory frame is a managed array - nothing to close or delete. Drop the
            ' reference so a second exposure cannot serve the previous frame's pixels.
            _rgbPixels = Nothing
            Try
                If Tiffimagefile IsNot Nothing Then
                    Tiffimagefile.Dispose() 'cleaning up aftermyself and removing the Tiff file once it is used
                    My.Computer.FileSystem.DeleteFile(TiffFileName)
                End If
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
            ' Already built for this exposure - hand back the same array, exactly as
            ' ImageArray does. This used to rebuild ~10 million boxed values on EVERY read,
            ' so a client that read the variant twice paid the full cost twice.
            If cameraImageArrayVariant IsNot Nothing Then
                TL.LogMessage("ImageArrayVariant Get", "returning the variant array already built for this exposure")
                Return cameraImageArrayVariant
            End If
            CurrentState = CameraStates.cameraDownload
            ' A client may read the variant without reading ImageArray first; build it
            ' then, rather than dereferencing a Nothing array (which surfaced as a raw
            ' NullReferenceException instead of an ASCOM exception).
            If cameraImageArray Is Nothing Then
                Dim ignored As Object = Me.ImageArray
            End If

            ' Array.Copy does the Integer -> Object boxing element by element in native
            ' code. The nested VB loop this replaces did the same work with two bounds
            ' checks and a 2-D index computation per pixel, and on a 10.2 MPix frame it
            ' took ~15 s - past the 10 s ConformU allows for ImageArrayVariant, which
            ' failed validation and aborted the rest of the run.
            Dim sw As System.Diagnostics.Stopwatch = System.Diagnostics.Stopwatch.StartNew()
            ReDim cameraImageArrayVariant(cameraNumX - 1, cameraNumY - 1)
            Array.Copy(cameraImageArray, cameraImageArrayVariant, cameraImageArray.Length)
            sw.Stop()
            TL.LogMessage("ImageArrayVariant Get",
                          "built " & cameraNumX & "x" & cameraNumY & " variant array in " & sw.ElapsedMilliseconds & " ms")
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
            TL.LogMessage("ReadoutMode Get", ReadoutModeName())
            Return CurrentROM
            'Throw New ASCOM.PropertyNotImplementedException("ReadoutMode", False)
        End Get
        Set(value As Short)
            ' Validate against the modes this transport actually offers - the list is
            ' shorter over USB. An out-of-range value used to index ROM() and escape as a
            ' raw IndexOutOfRangeException where ASCOM requires InvalidValueException.
            Dim modes As ArrayList = ActiveReadoutModes()
            If value < 0 OrElse value >= modes.Count Then
                TL.LogMessage("ReadoutMode Set", "rejected out-of-range value " & value.ToString())
                Throw New ASCOM.InvalidValueException("ReadoutMode", value.ToString(), "0.." & (modes.Count - 1).ToString())
            End If
            TL.LogMessage("ReadoutMode Set", CStr(modes(value)))
            If My.Settings.ConnectionMode.StartsWith("USB") Then
                ' No cam.cgi over USB. Extended can genuinely switch the body between RAW
                ' and JPEG; Standard cannot (the public SDK exports no ImageInfo_* calls),
                ' so its list is RAW-only and there is nothing to send.
                If My.Settings.ConnectionMode = "USBExtended" Then
                    UsbTransport.SetImageQuality(CStr(modes(value)) = "RAW")
                End If
                CurrentROM = value
                Return
            End If
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
            Dim modes As ArrayList = ActiveReadoutModes()
            TL.LogMessage("ReadoutModes Get", String.Join(", ", modes.ToArray()))
            Return modes
            'Throw New ASCOM.PropertyNotImplementedException("ReadoutModes", False)
        End Get
    End Property

    ''' <summary>
    ''' The readout modes this transport can actually deliver. WiFi fetches JPG, RAW or a
    ''' thumbnail over DLNA. USB does not: the capture path takes whatever the body
    ''' produces, there is no thumbnail call at all, and only the Tether ABI can change
    ''' the image quality (ImageInfo_Set_ImageQuality) - the public SDK exports nothing
    ''' for it. Advertising all three over USB offered choices that silently did nothing.
    ''' </summary>
    Private Function ActiveReadoutModes() As ArrayList
        Return ReadoutModesFor(My.Settings.ConnectionMode)
    End Function

    ''' <summary>
    ''' The readout modes a given transport can deliver. Shared so the setup dialog
    ''' offers exactly what the driver will honour - it used to show a fixed
    ''' JPG/RAW/Thumb list from the designer regardless of transport.
    ''' </summary>
    Public Shared Function ReadoutModesFor(connectionMode As String) As ArrayList
        Select Case connectionMode
            Case "USBExtended" : Return New ArrayList(New String() {"RAW", "JPG"})
            Case "USB" : Return New ArrayList(New String() {"RAW"})
            Case Else : Return New ArrayList(New String() {"JPG", "RAW", "Thumb"})
        End Select
    End Function

    ''' <summary>Name of the selected readout mode, or "RAW" if the index is unusable.</summary>
    Private Function ReadoutModeName() As String
        Dim modes As ArrayList = ActiveReadoutModes()
        If CurrentROM >= 0 AndAlso CurrentROM < modes.Count Then Return CStr(modes(CurrentROM))
        Return "RAW"
    End Function

    ''' <summary>True when the selected readout mode is RAW (was 'CurrentROM = 1').</summary>
    Private Function ReadoutIsRaw() As Boolean
        Return ReadoutModeName() = "RAW"
    End Function

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
    ' frame. The total budget stays ~20s - comfortably longer than a RAW commit observed
    ' on a GH5S over WiFi - but polled at 150ms rather than 500ms: the wait ends whenever
    ' the camera is ready, so a coarse interval just adds up to 500ms of pure idling to
    ' every exposure. This sits directly between the shutter closing and the download
    ' starting, so it is dead time on every single frame.
    Private Const CONTENT_READY_WAIT_MS As Integer = 150
    Private Const CONTENT_READY_RETRIES As Integer = 130

    ' The DLNA ContentDirectory can flap (HTTP 500 / UPnP 701 "No such object" /
    ' an empty list) while it reindexes, or when the card holds files it cannot
    ' serve (foreign RAW such as Sony .ARW, which cam.cgi still counts); retry the
    ' browse a few times before giving up.
    Private Const BROWSE_RETRIES As Integer = 8

    ' Same reasoning for the playmode switch that precedes the content browse: the camera
    ' rejects it while it is still committing the frame, so the loop around it is a
    ' readiness poll. 250ms x 24 keeps the ~6s ceiling the old 1000ms x 5 gave.
    Private Const PLAYMODE_WAIT_MS As Integer = 250
    Private Const PLAYMODE_RETRIES As Integer = 24

    ' WiFi image download tuning. RAW (RW2, ~18 MB) over the camera's own access point
    ' cannot finish inside the old flat 30 s wall-clock cap - at the ~0.4 MB/s the body
    ' streams that is a ~47 s transfer - and the camera tends to serve the object in
    ' bounded chunks, closing the stream early. So the download resumes with a byte Range
    ' until it has the whole Content-Length, bounded by an overall ceiling and a
    ' no-progress bailout rather than a fixed wall clock.
    Private Const DOWNLOAD_RESPONSE_TIMEOUT_MS As Integer = 20000 ' cap on GetResponse()
    Private Const DOWNLOAD_STALL_MS As Integer = 15000           ' ReadWriteTimeout: a mid-stream stall throws (and resumes) instead of blocking
    Private Const DOWNLOAD_MAX_MS As Integer = 180000            ' overall ceiling for one frame's download
    Private Const DOWNLOAD_MAX_NOPROGRESS As Integer = 5         ' consecutive zero-byte attempts before giving up

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

    ''' <param name="ensurePlaymode">
    ''' Send PLAYMODE before browsing. The normal readout path has just done that itself,
    ''' so it passes False; the download-resume path calls this precisely to nudge the
    ''' camera back into playmode and needs it.
    ''' </param>
    Private Function GetPix(num As Int16, Optional ensurePlaymode As Boolean = True) As String
        If ensurePlaymode Then SendLumixMessage(PLAYMODE)
        ' Right after a capture the camera answers get_content_info with
        ' <result>err_busy</result> while it is still committing the file - longest for
        ' RAW. NumberPix() then returns "", and 'NumPix - num' threw
        ' InvalidCastException ("" -> Double). Wait for a real count instead.
        Dim NumPix As String = NumberPix()
        Dim waits As Integer = 0
        Do While String.IsNullOrEmpty(NumPix) AndAlso waits < CONTENT_READY_RETRIES
            Thread.Sleep(CONTENT_READY_WAIT_MS)
            waits += 1
            NumPix = NumberPix()
        Loop
        ' camCount is cam.cgi's view of the card. It is only a HINT for indexing the
        ' DLNA browse: the DLNA ContentDirectory keeps its own, frequently different
        ' count (RAW+JPG pairing, videos, or files it won't enumerate - foreign RAW
        ' such as Sony .ARW that cam.cgi still counts). Indexing the browse by cam.cgi's
        ' number overshoots the DLNA range and returns an empty list, which used to
        ' surface downstream as a NullReferenceException. -1 means
        ' cam.cgi never answered; the DLNA TotalMatches below then carries the browse.
        Dim camCount As Integer = -1
        Integer.TryParse(NumPix, camCount)

        ' Browse with a few retries: the DLNA server can transiently answer 500 /
        ' UPnP 701 / an empty list while it reindexes. Aim at cam.cgi's count first
        ' (correct when the two services agree, which is the common case), and if that
        ' comes back short, re-aim off the DLNA server's OWN TotalMatches.
        For attempt As Integer = 1 To BROWSE_RETRIES
            Dim body As String = ""
            If camCount >= 0 Then body = DoBrowse(Math.Max(camCount - num, 0), num)
            Dim returned As Integer = ParseIntTag(body, "NumberReturned")
            Dim dlnaTotal As Integer = ParseIntTag(body, "TotalMatches")
            If returned < num AndAlso dlnaTotal > 0 AndAlso dlnaTotal <> camCount Then
                TL.LogMessage("GetPix", "cam.cgi count " & camCount & " <> DLNA TotalMatches " & dlnaTotal & " - re-aiming browse")
                body = DoBrowse(Math.Max(dlnaTotal - num, 0), num)
                returned = ParseIntTag(body, "NumberReturned")
            End If
            If returned >= num AndAlso Not String.IsNullOrEmpty(body) Then Return body
            TL.LogMessage("GetPix", "browse attempt " & attempt & "/" & BROWSE_RETRIES & " returned " & returned & " item(s) (camCount=" & camCount & ")")
            Thread.Sleep(CONTENT_READY_WAIT_MS)
        Next
        Return "" ' caller raises a clean DriverException on empty
    End Function

    ' One DLNA ContentDirectory Browse. Returns the entity-decoded SOAP body, or "" on
    ' any transport/HTTP error - including the 500 / UPnP-701 the camera throws while
    ' reindexing - so the caller can retry or re-aim rather than crash.
    Private Function DoBrowse(start As Integer, count As Integer) As String
        Try
            Dim HTTPReq As HttpWebRequest = WebRequest.Create("http://" + IPAddress + CDS_Control)
            HTTPReq.ContentType = "text/xml; charset=""utf-8"""
            HTTPReq.Method = "POST"
            HTTPReq.Accept = "text/xml"
            HTTPReq.Headers.Add("soapaction", "urn:schemas-upnp-org:service:ContentDirectory:1#Browse")
            Using rs As New StreamWriter(HTTPReq.GetRequestStream(), Encoding.UTF8)
                rs.Write(SoapEnvelop(start, count))
            End Using
            Dim resp As HttpWebResponse = CType(HTTPReq.GetResponse(), HttpWebResponse)
            If resp.StatusCode = HttpStatusCode.Accepted OrElse resp.StatusCode = HttpStatusCode.OK Then
                Using sr As New StreamReader(resp.GetResponseStream())
                    Dim text As String = sr.ReadToEnd()
                    Return text.Replace("&amp;", "&").Replace("&apos;", "'").Replace("&quot;", """").Replace("&lt;", "<").Replace("&gt;", ">")
                End Using
            End If
            Return ""
        Catch e As WebException
            ' ProtocolError carries the camera's 500/701 body; we only need to know it
            ' failed so the caller can retry.
            TL.LogMessage("DoBrowse", "browse " & start & "/" & count & " failed: " & e.Message)
            Return ""
        End Try
    End Function

    ' Pull an integer SOAP-envelope tag (NumberReturned / TotalMatches) out of a browse
    ' response. -1 when absent so callers can distinguish "0 items" from "no answer".
    Private Shared Function ParseIntTag(xml As String, tag As String) As Integer
        If String.IsNullOrEmpty(xml) Then Return -1
        Dim m = System.Text.RegularExpressions.Regex.Match(xml, "<" & tag & ">(\d+)</" & tag & ">")
        If m.Success Then Return CInt(m.Groups(1).Value)
        Return -1
    End Function



    'formats a message to be sent to the maera
    Public Function SendLumixMessage(LumixMessage As String, Optional timeoutMs As Integer = 0) As String
        ' No IP configured yet (e.g. a host connected without opening setup):
        ' don't build an invalid "http:///..." URI, which throws an *uncaught*
        ' UriFormatException. Just no-op with an empty response.
        If String.IsNullOrEmpty(IPAddress) Then Return ""
        Dim request = WebRequest.Create("http://" + IPAddress + "/" + LumixMessage)
        ' A slow or unresponsive camera must not freeze a caller running on the UI thread -
        ' e.g. Live View close sending "stopstream". Default WebRequest.Timeout is 100 s; an
        ' explicit short timeout caps the wait when the caller passes one.
        If timeoutMs > 0 Then request.Timeout = timeoutMs
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


    ''' <summary>Connect over USB (Standard/public SDK) and set sensor geometry from the model.</summary>
    Private Sub ConnectUsb()
        Try
            UsbTransport.Connect(My.Settings.ConnectionMode = "USBExtended")
            MODEL = UsbTransport.ModelName
            ' No TempPath over USB: the frame is decoded from memory and the only file
            ' produced is our own TIFF, which goes to the system temp area. The setup
            ' dialog's temp folder applies to the WiFi download path only.
            LogLibRawVersion()
            Dim w As Integer, h As Integer, p As Double
            UsbTransport.GetSpecs(MODEL, w, h, p)
            ccdWidth = w : ccdHeight = h
            cameraNumX = w : cameraNumY = h
            pixelSize = p                     ' PixelSizeX/Y in microns
            sensormmx = p * w / 1000.0        ' keep sensor-mm consistent with pitch
            sensormmy = p * h / 1000.0
            ' Honour the transfer format chosen in the setup dialog, falling back to RAW
            ' (index 0 in the USB lists). Set the field, not the property, whose WiFi
            ' branch posts cam.cgi.
            Dim usbModes As ArrayList = ActiveReadoutModes()
            Dim romIdx As Integer = usbModes.IndexOf(My.Settings.TransferFormat)
            If romIdx < 0 Then romIdx = usbModes.IndexOf("RAW")
            If romIdx < 0 Then romIdx = 0
            CurrentROM = CUShort(romIdx)
            If My.Settings.ConnectionMode = "USBExtended" Then
                UsbTransport.SetImageQuality(CStr(usbModes(romIdx)) = "RAW")
            End If
            connectedState = True
            TL.LogMessage("Connected Set", "USB connected: " & MODEL & " (" & w & "x" & h & ")")
        Catch ex As Exception
            connectedState = False
            TL.LogMessage("USB connect failed", ex.Message)
            Throw New ASCOM.DriverException("USB connect failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>Background worker: one-shot USB capture -> file -> TIFF -> ImageReady.</summary>
    Private Sub UsbCaptureWorker()
        Try
            ' Phase timings. Without these the only thing the log shows is "bulb 2s" and,
            ' fifteen seconds later, "image ready" - which says nothing about whether the
            ' time went on the camera, the SDK transfer or the decode.
            Dim swPhase As System.Diagnostics.Stopwatch = System.Diagnostics.Stopwatch.StartNew()
            Dim dur As Double = cameraLastExposureDuration
            Dim res As ASCOM.Lumix.Usb.CaptureResult
            If UsbTransport.IsExtended AndAlso dur > 1.0 Then
                ' Extended mode: hold the shutter open for the exact time (bulb), any duration incl. >60s.
                TL.LogMessage("USB capture", "bulb " & dur & "s")
                res = UsbTransport.CaptureBulb(dur, CInt(dur * 1000) + 120000)
            Else
                ' Snap to the nearest supported discrete shutter speed and fire a one-shot.
                Dim actual As Double = UsbTransport.SetShutterSeconds(dur)
                TL.LogMessage("USB capture", "requested " & dur & "s -> nearest " & actual & "s")
                res = UsbTransport.Capture(90000)
            End If
            If res IsNot Nothing AndAlso res.Success Then
                Dim msCapture As Long = swPhase.ElapsedMilliseconds
                TL.LogMessage("USB timing", "total " & msCapture & " ms = setup " & res.MsSetup &
                              " + expose " & res.MsExpose & " + transfer " & res.MsTransfer &
                              " ms (" & res.Data.Length & " bytes)")
                swPhase.Restart()
                ConvertToTiffFromBuffer(res.Data, res.Format <> 1) ' format 1 = JPEG, otherwise RAW
                TL.LogMessage("USB timing", "decode " & swPhase.ElapsedMilliseconds & " ms")
                If HaveDecodedFrame() Then
                    cameraImageReady = True
                    CurrentState = CameraStates.cameraIdle
                    TL.LogMessage("USB capture", "image ready: " & res.Data.Length & " bytes decoded from memory")
                Else
                    CurrentState = CameraStates.cameraError
                    TL.LogMessage("USB capture", "conversion produced no TIFF")
                End If
            ElseIf res IsNot Nothing AndAlso res.Error = "Aborted." Then
                ' A capture the client aborted is not a failure - go back to idle, or
                ' every AbortExposure leaves the camera reporting cameraError.
                CurrentState = CameraStates.cameraIdle
                TL.LogMessage("USB capture", "aborted by the client")
            Else
                CurrentState = CameraStates.cameraError
                TL.LogMessage("USB capture failed", If(res IsNot Nothing, res.Error, "null result"))
            End If
        Catch ex As Exception
            CurrentState = CameraStates.cameraError
            TL.LogMessage("USB capture exception", ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Convert a captured frame held in memory to the TIFF the ImageArray path reads.
    ''' Used by the USB path, where the SDK hands the whole object back as a byte array:
    ''' LibRaw decodes it with libraw_open_buffer and System.Drawing decodes a JPEG from a
    ''' MemoryStream, so the capture never touches the disk. That removes a ~24 MB write
    ''' plus read per RAW frame, the temp folder setting, and the fixed "usbcap" filename
    ''' two concurrent captures used to collide on.
    ''' </summary>
    Private Sub ConvertToTiffFromBuffer(data As Byte(), isRaw As Boolean, Optional count As Integer = -1)
        If data Is Nothing OrElse data.Length = 0 Then
            TL.LogMessage("ConvertToTiff", "empty capture buffer")
            Return
        End If
        If count < 0 OrElse count > data.Length Then count = data.Length

        ' The TIFF still goes to disk - ImageArray decodes it with TiffBitmapDecoder - but
        ' it is ours alone, so it lives in the system temp area under a unique name rather
        ' than in a user-configured folder.
        TiffFileName = IO.Path.Combine(IO.Path.GetTempPath(),
                                       "lumix-" & Guid.NewGuid().ToString("N") & ".tif")
        Try
            If isRaw Then
                DecodeRawBuffer(data, count)
                Return
            Else
                ' Image.FromStream keeps using the stream for the life of the Image, so the
                ' save has to happen before it is disposed.
                Using ms As New IO.MemoryStream(data, 0, count, writable:=False)
                    Using jpg = Image.FromStream(ms)
                        jpg.Save(TiffFileName, System.Drawing.Imaging.ImageFormat.Tiff)
                    End Using
                End Using
            End If
        Catch ex As Exception
            TL.LogMessage("ConvertToTiff", "failed: " & ex.Message)
        End Try
    End Sub

    ' The decoded frame, held as 8-bit BGR exactly as TiffBitmapDecoder used to hand it
    ' over, so the ImageArray pixel loops are unchanged. Non-Nothing means ImageArray
    ' should use this instead of opening a TIFF.
    Private _rgbPixels As Byte()
    Private _rgbW As Integer
    Private _rgbH As Integer
    Private _rgbStride As Integer

    ''' <summary>True once a frame is decoded, whether it came back in memory or as a TIFF.</summary>
    Private Function HaveDecodedFrame() As Boolean
        If _rgbPixels IsNot Nothing Then Return True
        Return Not String.IsNullOrEmpty(TiffFileName) AndAlso IO.File.Exists(TiffFileName)
    End Function

    ''' <summary>
    ''' Decode a RAW buffer to pixels without any file at all: libraw_open_buffer in,
    ''' libraw_dcraw_make_mem_image out. The old route wrote a ~31 MB TIFF and had WPF
    ''' read it straight back - measured at 303 ms to write plus 1450 ms to decode, all of
    ''' it avoidable.
    '''
    ''' One deliberate behaviour change: WPF applied the sRGB ICC profile LibRaw embeds in
    ''' that TIFF, so the values handed to the imaging software were colour-managed for a
    ''' display. make_mem_image returns the processed values directly. For astrophotography
    ''' that is the more faithful choice - a display transform has no business being baked
    ''' into measurement data - but it does mean pixel values differ from earlier releases.
    ''' </summary>
    ''' <param name="count">
    ''' Bytes of <paramref name="data"/> that are actually the frame. The WiFi path hands
    ''' over the MemoryStream's own backing array, which is larger than the download, so
    ''' the array length is not the file length. -1 means "all of it".
    ''' </param>
    Private Sub DecodeRawBuffer(data As Byte(), Optional count As Integer = -1)
        If count < 0 OrElse count > data.Length Then count = data.Length
        _rgbPixels = Nothing
        ' LibRaw does NOT copy the buffer, so it has to stay put until unpack() has read
        ' it - pin for the whole open/unpack/process sequence.
        Dim pin As GCHandle = GCHandle.Alloc(data, GCHandleType.Pinned)
        Dim h As IntPtr = IntPtr.Zero
        Dim img As IntPtr = IntPtr.Zero
        Try
            Dim is64 As Boolean = (IntPtr.Size = 8)
            Dim rc As Integer = 0
            Dim sw As System.Diagnostics.Stopwatch = System.Diagnostics.Stopwatch.StartNew()
            Dim msUnpack As Long, msProcess As Long, msMem As Long
            If is64 Then
                h = libraw_init64(1)
                libraw_open_buffer64(h, pin.AddrOfPinnedObject(), New IntPtr(count))
                libraw_unpack64(h)
                msUnpack = sw.ElapsedMilliseconds : sw.Restart()
                libraw_dcraw_process64(h)
                msProcess = sw.ElapsedMilliseconds : sw.Restart()
                img = libraw_dcraw_make_mem_image64(h, rc)
            Else
                h = libraw_init32(1)
                libraw_open_buffer32(h, pin.AddrOfPinnedObject(), New IntPtr(count))
                libraw_unpack32(h)
                msUnpack = sw.ElapsedMilliseconds : sw.Restart()
                libraw_dcraw_process32(h)
                msProcess = sw.ElapsedMilliseconds : sw.Restart()
                img = libraw_dcraw_make_mem_image32(h, rc)
            End If
            msMem = sw.ElapsedMilliseconds : sw.Restart()

            If img = IntPtr.Zero Then
                TL.LogMessage("DecodeRawBuffer", "make_mem_image returned nothing (err " & rc & ")")
                Return
            End If

            Dim hdr As LibRawProcessedImage = DirectCast(Marshal.PtrToStructure(img, GetType(LibRawProcessedImage)), LibRawProcessedImage)
            If hdr.colors <> 3 OrElse hdr.bits <> 8 Then
                TL.LogMessage("DecodeRawBuffer", "unexpected image: colors=" & hdr.colors & " bits=" & hdr.bits)
                Return
            End If

            Dim raw(CInt(hdr.data_size) - 1) As Byte
            Marshal.Copy(img + Marshal.SizeOf(GetType(LibRawProcessedImage)), raw, 0, raw.Length)

            ' make_mem_image gives RGB; the pixel loops below expect the BGR that WPF
            ' produced from the TIFF (they read index+2 as red). Swap in place rather than
            ' touching the loops - verified against the old path byte for byte.
            For i As Integer = 0 To raw.Length - 3 Step 3
                Dim t As Byte = raw(i)
                raw(i) = raw(i + 2)
                raw(i + 2) = t
            Next

            _rgbPixels = raw
            _rgbW = hdr.width
            _rgbH = hdr.height
            _rgbStride = hdr.width * 3
            TL.LogMessage("DecodeRawBuffer", _rgbW & "x" & _rgbH & " decoded in memory, no TIFF written")
            TL.LogMessage("DecodeRawBuffer", "unpack " & msUnpack & " + demosaic " & msProcess &
                          " + make_mem_image " & msMem & " + copy/swap " & sw.ElapsedMilliseconds & " ms")
        Catch ex As Exception
            _rgbPixels = Nothing
            TL.LogMessage("DecodeRawBuffer", "failed: " & ex.Message)
        Finally
            If img <> IntPtr.Zero Then
                If IntPtr.Size = 8 Then libraw_dcraw_clear_mem64(img) Else libraw_dcraw_clear_mem32(img)
            End If
            If h <> IntPtr.Zero Then
                If IntPtr.Size = 8 Then libraw_close64(h) Else libraw_close32(h)
            End If
            pin.Free()
        End Try
    End Sub

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
        _rgbPixels = Nothing
        cameraLastExposureDuration = Duration
        exposureStart = DateTime.Now
        If My.Settings.ConnectionMode.StartsWith("USB") Then
            ' USB: fire the capture on a background thread so StartExposure returns
            ' promptly. NOTE: exposure-time -> raw shutter mapping is a follow-up; this
            ' captures at the camera's current shutter setting.
            CurrentState = CameraStates.cameraExposing
            Dim t As New System.Threading.Thread(AddressOf UsbCaptureWorker)
            t.IsBackground = True
            t.Start()
            Return
        End If
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

    ''' <summary>
    ''' Record which LibRaw actually got loaded. The DLL carries no version resource, so
    ''' without this a "my RAW will not decode" report cannot be answered without the user
    ''' hashing the file - and the DLL is deliberately replaceable in place (the driver
    ''' only calls the flat C API through opaque handles and marshals no LibRaw struct, so
    ''' dropping in a newer build is safe).
    ''' </summary>
    ' Per instance, not per process: each connection writes its own trace file, and the
    ' whole point is that the file answers "which LibRaw decoded this?" on its own.
    Private _librawLogged As Boolean
    Private Sub LogLibRawVersion()
        If _librawLogged Then Return
        _librawLogged = True
        Try
            Dim p As IntPtr = If(IntPtr.Size = 8, libraw_version64(), libraw_version32())
            TL.LogMessage("LibRaw", If(p = IntPtr.Zero, "version unavailable", Marshal.PtrToStringAnsi(p)))
        Catch ex As Exception
            TL.LogMessage("LibRaw", "version query failed: " & ex.Message)
        End Try
    End Sub

    <DllImport("libraw.dll", EntryPoint:="libraw_version", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_version64() As IntPtr
    End Function

    <DllImport("libraw32.dll", EntryPoint:="libraw_version", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_version32() As IntPtr
    End Function

    ' Decode straight from the capture buffer instead of a file. Present since 0.19, so it
    ' works with both the shipped 64-bit LibRaw and the older 32-bit one. The size argument
    ' is size_t, hence IntPtr rather than Integer.
    <DllImport("libraw.dll", EntryPoint:="libraw_open_buffer", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_open_buffer64(ByVal libraw_data As IntPtr, ByVal buffer As IntPtr, ByVal size As IntPtr) As <MarshalAs(UnmanagedType.U4)> Int32
    End Function

    <DllImport("libraw32.dll", EntryPoint:="libraw_open_buffer", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_open_buffer32(ByVal libraw_data As IntPtr, ByVal buffer As IntPtr, ByVal size As IntPtr) As <MarshalAs(UnmanagedType.U4)> Int32
    End Function

    ''' <summary>
    ''' LibRaw's processed-image header, as returned by libraw_dcraw_make_mem_image. The
    ''' pixel data follows immediately after it. This is the one LibRaw structure the
    ''' driver marshals, and it is the stable one - four ushorts and two ints, unchanged
    ''' since 0.14. libraw_data_t, which does churn between versions, is still never
    ''' touched, so replacing libraw.dll in place remains safe.
    ''' </summary>
    <StructLayout(LayoutKind.Sequential)>
    Private Structure LibRawProcessedImage
        Public type As Integer          ' LibRaw_image_formats: 1 = JPEG, 2 = BITMAP
        Public height As UShort
        Public width As UShort
        Public colors As UShort
        Public bits As UShort
        Public data_size As UInteger
    End Structure

    <DllImport("libraw.dll", EntryPoint:="libraw_dcraw_make_mem_image", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_dcraw_make_mem_image64(ByVal libraw_data As IntPtr, ByRef errcode As Integer) As IntPtr
    End Function

    <DllImport("libraw32.dll", EntryPoint:="libraw_dcraw_make_mem_image", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_dcraw_make_mem_image32(ByVal libraw_data As IntPtr, ByRef errcode As Integer) As IntPtr
    End Function

    <DllImport("libraw.dll", EntryPoint:="libraw_dcraw_clear_mem", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Sub libraw_dcraw_clear_mem64(ByVal img As IntPtr)
    End Sub

    <DllImport("libraw32.dll", EntryPoint:="libraw_dcraw_clear_mem", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Sub libraw_dcraw_clear_mem32(ByVal img As IntPtr)
    End Sub

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
        ' 64 KB, not 1 KB. At 1 KB an 18.7 MB RAW took ~18,300 trips round the read loop,
        ' each allocating a fresh buffer, taking a timestamp and - worst of all - calling
        ' Flush() on the file stream, i.e. one forced disk write per kilobyte.
        Dim buflen As Integer = 65536

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
            ' Phase timings for the WiFi readout, mirroring the USB path - the gap between
            ' the shutter closing and the first byte arriving used to be ~3 s of unexplained
            ' HTTP chatter.
            Dim swWifi As System.Diagnostics.Stopwatch = System.Diagnostics.Stopwatch.StartNew()
            ' The camera refuses playmode until it has finished writing the frame to its
            ' card, so this loop is a readiness poll, not a retry. It used to back off a
            ' flat second between attempts, which rounds the wait up to the next whole
            ' second: the camera being ready at 2.1 s cost 3 s. Poll four times as often
            ' for the same ceiling.
            tries = PLAYMODE_RETRIES
            Do
                temp = SendLumixMessage(PLAYMODE)   'making sure the camera is in Playmode
                If temp.Contains("err") Then
                    TL.LogMessage("waiting for playmode", swWifi.ElapsedMilliseconds & " ms: " & temp.Trim())
                    Thread.Sleep(PLAYMODE_WAIT_MS)
                End If
                tries -= 1
            Loop While (tries > 0 And temp.Contains("err"))
            TL.LogMessage("WiFi timing", "playmode " & swWifi.ElapsedMilliseconds & " ms")
            swWifi.Restart()

            ' The loop above has just put the camera in playmode, so GetPix must not send
            ' it a second time - every one of these is a full HTTP round-trip to a camera
            ' on the far end of a wireless link.
            PictureString = GetPix(1, ensurePlaymode:=False)
            TL.LogMessage("WiFi timing", "content browse " & swWifi.ElapsedMilliseconds & " ms")
            swWifi.Restart()
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

            ' The DIDL-Lite that carries the items sits five nodes deep
            ' (Envelope>Body>BrowseResponse>Result>DIDL-Lite). If the browse came back
            ' well-formed but item-less - the camera reindexing, or an index past the
            ' DLNA range slipping through - one of those FirstNodes is Nothing and this
            ' chain threw a bare NullReferenceException that told nobody anything. Walk it
            ' defensively and raise a diagnosable error instead.
            Dim items As IEnumerable(Of XElement) = Nothing
            Try
                Dim node As System.[Xml].Linq.XNode = XPictures.FirstNode
                For depth As Integer = 1 To 4
                    node = DirectCast(node, System.[Xml].Linq.XContainer).FirstNode
                Next
                items = DirectCast(node, System.[Xml].Linq.XContainer).Elements
            Catch
                items = Nothing
            End Try
            If items Is Nothing Then
                TL.LogMessage("ReadImageFromCamera", "browse response had no item list - DLNA content empty or reindexing")
                Throw New ASCOM.DriverException("The camera returned no browsable image (DLNA content list empty - the card may hold files the camera cannot serve, e.g. foreign RAW such as Sony .ARW, or it is still reindexing). Remove foreign/unreadable files or power-cycle the camera, then retry.")
            End If

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
                Throw New ASCOM.DriverException("No " & LookupImgtag & " resource in the camera's newest content item (wrong readout mode for what is on the card, or that file type is not present).")
            End If

            ' No PLAYMODE here: the retry loop above set it and the content browse that
            ' followed cannot have changed it. This was the third identical round-trip in
            ' a single readout.
            CurrentState = CameraStates.cameraReading
            CurrentPercentCompleted = 0

            nRead = 0

            ' The download lands in memory, not in TempPath. The file used to exist only to
            ' be handed straight back to the decoder and deleted, costing an ~18.7 MB write
            ' plus read per RAW frame. The camera serves the object in bounded chunks and
            ' closes the stream early, so we resume with a byte Range until we have the whole
            ' Content-Length, bounded by an overall ceiling and a no-progress bailout rather
            ' than the old flat 30 s wall clock that RAW could never beat.
            Dim buffer As New IO.MemoryStream(24 * 1024 * 1024)
            Dim expectedLen As Long = -1    ' Content-Length of the whole object, once known
            Dim attempts As Integer = 0
            Dim noProgress As Integer = 0
            Dim restarts As Integer = 0     ' times the camera ignored Range and served from 0
            Dim complete As Boolean = False

            Do
                attempts += 1
                Dim posBefore As Integer = nRead
                Dim cleanEof As Boolean = False

                Dim theRequest As HttpWebRequest = DirectCast(HttpWebRequest.Create(Images), HttpWebRequest)
                theRequest.KeepAlive = True
                theRequest.ProtocolVersion = HttpVersion.Version11
                theRequest.ServicePoint.ConnectionLimit = 1
                theRequest.Timeout = DOWNLOAD_RESPONSE_TIMEOUT_MS
                theRequest.ReadWriteTimeout = DOWNLOAD_STALL_MS ' a mid-stream stall raises instead of blocking, so we resume
                If nRead > 0 Then
                    theRequest.AddRange(nRead)  ' resume from where the previous attempt stopped
                    GetPix(1)                   ' nudge the camera back into serving this file (it forgets between requests)
                End If
                TL.LogMessage("download", "attempt " & attempts & " GET from byte " & nRead & If(nRead > 0, " (range)", ""))

                Dim theResponse As HttpWebResponse = Nothing
                Try
                    theResponse = DirectCast(theRequest.GetResponse(), HttpWebResponse)
                Catch ex As Exception
                    TL.LogMessage("download", "GetResponse failed at " & nRead & ": " & ex.Message)
                End Try

                If theResponse IsNot Nothing Then
                    Dim status As Integer = CInt(theResponse.StatusCode)
                    Dim bodyLen As Long = theResponse.ContentLength
                    If nRead = 0 Then
                        expectedLen = bodyLen ' first (full) response carries the whole length
                    ElseIf status = 200 Then
                        ' The camera ignored our Range and restarted from the top. Appending
                        ' would duplicate the prefix; if it does this every time, resume is
                        ' impossible and RAW simply cannot be pulled over WiFi.
                        restarts += 1
                        TL.LogMessage("download", "camera ignored Range (HTTP 200 on resume, restart " & restarts & ")")
                        If restarts > 1 Then
                            Throw New ASCOM.DriverException("Camera does not honour HTTP Range on RW2 (only " & bodyLen &
                                " bytes served per request) - RAW cannot be completed over WiFi; use the USB path for RAW.")
                        End If
                        buffer.SetLength(0)
                        nRead = 0
                        posBefore = 0
                        expectedLen = bodyLen
                    End If
                    TL.LogMessage("download", "HTTP " & status & " bodyLen=" & bodyLen & " total-expected=" & expectedLen)

                    buffer.Position = nRead
                    Try
                        Dim readBytes(buflen - 1) As Byte
                        Dim src As IO.Stream = theResponse.GetResponseStream()
                        Do
                            Dim n As Integer = src.Read(readBytes, 0, buflen)
                            If n = 0 Then
                                cleanEof = True
                                Exit Do
                            End If
                            buffer.Write(readBytes, 0, n)
                            nRead += n
                            CurrentPercentCompleted = If(expectedLen > 0, CInt(Math.Min(100L, nRead * 100L \ expectedLen)), Math.Min(nRead \ 80000, 100))
                        Loop
                        src.Close()
                    Catch ioex As Exception
                        ' ReadWriteTimeout or a mid-stream close lands here; the outer loop resumes.
                        TL.LogMessage("download", "stream broke at " & nRead & ": " & ioex.Message)
                    End Try
                    theResponse.Close()
                End If

                ' Done when we have the whole known length, or the camera signalled a clean
                ' end and never advertised a length (nothing more is coming).
                If expectedLen > 0 Then
                    complete = (nRead >= expectedLen)
                ElseIf cleanEof Then
                    complete = True
                End If

                If Not complete Then
                    If nRead > posBefore Then
                        noProgress = 0
                    Else
                        noProgress += 1
                        TL.LogMessage("download", "no progress (" & noProgress & "/" & DOWNLOAD_MAX_NOPROGRESS & ") at byte " & nRead)
                        If noProgress >= DOWNLOAD_MAX_NOPROGRESS Then
                            Throw New ASCOM.DriverException("RAW download stalled at " & nRead & " of " & expectedLen & " bytes")
                        End If
                    End If
                    If swWifi.ElapsedMilliseconds > DOWNLOAD_MAX_MS Then
                        Throw New ASCOM.DriverException("RAW download exceeded " & (DOWNLOAD_MAX_MS \ 1000) & "s at " & nRead & " of " & expectedLen & " bytes")
                    End If
                End If
            Loop While Not complete
            TL.LogMessage("WiFi timing", "download " & swWifi.ElapsedMilliseconds & " ms (" & nRead & " bytes, " & attempts & " attempts)")
            swWifi.Restart()

            ' Same decode the USB path uses: RAW goes through libraw_open_buffer and comes
            ' back as pixels, a JPEG through System.Drawing. This route used to be a second,
            ' file-based copy of the same logic - LibRaw writing a ~31 MB TIFF that WPF read
            ' straight back - so WiFi never got the in-memory decode that USB already had.
            ' ReadoutMode 1 is RAW; 0 (jpg) and 2 (thumbnail) are both JPEG.
            ' GetBuffer, not ToArray: ToArray copies the whole ~18.7 MB onto the large
            ' object heap a second time for no reason. The backing array is longer than the
            ' download, hence the explicit count.
            ConvertToTiffFromBuffer(buffer.GetBuffer(), ReadoutMode = 1, nRead)
            TL.LogMessage("WiFi timing", "decode " & swWifi.ElapsedMilliseconds & " ms")

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
        ElseIf HaveDecodedFrame() Then
            CurrentState = CameraStates.cameraIdle
            cameraImageReady = True
            TL.LogMessage("Imageready", "true")
        Else
            ' Conversion failed: don't report ImageReady=True and then throw from
            ' ImageArray - report the error state instead.
            cameraImageReady = False
            CurrentState = CameraStates.cameraError
            TL.LogMessage("Imageready", "False (no image decoded)")
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
        If My.Settings.ConnectionMode.StartsWith("USB") Then
            ' No cam.cgi over USB. Ending the capture also matters for safety: a
            ' disconnect while the SDK still has a capture in flight faults the native
            ' library (an AccessViolation that takes the host process down).
            UsbTransport.AbortCapture()
            Return
        End If
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

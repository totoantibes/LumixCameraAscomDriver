'tabs=4
' --------------------------------------------------------------------------------
' TODO fill in this information for your driver, then remove this line!
'
' ASCOM Camera driver for LumixG80
'
' Description:	Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam 
'				nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam 
'				erat, sed diam voluptua. At vero eos et accusam et justo duo 
'				dolores et ea rebum. Stet clita kasd gubergren, no sea takimata 
'				sanctus est Lorem ipsum dolor sit amet.
'
' Implements:	ASCOM Camera interface version: 1.0
' Author:		robert hasson robert_hasson@yahoo.com
'
' Edit Log:
'
' Date			Who	Vers	Description
' -----------	---	-----	-------------------------------------------------------
' dd-mmm-yyyy	XXX	1.0.0	Initial edit, from Camera template
' ---------------------------------------------------------------------------------
'
'
' Your driver's ID is ASCOM.LumixG80.Camera
'
' The Guid attribute sets the CLSID for ASCOM.DeviceName.Camera
' The ClassInterface/None addribute prevents an empty interface called
' _Camera from being created and used as the [default] interface
'

' This definition is used to select code that's only applicable for one device type
#Const Device = "Camera"

Imports ASCOM
Imports ASCOM.Astrometry
Imports ASCOM.Astrometry.AstroUtils
Imports ASCOM.DeviceInterface
Imports ASCOM.Utilities
Imports UPNPLib
Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Net
Imports System.IO
Imports System.Xml
Imports System.Linq
Imports System.Xml.Linq
Imports System.Windows.Forms.HtmlElementCollection
Imports Microsoft.VisualBasic

<Guid("08832ede-d16d-4090-b661-91f670d95f4d")>
<ClassInterface(ClassInterfaceType.None)>
Public Class Camera

    ' The Guid attribute sets the CLSID for ASCOM.LumixG80.Camera
    ' The ClassInterface/None addribute prevents an empty interface called
    ' _LumixG80 from being created and used as the [default] interface

    ' TODO Replace the not implemented exceptions with code to implement the function or
    ' throw the appropriate ASCOM exception.
    '
    Implements ICameraV2

    '
    ' Driver ID and descriptive string that shows in the Chooser
    '
    Friend Shared driverID As String = "ASCOM.LumixG80.Camera"
    Private Shared driverDescription As String = "LumixG80 Camera"

    Friend Shared comPortProfileName As String = "COM Port" 'Constants used for Profile persistence
    Friend Shared traceStateProfileName As String = "Trace Level"
    Friend Shared comPortDefault As String = "COM1"
    Friend Shared traceStateDefault As String = "False"
    Friend Shared IPAddressDefault As String = "localhost"
    Friend Shared LumixService As UPnPDevice
    '----- Lumix constants ------
    Private MODEL As String = "LUMIX"

    '----- HTTP ------------
    Private USER_AGENT As String = "Mozilla/5.0"

    'Cam Info
    Public Shared STATE As String = "cam.cgi?mode=getstate"

    Private CAPABILITY As String = "cam.cgi?mode=getinfo&type=capability"

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
    Private RECMODE As String = "cam.cgi?mode=camcmd&value=recmode"
    Public Shared PLAYMODE As String = "cam.cgi?mode=camcmd&value=playmode"

    Private SHUTTERSTART As String = "cam.cgi?mode=camcmd&value=capture"

    Private SHUTTERSTOP As String = "cam.cgi?mode=camcmd&value=capture_cancel"

    Private SETAPERTURE As String = "cam.cgi?mode=setsetting&type=focal&value="

    'Focus
    Private FOCUS As String = "cam.cgi?mode=camctrl&type=focus&value="

    Private NEARFAST As String = "wide-fast"

    Private NEAR As String = "wide-normal"

    Private FAR As String = "tele-normal"

    Private FARFAST As String = "tele-fast"

    Private INCREMENT As String = "%2F256"

    'allowed external actions
    Public Enum Actions

        SINGLESHOT

        APERTURE

        SHUTTER

        FOCUS

        ZOOM
    End Enum

    Public Enum Capabilities

        LENS

        CAMERA
    End Enum

    Public Shared ShutterTable =
        {{"3072/256", 4000},
        {"2987/256", 3200},
        {"2902/256", 2500},
        {"2816/256", 2000},
        {"2731/256", 1600},
        {"2646/256", 1300},
        {"2560/256", 1000},
        {"2475/256", 800},
        {"2390/256", 640},
        {"2304/256", 500},
        {"2219/256", 400},
        {"2134/256", 320},
        {"2048/256", 250},
        {"1963/256", 200},
        {"1878/256", 160},
        {"1792/256", 125},
        {"1707/256", 100},
        {"1622/256", 80},
        {"1536/256", 60},
        {"1451/256", 50},
        {"1366/256", 40},
        {"1280/256", 30},
        {"1195/256", 25},
        {"1110/256", 20},
        {"1024/256", 15},
        {"939/256", 13},
        {"854/256", 10},
        {"768/256", 8},
        {"683/256", 6},
        {"598/256", 5},
        {"512/256", 4},
        {"427/256", 3.2},
        {"342/256", 2.5},
        {"256/256", 2},
        {"171/256", 1.6},
        {"86/256", 1.3},
        {"0/256", 1},
        {"-85/256", "1.3s"},
        {"-170/256", "1.6s"},
        {"-256/256", "2s"},
        {"-341/256", "2.5s"},
        {"-426/256", "3.2s"},
        {"-512/256", "4s"},
        {"-682/256", "5s"},
        {"-768/256", "6s"},
        {"-853/256", "8s"},
        {"-938/256", "10s"},
        {"-1024/256", "13s"},
        {"-1109/256", "15s"},
        {"-1194/256", "20s"},
        {"-1280/256", "25s"},
        {"-1365/256", "30s"},
        {"-1450/256", "40s"},
        {"-1536/256", "50s"},
        {"16384/256", "60s"},
        {"256/256", "B"}
}

    Friend Shared comPort As String ' Variables to hold the currrent device configuration
    Friend Shared traceState As Boolean

    Friend Shared IPAddress As String

    Private connectedState As Boolean ' Private variable to hold the connected state
    Private utilities As Util ' Private variable to hold an ASCOM Utilities object
    Private astroUtilities As AstroUtils ' Private variable to hold an AstroUtils object to provide the Range method
    Private TL As TraceLogger ' Private variable to hold the trace logger object (creates a diagnostic log file with information that you specify)

    '
    ' Constructor - Must be public for COM registration!
    '
    ''' <summary>
    ''' 
    ''' </summary>
    Public Sub New()

        ReadProfile() ' Read device configuration from the ASCOM Profile store
        TL = New TraceLogger("", "LumixG80")
        TL.Enabled = traceState
        TL.LogMessage("Camera", "Starting initialisation")

        connectedState = False ' Initialise connected to false
        utilities = New Util() ' Initialise util object
        astroUtilities = New AstroUtils 'Initialise new astro utiliites object

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
        If IsConnected Then
            System.Windows.Forms.MessageBox.Show("Already connected, just press OK")
        End If

        Using F As SetupDialogForm = New SetupDialogForm()
            Dim result As System.Windows.Forms.DialogResult = F.ShowDialog()
            If result = DialogResult.OK Then
                WriteProfile() ' Persist device configuration values to the ASCOM Profile store
            End If
        End Using
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

    Public Property Connected() As Boolean Implements ICameraV2.Connected
        Get
            TL.LogMessage("Connected Get", IsConnected.ToString())
            Return IsConnected
        End Get
        Set(value As Boolean)
            TL.LogMessage("Connected Set", value.ToString())
            If value = IsConnected Then
                Return
            End If

            If value Then
                connectedState = True
                TL.LogMessage("Connected Set", "Connecting to IP Address " + IPAddress)
                ' TODO connect to the device

            Else
                connectedState = False
                TL.LogMessage("Connected Set", "Disconnecting from IP Address " + IPAddress)
                ' TODO disconnect from the device
            End If
        End Set
    End Property

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
            Dim s_driverInfo As String = "Information about the driver itself. Version: " + m_version.Major.ToString() + "." + m_version.Minor.ToString()
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
            Dim s_name As String = "Short driver name - please customise"
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

    Private Const ccdWidth As Integer = 4592 ' Constants to define the ccd pixel dimenstions
    Private Const ccdHeight As Integer = 3064
    Private Const pixelSize As Double = 3.75 ' Constant for the pixel physical dimension

    Private cameraNumX As Integer = ccdWidth ' Initialise variables to hold values required for functionality tested by Conform
    Private cameraNumY As Integer = ccdHeight
    Private cameraStartX As Integer = 0
    Private cameraStartY As Integer = 0
    Private exposureStart As DateTime = DateTime.MinValue
    Private cameraLastExposureDuration As Double = 0.0
    Private cameraImageReady As Boolean = False
    Private cameraImageArray As Integer(,)
    Private cameraImageArrayVariant As Object(,)

    Public Sub AbortExposure() Implements ICameraV2.AbortExposure
        StopExposure()
        TL.LogMessage("AbortExposure", "Exposure Aborted")
        'Throw New MethodNotImplementedException("AbortExposure")
    End Sub

    Public ReadOnly Property BayerOffsetX() As Short Implements ICameraV2.BayerOffsetX
        Get
            TL.LogMessage("BayerOffsetX Get", "Not implemented")
            Throw New PropertyNotImplementedException("BayerOffsetX", False)
        End Get
    End Property

    Public ReadOnly Property BayerOffsetY() As Short Implements ICameraV2.BayerOffsetY
        Get
            TL.LogMessage("BayerOffsetY Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("BayerOffsetY", False)
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
            TL.LogMessage("CCDTemperature Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("CCDTemperature", False)
        End Get
    End Property

    Public ReadOnly Property CameraState() As CameraStates Implements ICameraV2.CameraState
        Get
            TL.LogMessage("CameraState Get", CameraStates.cameraIdle.ToString())
            Return CameraStates.cameraIdle
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
            TL.LogMessage("ExposureMax Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ExposureMax", False)
        End Get
    End Property

    Public ReadOnly Property ExposureMin() As Double Implements ICameraV2.ExposureMin
        Get
            TL.LogMessage("ExposureMin Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ExposureMin", False)
        End Get
    End Property

    Public ReadOnly Property ExposureResolution() As Double Implements ICameraV2.ExposureResolution
        Get
            TL.LogMessage("ExposureResolution Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ExposureResolution", False)
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
            TL.LogMessage("Gain Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("Gain", False)
        End Get
        Set(value As Short)
            TL.LogMessage("Gain Set", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("Gain", True)
        End Set
    End Property

    Public ReadOnly Property GainMax() As Short Implements ICameraV2.GainMax
        Get
            TL.LogMessage("GainMax Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("GainMax", False)
        End Get
    End Property

    Public ReadOnly Property GainMin() As Short Implements ICameraV2.GainMin
        Get
            TL.LogMessage("GainMin Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("GainMin", False)
        End Get
    End Property

    Public ReadOnly Property Gains() As ArrayList Implements ICameraV2.Gains
        Get
            TL.LogMessage("Gains Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("Gains", False)
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

    Public ReadOnly Property ImageArray() As Object Implements ICameraV2.ImageArray
        Get
            If (Not cameraImageReady) Then
                TL.LogMessage("ImageArray Get", "Throwing InvalidOperationException because of a call to ImageArray before the first image has been taken!")
                Throw New ASCOM.InvalidOperationException("Call to ImageArray before the first image has been taken!")
            End If

            ReDim cameraImageArray(cameraNumX - 1, cameraNumY - 1)

        End Get
    End Property

    Public ReadOnly Property ImageArrayVariant() As Object Implements ICameraV2.ImageArrayVariant
        Get
            If (Not cameraImageReady) Then
                TL.LogMessage("ImageArrayVariant Get", "Throwing InvalidOperationException because of a call to ImageArrayVariant before the first image has been taken!")
                Throw New ASCOM.InvalidOperationException("Call to ImageArrayVariant before the first image has been taken!")
            End If

            ReDim cameraImageArrayVariant(cameraNumX - 1, cameraNumY - 1)
            For i As Integer = 0 To cameraImageArray.GetLength(1) - 1
                For j As Integer = 0 To cameraImageArray.GetLength(0) - 1
                    cameraImageArrayVariant(j, i) = cameraImageArray(j, i)
                Next
            Next

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
            TL.LogMessage("IsPulseGuiding Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("IsPulseGuiding", False)
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
            TL.LogMessage("MaxADU Get", "20000")
            Return 20000
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
            TL.LogMessage("PercentCompleted Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("PercentCompleted", False)
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
            TL.LogMessage("ReadoutMode Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ReadoutMode", False)
        End Get
        Set(value As Short)
            TL.LogMessage("ReadoutMode Set", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ReadoutMode", True)
        End Set
    End Property

    Public ReadOnly Property ReadoutModes() As ArrayList Implements ICameraV2.ReadoutModes
        Get
            TL.LogMessage("ReadoutModes Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ReadoutModes", False)
        End Get
    End Property

    Public ReadOnly Property SensorName() As String Implements ICameraV2.SensorName
        Get
            TL.LogMessage("SensorName Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("SensorName", False)
        End Get
    End Property

    Public ReadOnly Property SensorType() As SensorType Implements ICameraV2.SensorType
        Get
            TL.LogMessage("SensorType Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("SensorType", False)
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

    Public Shared Function NumberPix() As String
        Dim response As String = SendLumixMessage(NUMPIX)
        Dim doc As XElement = XElement.Parse(response)
        Return doc...<total_content_number>.Value
    End Function

    Public Shared Function GetPix(num As Int16) As String
        SendLumixMessage(PLAYMODE)
        Dim Start As Int16 = 0
        Dim NumPix As String = NumberPix()
        Dim SoapMsg As String = SoapEnvelop(Math.Max(NumPix - num, 0), NumPix)
        Dim Stream As System.IO.StreamWriter
        Dim HTTPReq As HttpWebRequest
        'Dim XEmpty As XDocument = XDocument.Parse("")
        HTTPReq = WebRequest.Create("http://" + Camera.IPAddress + ":60606/Server0/CDS_control")
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
                'Return ResponseText.Replace("&amp;", "&").Replace("&apos;", "'").Replace("&quot;", """""").Replace("&lt;", "<").Replace("&gt;", ">")
                Dim answer As String = ResponseText.Replace("&amp;", "&").Replace("&apos;", "'").Replace("&quot;", """").Replace("&lt;", "<").Replace("&gt;", ">")
                Return answer
                'Return XMLResponse
            Else
                Return Nothing
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
                            Return Nothing
                        End Using
                    Catch ex As Exception
                        'TL.LogMessage("Message" + LumixMessage + " Sent Failed", LumixMessage + " failed")
                    End Try
                End Using
            End If
        End Try
        'Return Nothing
    End Function

    Public Shared Function SendLumixMessage(LumixMessage As String) As String
        Dim request = WebRequest.Create("http://" + Camera.IPAddress + "/" + LumixMessage)
        Dim myStreamReader As StreamReader
        Dim SendStatus As Integer = -1
        Dim statusCode As HttpStatusCode
        Dim ResponseText As String
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
    End Function


    Public Sub StartExposure(Duration As Double, Light As Boolean) Implements ICameraV2.StartExposure
        If (Duration < 0.0) Then Throw New InvalidValueException("StartExposure", Duration.ToString(), "0.0 upwards")
        If (cameraNumX > ccdWidth) Then Throw New InvalidValueException("StartExposure", cameraNumX.ToString(), ccdWidth.ToString())
        If (cameraNumY > ccdHeight) Then Throw New InvalidValueException("StartExposure", cameraNumY.ToString(), ccdHeight.ToString())
        If (cameraStartX > ccdWidth) Then Throw New InvalidValueException("StartExposure", cameraStartX.ToString(), ccdWidth.ToString())
        If (cameraStartY > ccdHeight) Then Throw New InvalidValueException("StartExposure", cameraStartY.ToString(), ccdHeight.ToString())

        cameraLastExposureDuration = Duration
        exposureStart = DateTime.Now
        SendLumixMessage(SHUTTERSTART)
        ' System.Threading.Thread.Sleep(Duration * 1000) ' Sleep for the duration to simulate exposure 
        TL.LogMessage("StartExposure", Duration.ToString() + " " + Light.ToString())
        cameraImageReady = True
        StopExposure()


        Dim Pictures As XmlDocument                     'the XML with all the results from the camea
        Dim PictureList As XmlNodeList                  'the list of picture items extratec
        Dim Picture As XmlNode                          'a singlepicture iteme
        Dim Images(250) As String 'the array of the urls in the camera
        Dim nRead(250) As Integer
        Dim j = -1
        Dim SendStatus As Integer = -1
        Dim statusCode As HttpStatusCode
        Dim length As Integer = 0

        Pictures = New XmlDocument
        Pictures.LoadXml(GetPix(10))
        PictureList = Pictures.LastChild.FirstChild.FirstChild.FirstChild.FirstChild.ChildNodes 'items
        SendLumixMessage(PLAYMODE)                      'making sure the camera is in Playmode
        For Each Picture In PictureList
            If Picture.ChildNodes(3).InnerText.Contains("RAW") Then
                'If Picture.LastChild.FirstChild.InnerText.Contains("RW2") Then (not sure why it wsitched from RW2 to RAW)
                'If Picture.ChildNodes(3).InnerText.Contains(".JPG") And Picture.ChildNodes(3).InnerText.Contains("DO") Then
                SendLumixMessage(PLAYMODE)
                j = j + 1
                Images(j) = Picture.ChildNodes(3).InnerText
                nRead(j) = 0
                'Images(j) = Picture.LastChild.FirstChild.InnerText
                ' Console.WriteLine("JPG found " & j.ToString)
                Dim theResponse As HttpWebResponse
                Dim theRequest As HttpWebRequest
                Dim bytesread As Integer
                Do
                    theRequest = WebRequest.Create(Images(j))
                    TL.LogMessage("reading stream ", Images(j) & " position " & nRead(j))
                    theRequest.KeepAlive = False
                    theRequest.ProtocolVersion = HttpVersion.Version10
                    theRequest.ServicePoint.ConnectionLimit = 1
                    If nRead(j) > 0 Then
                        theRequest.AddRange(nRead(j))
                    End If

                    Try 'Checks if the file exist
                        theResponse = theRequest.GetResponse()

                    Catch ex As Exception

                        MessageBox.Show("An error occurred while downloading file  " & Images(j) & "   " & nRead(j) & ". _
			Possible causes:" & ControlChars.CrLf &
                            "1) File doesn't exist" & ControlChars.CrLf &
                            "2) Remote server error", "Error",
            MessageBoxButtons.OK, MessageBoxIcon.Error)

                        TL.LogMessage("error in reading stream ", Images(j) & " position " & nRead(j))
                        Exit Do

                    End Try
                    'length = theResponse.ContentLength 'Size of the response (in bytes)
                    'TL.LogMessage("length of the response is ", Images(j) & " is " & length)

                    Dim writeStream As New IO.FileStream("C:\Users\robert.hasson\Documents\XMLLumix\" & Images(j).Substring(Images(j).Length - 13), IO.FileMode.OpenOrCreate)

                    'Replacement for Stream.Position (webResponse stream doesn't support seek)

                    Try
                        Do
                            Dim buflen As Integer = 4096
                            Dim readBytes(buflen - 1) As Byte
                            bytesread = theResponse.GetResponseStream.Read(readBytes, 0, buflen)

                            nRead(j) += bytesread
                            If bytesread = 0 Then
                                Exit Do
                            End If
                            writeStream.Write(readBytes, 0, bytesread)

                        Loop

                        'Close the streams
                        theResponse.GetResponseStream.Close()
                        writeStream.Close()
                    Catch e As System.IO.IOException
                        TL.LogMessage("camera stopped streaming  ", Images(j) & " position  " & nRead(j))
                    End Try
                Loop While bytesread > 0
                'theResponse.GetResponseStream.Close()

                'Try
                '    getimageHttp = WebRequest.Create(Images(j))
                '    getimageHttp.KeepAlive = True

                '    getimageHttp.ProtocolVersion = HttpVersion.Version11
                '    getimageHttp.ServicePoint.ConnectionLimit = 1
                '    Dim resp = CType(getimageHttp.GetResponse(), HttpWebResponse)
                '    Try
                '        Dim ImageListStreamer As Stream = resp.GetResponseStream
                '        Dim fs As New FileStream("C:\Users\robert.hasson\Documents\XMLLumix\" & Images(j).Substring(Images(j).Length - 13), FileMode.Create)
                '        ImageListStreamer.CopyTo(fs)
                '        fs.Flush()
                '        fs.Close()
                '    Catch e As System.IO.IOException
                '        TL.LogMessage("camera stopped streaming  ", Images(j))
                '    End Try

                'Catch e As WebException
                '        If (e.Status = WebExceptionStatus.ProtocolError) Then
                '        Dim response As WebResponse = e.Response
                '        Using (response)
                '            Dim httpResponse As HttpWebResponse = CType(response, HttpWebResponse)
                '            statusCode = httpResponse.StatusCode
                '        End Using
                '    End If
                'End Try
            End If
        Next



    End Sub

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

    ''' <summary>
    ''' Read the device configuration from the ASCOM Profile store
    ''' </summary>
    Friend Sub ReadProfile()
        Using driverProfile As New Profile()
            driverProfile.DeviceType = "Camera"
            traceState = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, String.Empty, traceStateDefault))
            comPort = driverProfile.GetValue(driverID, comPortProfileName, String.Empty, comPortDefault)
        End Using
    End Sub

    ''' <summary>
    ''' Write the device configuration to the  ASCOM  Profile store
    ''' </summary>
    Friend Sub WriteProfile()
        Using driverProfile As New Profile()
            driverProfile.DeviceType = "Camera"
            driverProfile.WriteValue(driverID, traceStateProfileName, traceState.ToString())
            driverProfile.WriteValue(driverID, comPortProfileName, comPort.ToString())
        End Using

    End Sub

#End Region



End Class

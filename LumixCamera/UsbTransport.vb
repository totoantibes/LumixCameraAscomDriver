Imports System.IO
Imports System.Reflection
Imports ASCOM.Lumix.Usb

''' <summary>
''' Thin VB wrapper over the ASCOM.Lumix.Usb library — the driver's USB transport.
''' Resolves the bundled public Lmxptpif.dll, opens device 0, and captures to a file
''' the driver then feeds through its normal RAW/JPG -> TIFF -> ImageArray path.
''' Standard (public SDK) mode only for now; Extended/Tether is a later phase.
''' </summary>
Public Class UsbTransport
    Private Shared _cam As UsbCamera

    Public Shared ReadOnly Property ModelName As String
        Get
            Return If(_cam IsNot Nothing, _cam.ModelName, "")
        End Get
    End Property

    Public Shared ReadOnly Property IsConnected As Boolean
        Get
            Return _cam IsNot Nothing AndAlso _cam.IsConnected
        End Get
    End Property

    Public Shared ReadOnly Property IsExtended As Boolean
        Get
            Return _cam IsNot Nothing AndAlso _cam.IsExtended
        End Get
    End Property

    ''' <summary>
    ''' Initialise the SDK and open device 0. Standard uses the bundled public DLL;
    ''' Extended loads the user's installed LUMIX Tether DLL (bulb / &gt;60 s).
    ''' </summary>
    Public Shared Sub Connect(extended As Boolean)
        Dim dll As String
        If extended Then
            dll = TetherDllPath()
        Else
            Dim dir As String = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            Dim arch As String = If(IntPtr.Size = 8, "x64", "x86")
            dll = Path.Combine(dir, "native", arch, "Lmxptpif.dll")
            If Not File.Exists(dll) Then
                Dim alt As String = Path.Combine(dir, "Lmxptpif.dll")
                If File.Exists(alt) Then dll = alt
            End If
        End If
        LumixUsb.Initialize(dll, extended)
        _cam = UsbCamera.Open(0)
    End Sub

    ''' <summary>Default LUMIX Tether install path (never bundled — the user installs it).</summary>
    Private Shared Function TetherDllPath() As String
        Return "C:\Program Files\Panasonic\LUMIX Tether\Lmxptpif.dll"
    End Function

    Public Shared Sub Disconnect()
        If _cam IsNot Nothing Then
            Try
                _cam.Disconnect()
                _cam.Dispose()
            Catch
            End Try
            _cam = Nothing
        End If
    End Sub

    ''' <summary>Fire a one-shot capture and write the image to <paramref name="dir"/>.</summary>
    Public Shared Function Capture(dir As String, timeoutMs As Integer) As CaptureResult
        If _cam Is Nothing Then Return New CaptureResult With {.Success = False, .Error = "not connected"}
        Return _cam.CaptureOneShot(dir, timeoutMs)
    End Function

    ''' <summary>Bulb exposure of <paramref name="seconds"/> (Extended only; supports &gt;60 s).</summary>
    Public Shared Function CaptureBulb(dir As String, seconds As Double, timeoutMs As Integer) As CaptureResult
        If _cam Is Nothing Then Return New CaptureResult With {.Success = False, .Error = "not connected"}
        Return _cam.CaptureBulb(dir, seconds, timeoutMs)
    End Function

    ''' <summary>Snap the requested exposure to the nearest supported shutter speed and set it. Returns the actual seconds.</summary>
    Public Shared Function SetShutterSeconds(seconds As Double) As Double
        If _cam Is Nothing Then Return seconds
        Dim actual As Double = seconds
        Dim raw As Long = _cam.NearestShutterRaw(seconds, actual)
        If raw <> 0 Then _cam.SetShutter(raw)
        Return actual
    End Function

    ''' <summary>Number of supported ISO values (ASCOM gain list size).</summary>
    Public Shared ReadOnly Property IsoCount As Integer
        Get
            Return If(_cam IsNot Nothing, _cam.IsoValues.Count, 0)
        End Get
    End Property

    ''' <summary>Set ISO by ASCOM gain index into the camera's supported ISO list.</summary>
    Public Shared Sub SetIsoIndex(index As Integer)
        If _cam Is Nothing Then Return
        If index >= 0 AndAlso index < _cam.IsoValues.Count Then _cam.SetIso(_cam.IsoValues(index))
    End Sub

    ''' <summary>
    ''' Minimal sensor spec by USB model name (DC- prefixed, as the USB SDK reports it).
    ''' Returns full-res width/height and pixel pitch (µm); DefaultLumix otherwise.
    ''' </summary>
    Public Shared Sub GetSpecs(model As String, ByRef width As Integer, ByRef height As Integer, ByRef pitch As Double)
        Select Case model
            Case "DC-GH5S", "DC-BGH1" : width = 3680 : height = 2760 : pitch = 4.68
            Case "DC-GH5", "DC-GH5M2", "DC-G9", "DC-G9M2" : width = 5184 : height = 3888 : pitch = 3.33
            Case "DC-GH6", "DC-GH7" : width = 5776 : height = 4336 : pitch = 3.0
            Case "DC-S1R" : width = 8368 : height = 5584 : pitch = 4.3
            Case "DC-S1RII" : width = 8144 : height = 5434 : pitch = 4.39
            Case "DC-S9" : width = 8192 : height = 5464 : pitch = 2.11
            Case "DC-S1", "DC-S1H", "DC-S5", "DC-S5M2", "DC-S5M2X", "DC-BGS1" : width = 6000 : height = 4000 : pitch = 5.94
            Case Else : width = 6000 : height = 4000 : pitch = 5.9 ' DefaultLumix
        End Select
    End Sub
End Class

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
        ' The Lumix USB SDK holds ONE session per process, and every Camera instance
        ' shares this transport. A second Connect must therefore reuse the open session
        ' rather than open another - opening a second one fails with
        ' "OpenSession failed (err 0x00000000)".
        ' This happens in normal use: the setup dialog connects when the user presses OK,
        ' and then the client (NINA) connects its own Camera instance a moment later.
        If _cam IsNot Nothing AndAlso _cam.IsConnected Then Return

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
    Public Shared Function TetherDllPath() As String
        Return "C:\Program Files\Panasonic\LUMIX Tether\Lmxptpif.dll"
    End Function

    ''' <summary>True if the user's LUMIX Tether DLL is installed (enables Extended/bulb).</summary>
    Public Shared Function IsTetherInstalled() As Boolean
        Return File.Exists(TetherDllPath())
    End Function

    ''' <summary>
    ''' True if a Panasonic USB device is currently present. Lumix bodies in PTP mode
    ''' enumerate under vendor id 04DA. Detection is SDK-free (WMI) so it does not lock
    ''' the process to a Lmxptpif.dll before the real connect chooses public vs Tether.
    ''' </summary>
    Public Shared Function IsUsbCameraPresent() As Boolean
        Try
            Using searcher As New System.Management.ManagementObjectSearcher(
                "SELECT DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_04DA%'")
                For Each mo As System.Management.ManagementObject In searcher.Get()
                    Return True
                Next
            End Using
        Catch
        End Try
        Return False
    End Function

    ''' <summary>
    ''' Model name of the cabled camera ("DC-GH5S"), or "" if none. Read from WMI rather
    ''' than the SDK on purpose: the setup dialog needs the model before anything is
    ''' connected, and loading/initialising Lmxptpif here would pin the process to one
    ''' DLL and ABI before the user has chosen Standard or Extended.
    ''' </summary>
    Public Shared Function UsbCameraModel() As String
        Try
            Using searcher As New System.Management.ManagementObjectSearcher(
                "SELECT Name FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_04DA%'")
                For Each mo As System.Management.ManagementObject In searcher.Get()
                    Dim n As Object = mo("Name")
                    If n IsNot Nothing AndAlso Not String.IsNullOrEmpty(n.ToString()) Then Return n.ToString()
                Next
            End Using
        Catch
        End Try
        Return ""
    End Function

    ''' <summary>End any capture in progress (ASCOM AbortExposure/StopExposure).</summary>
    Public Shared Sub AbortCapture()
        If _cam IsNot Nothing Then
            Try
                _cam.RequestAbort()
            Catch
            End Try
        End If
    End Sub

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

    ' ---- live view ----
    Public Shared Function StartLiveView() As Boolean
        Return _cam IsNot Nothing AndAlso _cam.StartLiveView()
    End Function
    Public Shared Sub StopLiveView()
        If _cam IsNot Nothing Then _cam.StopLiveView()
    End Sub
    ''' <summary>One live-view JPEG frame, or Nothing.</summary>
    Public Shared Function GetLiveViewFrame() As Byte()
        Return If(_cam IsNot Nothing, _cam.GetLiveViewJpeg(), Nothing)
    End Function

    ''' <summary>Supported shutter speeds formatted for display (e.g. "4s", "1/125").</summary>
    Public Shared Function ShutterDisplay() As String()
        If _cam Is Nothing OrElse _cam.ShutterSeconds.Count = 0 Then Return New String() {}
        Dim n As Integer = _cam.ShutterSeconds.Count
        Dim a(n - 1) As String
        For i As Integer = 0 To n - 1
            a(i) = FormatSeconds(_cam.ShutterSeconds(i))
        Next
        Return a
    End Function
    Public Shared Sub SetShutterByIndex(index As Integer)
        If _cam IsNot Nothing Then _cam.SetShutterByIndex(index)
    End Sub

    ''' <summary>Supported ISO values as plain numbers ("160", "51200"), in SetIsoIndex order.</summary>
    Public Shared Function IsoDisplay() As String()
        If _cam Is Nothing OrElse _cam.IsoNumbers.Count = 0 Then Return New String() {}
        Dim n As Integer = _cam.IsoNumbers.Count
        Dim a(n - 1) As String
        For i As Integer = 0 To n - 1
            a(i) = _cam.IsoNumbers(i).ToString()
        Next
        Return a
    End Function

    Private Shared Function FormatSeconds(s As Double) As String
        If s >= 1 Then Return s.ToString("0.###") & "s"
        Return "1/" & CInt(Math.Round(1.0 / s)).ToString()
    End Function

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

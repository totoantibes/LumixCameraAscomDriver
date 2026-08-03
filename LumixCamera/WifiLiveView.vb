Imports System.Net
Imports System.Net.Sockets
Imports System.Threading

''' <summary>
''' Live view over WiFi. The camera streams MJPEG over UDP: ask it to start with
''' cam.cgi?mode=startstream&amp;value=&lt;port&gt; and it sends one complete JPEG per
''' datagram to the requesting host, prefixed by a Panasonic header (264 bytes on a
''' GH5S, ~26 KB frames at ~27 fps). cam.cgi?mode=stopstream ends it.
'''
''' The USB path gets its frames from the Lumix SDK instead (UsbTransport); this class
''' is the WiFi equivalent, so LiveViewForm can offer live view on both transports.
'''
''' Note the header length is NOT assumed: each datagram is scanned for the JPEG SOI
''' marker, so a body that uses a different header size still works.
''' </summary>
Public Class WifiLiveView

    Public Const DefaultPort As Integer = 49199

    ''' <summary>
    ''' The stream sizes the camera offers. Verified on a GH5S: "vga" gives 640x480
    ''' (~26 KB/frame) and "qvga" gives 320x240 (~13 KB/frame); anything else is
    ''' rejected with err_param. QVGA halves the bandwidth, which helps on a weak link.
    ''' </summary>
    Public Shared ReadOnly Sizes As String() = {"vga", "qvga"}

    Private Const LIVEVIEWSIZE As String = "cam.cgi?mode=setsetting&type=liveviewsize&value="

    ''' <summary>
    ''' Ask the camera for a stream size. NOTE the camera answers ok but keeps sending
    ''' the old size if it is already streaming - measured on a GH5S, frame sizes were
    ''' unchanged across a mid-stream switch. Use <see cref="ChangeSize"/> while running.
    ''' </summary>
    Public Shared Function SetSize(size As String) As Boolean
        If String.IsNullOrEmpty(size) Then Return False
        Dim reply As String = Camera.SendLumixMessage(LIVEVIEWSIZE & size)
        Return reply IsNot Nothing AndAlso reply.Contains("ok")
    End Function

    ''' <summary>
    ''' Switch stream size, restarting the stream so the camera actually applies it.
    ''' Setting it alone is accepted but ignored until the stream restarts.
    ''' </summary>
    Public Function ChangeSize(size As String) As Boolean
        Dim wasRunning As Boolean = _running
        If wasRunning Then [Stop]()
        If Not SetSize(size) Then
            If wasRunning Then Start(_port)
            Return False
        End If
        If wasRunning Then Return Start(_port)
        Return True
    End Function

    ''' <summary>The size the camera currently reports, or "" if it cannot be read.</summary>
    Public Shared Function GetSize() As String
        Dim reply As String = Camera.SendLumixMessage("cam.cgi?mode=getsetting&type=liveviewsize")
        If reply Is Nothing Then Return ""
        Dim marker As String = "liveviewsize="""
        Dim i As Integer = reply.IndexOf(marker, StringComparison.Ordinal)
        If i < 0 Then Return ""
        i += marker.Length
        Dim j As Integer = reply.IndexOf(""""c, i)
        If j < 0 Then Return ""
        Return reply.Substring(i, j - i)
    End Function

    Private _udp As UdpClient
    Private _receiver As Thread
    Private _running As Boolean
    Private _latest As Byte()
    Private ReadOnly _gate As New Object()
    Private _port As Integer
    Private _frames As Long

    ''' <summary>Frames received since Start (a stalled stream shows as a static count).</summary>
    Public ReadOnly Property FrameCount As Long
        Get
            Return Interlocked.Read(_frames)
        End Get
    End Property

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return _running
        End Get
    End Property

    ''' <summary>
    ''' Open the UDP socket and ask the camera to stream to it. Returns False if the
    ''' socket cannot be opened or the camera refuses.
    ''' </summary>
    Public Function Start(Optional port As Integer = DefaultPort) As Boolean
        If _running Then Return True
        If String.IsNullOrEmpty(Camera.IPAddress) Then Return False

        _port = port
        Try
            _udp = New UdpClient(_port)
            _udp.Client.ReceiveTimeout = 2000
        Catch ex As SocketException
            ' Port busy: let the OS pick one and tell the camera about that instead.
            Try
                _udp = New UdpClient(0)
                _udp.Client.ReceiveTimeout = 2000
                _port = CType(_udp.Client.LocalEndPoint, IPEndPoint).Port
            Catch
                Return False
            End Try
        End Try

        ' Live view only streams from rec mode; a preceding capture leaves the camera
        ' in playmode.
        Camera.SendLumixMessage("cam.cgi?mode=camcmd&value=recmode")
        Dim reply As String = Camera.SendLumixMessage("cam.cgi?mode=startstream&value=" & _port.ToString())
        If reply Is Nothing OrElse Not reply.Contains("ok") Then
            CloseSocket()
            Return False
        End If

        _running = True
        Interlocked.Exchange(_frames, 0)
        _receiver = New Thread(AddressOf ReceiveLoop) With {.IsBackground = True, .Name = "LumixWifiLiveView"}
        _receiver.Start()
        Return True
    End Function

    Public Sub [Stop]()
        If Not _running Then Return
        _running = False
        Try
            Camera.SendLumixMessage("cam.cgi?mode=stopstream")
        Catch
        End Try
        Try
            If _receiver IsNot Nothing Then _receiver.Join(2500)
        Catch
        End Try
        CloseSocket()
        SyncLock _gate
            _latest = Nothing
        End SyncLock
    End Sub

    ''' <summary>The most recent frame as JPEG bytes, or Nothing if none has arrived.</summary>
    Public Function GetFrame() As Byte()
        SyncLock _gate
            Return _latest
        End SyncLock
    End Function

    Private Sub ReceiveLoop()
        Dim ep As New IPEndPoint(IPAddress.Any, 0)
        While _running
            Dim datagram As Byte()
            Try
                datagram = _udp.Receive(ep)
            Catch ex As SocketException
                Continue While   ' receive timeout: just keep waiting while running
            Catch
                Exit While       ' socket closed under us by Stop()
            End Try

            Dim soi As Integer = IndexOfJpegStart(datagram)
            If soi < 0 Then Continue While

            Dim frame(datagram.Length - soi - 1) As Byte
            Array.Copy(datagram, soi, frame, 0, frame.Length)
            SyncLock _gate
                _latest = frame
            End SyncLock
            Interlocked.Increment(_frames)
        End While
    End Sub

    ''' <summary>Offset of the JPEG SOI (FF D8 FF), or -1. Not a fixed header size.</summary>
    Private Shared Function IndexOfJpegStart(b As Byte()) As Integer
        If b Is Nothing Then Return -1
        For i As Integer = 0 To b.Length - 3
            If b(i) = &HFF AndAlso b(i + 1) = &HD8 AndAlso b(i + 2) = &HFF Then Return i
        Next
        Return -1
    End Function

    Private Sub CloseSocket()
        Try
            If _udp IsNot Nothing Then _udp.Close()
        Catch
        End Try
        _udp = Nothing
    End Sub
End Class

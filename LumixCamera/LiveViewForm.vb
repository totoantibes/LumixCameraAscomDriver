Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

''' <summary>
''' USB live-view preview with shutter/ISO controls. Opened from the setup dialog in
''' a USB mode. Connects the camera if needed (and disconnects on close if it opened
''' the connection), streams JPEG frames, and applies shutter/ISO changes live — a
''' framing/focus aid (ASCOM itself has no live-view surface).
''' </summary>
Public Class LiveViewForm
    Inherits Form

    Private ReadOnly _pic As PictureBox
    Private ReadOnly _cbShutter As ComboBox
    Private ReadOnly _cbIso As ComboBox
    Private ReadOnly _tmr As Timer
    Private ReadOnly _ownsConnection As Boolean
    Private _busy As Boolean
    Private ReadOnly _wifi As Boolean
    Private _wifiLv As WifiLiveView
    Private _cbSize As ComboBox
    Private _lblInfo As Label
    Private _framesSeen As Integer
    Private _lastInfo As DateTime = DateTime.MinValue
    Private _lastCount As Integer

    ''' <summary>
    ''' <paramref name="extended"/> selects the Tether ABI on USB. <paramref name="wifi"/>
    ''' switches the whole form to the WiFi transport (UDP MJPEG) instead of the USB SDK.
    ''' </summary>
    Public Sub New(extended As Boolean, Optional wifi As Boolean = False)
        _wifi = wifi
        Me.Text = "Lumix Live View"
        Me.ClientSize = New Size(700, 560)
        Me.StartPosition = FormStartPosition.CenterParent
        Me.MinimizeBox = False

        _pic = New PictureBox With {
            .Location = New Point(8, 8), .Size = New Size(684, 500),
            .SizeMode = PictureBoxSizeMode.Zoom, .BorderStyle = BorderStyle.FixedSingle,
            .BackColor = Color.Black,
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom}
        Me.Controls.Add(_pic)

        Me.Controls.Add(New Label With {.Text = "Shutter:", .Location = New Point(8, 522), .AutoSize = True, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left})
        _cbShutter = New ComboBox With {.Location = New Point(64, 518), .Size = New Size(120, 24), .DropDownStyle = ComboBoxStyle.DropDownList, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
        Me.Controls.Add(_cbShutter)
        Me.Controls.Add(New Label With {.Text = "ISO:", .Location = New Point(200, 522), .AutoSize = True, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left})
        _cbIso = New ComboBox With {.Location = New Point(238, 518), .Size = New Size(110, 24), .DropDownStyle = ComboBoxStyle.DropDownList, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
        Me.Controls.Add(_cbIso)

        ' The PictureBox zooms every frame to the same area, so a 320x240 stream looks
        ' identical to 640x480 apart from being softer - report the real frame size (and
        ' rate) so a size change is actually visible.
        _lblInfo = New Label With {.Location = New Point(500, 522), .AutoSize = True,
                                   .ForeColor = Color.DimGray,
                                   .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right}
        Me.Controls.Add(_lblInfo)

        Dim btnClose As New Button With {.Text = "Close", .Location = New Point(612, 517), .Size = New Size(80, 26), .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right}
        AddHandler btnClose.Click, Sub(s, e) Me.Close()
        Me.Controls.Add(btnClose)

        If _wifi Then
            ' WiFi: the camera streams MJPEG over UDP; no session to open, but it does
            ' need an IP, which only exists once the setup dialog has one selected.
            If String.IsNullOrEmpty(Camera.IPAddress) Then
                MessageBox.Show("No camera IP address selected yet.", "Live View")
            End If
            For i As Integer = 0 To Camera.ShutterTable.GetLength(0) - 1
                _cbShutter.Items.Add(Camera.ShutterTable(i, 1))
            Next
            For Each isoValue As String In Camera.ISOTable
                Dim numericIso As Integer
                If Integer.TryParse(isoValue, numericIso) Then _cbIso.Items.Add(isoValue)
            Next
            AddHandler _cbShutter.SelectedIndexChanged,
                Sub(s, e) Camera.SendLumixMessage(Camera.SHUTTERSPEED & Camera.ShutterTable(_cbShutter.SelectedIndex, 0))
            AddHandler _cbIso.SelectedIndexChanged,
                Sub(s, e) Camera.SendLumixMessage(Camera.ISO & _cbIso.SelectedItem.ToString())

            ' Stream-size selector: the camera offers VGA and QVGA, switchable while
            ' streaming. QVGA halves the bandwidth on a weak link.
            Me.Controls.Add(New Label With {.Text = "Size:", .Location = New Point(364, 522), .AutoSize = True,
                                            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left})
            _cbSize = New ComboBox With {.Location = New Point(404, 518), .Size = New Size(90, 24),
                                         .DropDownStyle = ComboBoxStyle.DropDownList,
                                         .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
            _cbSize.Items.AddRange(WifiLiveView.Sizes)
            Dim current As String = WifiLiveView.GetSize()
            _cbSize.SelectedIndex = Math.Max(0, _cbSize.FindStringExact(current))
            ' Restart the stream around the change: the camera accepts a size change
            ' while streaming but goes on sending the old size until it restarts.
            AddHandler _cbSize.SelectedIndexChanged,
                Sub(s, e)
                    If _wifiLv Is Nothing Then Return
                    _busy = True
                    Try
                        _wifiLv.ChangeSize(_cbSize.SelectedItem.ToString())
                    Finally
                        _busy = False
                    End Try
                End Sub
            Me.Controls.Add(_cbSize)

            _wifiLv = New WifiLiveView()
            If Not _wifiLv.Start() Then
                MessageBox.Show("The camera did not start the live-view stream." & vbCrLf &
                                "If Windows asks to allow inbound network access, accept it - the " &
                                "stream arrives on a UDP port and is otherwise blocked.", "Live View")
            End If
        Else
            ' Connect if the driver isn't already connected over USB.
            Try
                If Not UsbTransport.IsConnected Then
                    UsbTransport.Connect(extended)
                    _ownsConnection = True
                End If
            Catch ex As Exception
                MessageBox.Show("USB connect failed: " & ex.Message, "Live View")
            End Try

            _cbShutter.Items.AddRange(UsbTransport.ShutterDisplay())
            _cbIso.Items.AddRange(UsbTransport.IsoDisplay())
            AddHandler _cbShutter.SelectedIndexChanged, Sub(s, e) UsbTransport.SetShutterByIndex(_cbShutter.SelectedIndex)
            AddHandler _cbIso.SelectedIndexChanged, Sub(s, e) UsbTransport.SetIsoIndex(_cbIso.SelectedIndex)

            UsbTransport.StartLiveView()
        End If
        _tmr = New Timer With {.Interval = 80} ' ~12 fps
        AddHandler _tmr.Tick, AddressOf OnTick
        _tmr.Start()

        AddHandler Me.FormClosing, AddressOf HandleFormClosing
    End Sub

    Private Sub OnTick(sender As Object, e As EventArgs)
        If _busy Then Return
        _busy = True
        Try
            Dim frame As Byte() = If(_wifi, If(_wifiLv IsNot Nothing, _wifiLv.GetFrame(), Nothing),
                                            UsbTransport.GetLiveViewFrame())
            If frame IsNot Nothing AndAlso frame.Length > 0 Then
                Dim img As Image
                Using ms As New MemoryStream(frame)
                    Using decoded As Image = Image.FromStream(ms)
                        img = New Bitmap(decoded) ' independent copy so the stream can close
                    End Using
                End Using
                Dim old As Image = _pic.Image
                _pic.Image = img
                If old IsNot Nothing Then old.Dispose()

                _framesSeen += 1
                If (DateTime.UtcNow - _lastInfo).TotalMilliseconds >= 1000 Then
                    Dim fps As Integer = _framesSeen - _lastCount
                    If _lastInfo = DateTime.MinValue Then fps = 0
                    _lblInfo.Text = String.Format("{0}x{1}   {2} fps", img.Width, img.Height, fps)
                    _lastInfo = DateTime.UtcNow
                    _lastCount = _framesSeen
                End If
            End If
        Catch
        Finally
            _busy = False
        End Try
    End Sub

    ' Not named OnClosing: that shadows Form.OnClosing (BC40005).
    Private Sub HandleFormClosing(sender As Object, e As FormClosingEventArgs)
        Try : _tmr.Stop() : Catch : End Try
        If _wifi Then
            ' Always stop the stream: the camera keeps sending UDP otherwise.
            Try
                If _wifiLv IsNot Nothing Then _wifiLv.Stop()
            Catch
            End Try
            Return
        End If
        Try : UsbTransport.StopLiveView() : Catch : End Try
        If _ownsConnection Then Try : UsbTransport.Disconnect() : Catch : End Try
    End Sub
End Class

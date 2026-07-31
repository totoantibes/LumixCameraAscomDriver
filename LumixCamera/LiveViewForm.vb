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

    Public Sub New(extended As Boolean)
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

        Dim btnClose As New Button With {.Text = "Close", .Location = New Point(612, 517), .Size = New Size(80, 26), .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right}
        AddHandler btnClose.Click, Sub(s, e) Me.Close()
        Me.Controls.Add(btnClose)

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
        _tmr = New Timer With {.Interval = 80} ' ~12 fps
        AddHandler _tmr.Tick, AddressOf OnTick
        _tmr.Start()

        AddHandler Me.FormClosing, AddressOf OnClosing
    End Sub

    Private Sub OnTick(sender As Object, e As EventArgs)
        If _busy Then Return
        _busy = True
        Try
            Dim frame As Byte() = UsbTransport.GetLiveViewFrame()
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
            End If
        Catch
        Finally
            _busy = False
        End Try
    End Sub

    Private Sub OnClosing(sender As Object, e As FormClosingEventArgs)
        Try : _tmr.Stop() : Catch : End Try
        Try : UsbTransport.StopLiveView() : Catch : End Try
        If _ownsConnection Then Try : UsbTransport.Disconnect() : Catch : End Try
    End Sub
End Class

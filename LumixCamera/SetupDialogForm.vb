Imports System.IO
Imports System.Net
Imports System.Data
Imports System.Net.NetworkInformation
Imports System.Xml
Imports System.Xml.Linq
Imports System.Linq

<ComVisible(False)>
Public Class SetupDialogForm

    Private Sub OK_Button_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles OK_Button.Click ' OK button event handler
        If Camera.IPAddress IsNot Camera.IPAddressDefault Then
            Camera.SendLumixMessage(Camera.ISO + CBISO.SelectedItem)
            Camera.SendLumixMessage(Camera.SHUTTERSPEED + Camera.ShutterTable(CBShutterSpeed.SelectedIndex, 0))
            Camera.SendLumixMessage(Camera.QUALITY + "raw_fine") 'that way we get all the format all the time. drawback is that the SD cards has now both RAW+JPG


        End If
        My.Settings.Resolution = CBResolution.SelectedItem.ToString()
        My.Settings.ISO = CBISO.SelectedItem.ToString()
        My.Settings.IPAddress = Camera.IPAddress
        My.Settings.TempPath = TBTempPath.Text
        My.Settings.ConnectionMode = SelectedMode ' persist the chosen transport for next session
        Me.DialogResult = System.Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub

    ''' <summary>
    ''' GetIpNetTable external method
    ''' </summary>
    ''' <param name="pIpNetTable"></param>
    ''' <param name="pdwSize"></param>
    ''' <param name="bOrder"></param>
    ''' <returns></returns>
    <DllImport("IpHlpApi.dll")>
    Private Shared Function GetIpNetTable(pIpNetTable As IntPtr, <MarshalAs(UnmanagedType.U4)> ByRef pdwSize As Integer, bOrder As Boolean) As <MarshalAs(UnmanagedType.U4)> Integer
    End Function

    ''' <summary>
    ''' Error codes GetIpNetTable returns that we recognise
    ''' </summary>
    Const ERROR_INSUFFICIENT_BUFFER As Integer = 122

    ' The Camera driver instance this dialog configures. Held rather than reaching for
    ' Shared members so this branch merges cleanly with the instance-state refactor.
    Private ReadOnly cam As Camera

    Public Sub New(cameraInstance As Camera)

        ' This call is required by the designer.
        InitializeComponent()
        cam = cameraInstance

        ' Add any initialization after the InitializeComponent() call.
        CBResolution.DataSource = New BindingSource(Camera.ResolutionTable, Nothing)
        CBISO.DataSource = New BindingSource(Camera.ISOTable, Nothing)
        For i = 0 To 58
            CBShutterSpeed.Items.Add(Camera.ShutterTable(i, 1))
        Next
        BuildModeSelector()
    End Sub

    Private CBConnectionMode As ComboBox
    Private lblModeStatus As Label
    Private btnLiveView As Button

    ''' <summary>
    ''' Add the connection-mode selector at the top of the form (WiFi / USB / USB
    ''' Extended). Detects a connected USB camera and the Tether DLL to preselect a
    ''' sensible default; the chosen value is persisted (My.Settings.ConnectionMode).
    ''' </summary>
    Private Sub BuildModeSelector()
        ' Three stacked rows, each on its own line so nothing can overlap on this narrow
        ' (~370px) form: the mode combo, then the USB/Tether status text, then the Live
        ' View button. The combo lines up with the other dropdowns and the button with
        ' the other left-hand buttons.
        Const rowMode As Integer = 8
        Const rowStatus As Integer = 40
        Const rowButton As Integer = 62
        Const shift As Integer = 96

        Me.ClientSize = New Drawing.Size(Me.ClientSize.Width, Me.ClientSize.Height + shift)
        For Each c As Control In Me.Controls
            c.Top += shift
        Next

        Dim rightEdge As Integer = Me.ClientSize.Width - 10

        Me.Controls.Add(New Label With {.Text = "Connection:", .Location = New Drawing.Point(12, rowMode + 4), .AutoSize = True})
        CBConnectionMode = New ComboBox With {
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .Location = New Drawing.Point(125, rowMode),
            .Size = New Drawing.Size(Math.Max(120, rightEdge - 125), 24)}
        CBConnectionMode.Items.AddRange(New Object() {ModeDisplay("WiFi"), ModeDisplay("USB"), ModeDisplay("USBExtended")})
        Me.Controls.Add(CBConnectionMode)

        lblModeStatus = New Label With {
            .Location = New Drawing.Point(12, rowStatus), .AutoSize = False,
            .Size = New Drawing.Size(rightEdge - 12, 18),
            .AutoEllipsis = True, .ForeColor = Drawing.Color.DimGray}
        Me.Controls.Add(lblModeStatus)

        btnLiveView = New Button With {
            .Text = "Live View…", .Location = New Drawing.Point(12, rowButton),
            .Size = New Drawing.Size(110, 26)}
        AddHandler btnLiveView.Click, AddressOf OpenLiveView
        Me.Controls.Add(btnLiveView)

        Dim usbPresent As Boolean = UsbTransport.IsUsbCameraPresent()
        Dim tetherPresent As Boolean = UsbTransport.IsTetherInstalled()
        lblModeStatus.Text = String.Format("USB cam: {0}   Tether: {1}",
            If(usbPresent, "detected", "none"), If(tetherPresent, "found", "not found"))

        ' Retain the saved choice when the hardware still supports it; otherwise fall
        ' back to what is actually available now.
        Dim saved As String = My.Settings.ConnectionMode
        Dim preselect As String
        If saved = "USBExtended" AndAlso usbPresent AndAlso tetherPresent Then
            preselect = "USBExtended"
        ElseIf saved = "USB" AndAlso usbPresent Then
            preselect = "USB"
        ElseIf saved = "WiFi" Then
            preselect = "WiFi"
        ElseIf usbPresent AndAlso tetherPresent Then
            preselect = "USBExtended"
        ElseIf usbPresent Then
            preselect = "USB"
        Else
            preselect = "WiFi"
        End If
        CBConnectionMode.SelectedItem = ModeDisplay(preselect)

        ' Live view works on both transports: the USB SDK hands over frames directly,
        ' and WiFi streams MJPEG over UDP once the camera has been told where to send it.
        ' Re-check on a mode change AND on an IP change - in WiFi the IP only appears
        ' once InitUI has run, which is after this constructor.
        RefreshLiveViewButton()
        AddHandler CBConnectionMode.SelectedIndexChanged, Sub(s, e) RefreshLiveViewButton()
        AddHandler CBCameraIPAddress.SelectedIndexChanged, Sub(s, e) RefreshLiveViewButton()
    End Sub

    ''' <summary>
    ''' Enable/disable the Live View button for the current mode and IP. Called from the
    ''' constructor, from Load (after discovery has found an IP) and on any mode or IP
    ''' change - evaluating it only in the constructor left the button disabled in WiFi
    ''' until the user toggled the mode away and back.
    ''' </summary>
    Private Sub RefreshLiveViewButton()
        If btnLiveView Is Nothing Then Return
        btnLiveView.Enabled = LiveViewAvailable()
    End Sub

    ''' <summary>
    ''' USB can always open live view; WiFi needs an IP first, since the stream is
    ''' started by an HTTP request to the camera.
    ''' </summary>
    Private Function LiveViewAvailable() As Boolean
        If SelectedMode.StartsWith("USB") Then Return True
        Return Not String.IsNullOrEmpty(cam.IPAddress) AndAlso cam.IPAddress <> Camera.IPAddressDefault
    End Function

    Private Sub OpenLiveView(sender As Object, e As EventArgs)
        If Not LiveViewAvailable() Then Return
        Using f As New LiveViewForm(SelectedMode = "USBExtended", Not SelectedMode.StartsWith("USB"), cam)
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Shared Function ModeDisplay(mode As String) As String
        Select Case mode
            Case "USB" : Return "USB (Standard)"
            Case "USBExtended" : Return "USB Extended (Tether)"
            Case Else : Return "Wi-Fi (HTTP)"
        End Select
    End Function

    Private Shared Function ModeFromDisplay(display As String) As String
        If display Is Nothing Then Return "WiFi"
        If display.StartsWith("USB Extended") Then Return "USBExtended"
        If display.StartsWith("USB") Then Return "USB"
        Return "WiFi"
    End Function

    ''' <summary>The connection mode currently selected in the dialog.</summary>
    Private ReadOnly Property SelectedMode As String
        Get
            If CBConnectionMode Is Nothing OrElse CBConnectionMode.SelectedItem Is Nothing Then Return My.Settings.ConnectionMode
            Return ModeFromDisplay(CBConnectionMode.SelectedItem.ToString())
        End Get
    End Property

    ''' <summary>
    ''' MIB_IPNETROW structure returned by GetIpNetTable
    ''' DO NOT MODIFY THIS STRUCTURE.
    ''' </summary>
    <StructLayout(LayoutKind.Sequential)>
    Private Structure MIB_IPNETROW
        <MarshalAs(UnmanagedType.U4)>
        Public dwIndex As Integer
        <MarshalAs(UnmanagedType.U4)>
        Public dwPhysAddrLen As Integer
        <MarshalAs(UnmanagedType.U1)>
        Public mac0 As Byte
        <MarshalAs(UnmanagedType.U1)>
        Public mac1 As Byte
        <MarshalAs(UnmanagedType.U1)>
        Public mac2 As Byte
        <MarshalAs(UnmanagedType.U1)>
        Public mac3 As Byte
        <MarshalAs(UnmanagedType.U1)>
        Public mac4 As Byte
        <MarshalAs(UnmanagedType.U1)>
        Public mac5 As Byte
        <MarshalAs(UnmanagedType.U1)>
        Public mac6 As Byte
        <MarshalAs(UnmanagedType.U1)>
        Public mac7 As Byte
        <MarshalAs(UnmanagedType.U4)>
        Public dwAddr As Integer
        <MarshalAs(UnmanagedType.U4)>
        Public dwType As Integer
    End Structure

    ''' <summary>
    ''' Get the IP and MAC addresses of all known devices on the LAN
    ''' </summary>
    ''' <remarks>
    ''' 1) This table is not updated often - it can take some human-scale time
    '''    to notice that a device has dropped off the network, or a new device
    '''    has connected.
    ''' 2) This discards non-local devices if they are found - these are multicast
    '''    and can be discarded by IP address range.
    ''' </remarks>
    ''' <returns></returns>
    Public Shared Function GetAllDevicesOnLAN() As Dictionary(Of IPAddress, PhysicalAddress)
        Dim all As New Dictionary(Of IPAddress, PhysicalAddress)()
        Dim spaceForNetTable As Integer = 0
        ' Get the space needed
        ' We do that by requesting the table, but not giving any space at all.
        ' The return value will tell us how much we actually need.
        GetIpNetTable(IntPtr.Zero, spaceForNetTable, False)
        ' Allocate the space
        ' We use a try-finally block to ensure release.
        Dim rawTable As IntPtr = IntPtr.Zero
        Try
            rawTable = Marshal.AllocCoTaskMem(spaceForNetTable)
            ' Get the actual data
            Dim errorCode As Integer = GetIpNetTable(rawTable, spaceForNetTable, False)
            If errorCode <> 0 Then
                ' Failed for some reason - can do no more here.
                Throw New Exception(String.Format("Unable to retrieve network table. Error code {0}", errorCode))
            End If
            ' Get the rows count
            Dim rowsCount As Integer = Marshal.ReadInt32(rawTable)
            Dim currentBuffer As New IntPtr(rawTable.ToInt64() + Marshal.SizeOf(GetType(Int32)))
            ' Convert the raw table to individual entries
            Dim rows As MIB_IPNETROW() = New MIB_IPNETROW(rowsCount - 1) {}
            For index As Integer = 0 To rowsCount - 1
                rows(index) = CType(Marshal.PtrToStructure(New IntPtr(currentBuffer.ToInt64() + (index * Marshal.SizeOf(GetType(MIB_IPNETROW)))), GetType(MIB_IPNETROW)), MIB_IPNETROW)
            Next
            ' Define the dummy entries list (we can discard these)
            Dim virtualMAC As New PhysicalAddress(New Byte() {0, 0, 0, 0, 0, 0})
            Dim broadcastMAC As New PhysicalAddress(New Byte() {255, 255, 255, 255, 255, 255})
            For Each row As MIB_IPNETROW In rows
                Dim ip As New IPAddress(BitConverter.GetBytes(row.dwAddr))
                Dim rawMAC As Byte() = New Byte() {row.mac0, row.mac1, row.mac2, row.mac3, row.mac4, row.mac5}
                Dim pa As New PhysicalAddress(rawMAC)
                If Not pa.Equals(virtualMAC) AndAlso Not pa.Equals(broadcastMAC) Then
                    'Console.WriteLine("IP: {0}\t\tMAC: {1}", ip.ToString(), pa.ToString());
                    If Not all.ContainsKey(ip) Then
                        all.Add(ip, pa)
                    End If
                End If
            Next
        Finally
            ' Release the memory.
            Marshal.FreeCoTaskMem(rawTable)
        End Try
        Return all
    End Function


    Private Sub Cancel_Button_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Cancel_Button.Click 'Cancel button event handler
        Me.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.Close()
    End Sub

    Private Sub ShowAscomWebPage(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles PictureBox1.DoubleClick, PictureBox1.Click
        ' Click on ASCOM logo event handler
        Try
            System.Diagnostics.Process.Start("http://ascom-standards.org/")
        Catch noBrowser As System.ComponentModel.Win32Exception
            If noBrowser.ErrorCode = -2147467259 Then
                MessageBox.Show(noBrowser.Message)
            End If
        Catch other As System.Exception
            MessageBox.Show(other.Message)
        End Try
    End Sub

    Private Sub SetupDialogForm_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load ' Form load event handler
        ' Retrieve current values of user settings from the ASCOM Profile.
        ' Only run the WiFi LAN discovery in WiFi mode (it is a slow, pointless scan over USB).
        If SelectedMode = "WiFi" Then InitUI()
        ' Discovery has now had its chance to find a camera IP, so re-evaluate the Live
        ' View button: at construction time there was no IP yet and it stayed disabled.
        RefreshLiveViewButton()
        ' Set default value for CBShutterSpeed
        If CBShutterSpeed.Items.Count > 0 Then

            CBShutterSpeed.SelectedIndex = 58 ' Bulb shutter speed
        End If

        ' Set default value for CBReadoutMode
        If CBReadoutMode.Items.Count > 0 Then
            CBReadoutMode.SelectedIndex = 2 ' Thumbnail readout mode
        End If



        If CBISO.Items.Count > 0 Then



            CBISO.SelectedIndex = 18 ' 3200 ISO
        End If



        If My.Settings.TempPath <> "" Then
            TBTempPath.Text = My.Settings.TempPath ' use the saved temp path
        Else
            TBTempPath.Text = "C:\Temp\" ' default temp path
        End If

    End Sub


    Private Sub InitUI()
        Dim request As WebRequest
        Dim myStreamReader As StreamReader
        Dim SendStatus As Integer = -1
        Dim statusCode As HttpStatusCode
        Dim ResponseText As String
        Dim Capabilities As XElement
        Dim CameraFound As Boolean = False
        Dim CameraConnected As Boolean = False

        Dim IPValues As New List(Of IPAddress)(GetAllDevicesOnLAN().Keys)
        CBCameraIPAddress.Items.Clear()
        CBCameraIPAddress.DataSource = New BindingSource(IPValues, Nothing)
        ' select the current IPAddress if possible
        If CBCameraIPAddress.Items.Contains(Camera.IPAddress) Then
            CBCameraIPAddress.SelectedItem = Camera.IPAddress
        End If

        'trying to connect to the Lumix Cam
        For Each TryIPValue As IPAddress In IPValues
            request = WebRequest.Create("http://" + TryIPValue.ToString + "/" + Camera.CAPABILITY)
            request.Timeout = 2000
            Try
                Dim myWebResponse = CType(request.GetResponse(), HttpWebResponse)


                If myWebResponse.StatusCode = HttpStatusCode.Accepted Or myWebResponse.StatusCode = 200 Then
                    SendStatus = 1 'message sent successfully
                    CBCameraIPAddress.SelectedItem = TryIPValue
                    Camera.IPAddress = TryIPValue.ToString
                    myStreamReader = New StreamReader(myWebResponse.GetResponseStream())
                    Using (myStreamReader)
                        ResponseText = myStreamReader.ReadToEnd
                    End Using
                    If ResponseText.ToString.Contains("camrply") Then
                        Capabilities = XElement.Parse(ResponseText)
                        '//{<result>err_already_connected</result>}

                        If Capabilities.FirstNode.ToString.Contains("err_already_connected") Then
                            CameraConnected = True
                        Else

                            Dim Capability As IEnumerable(Of XElement) =
                            From El In Capabilities.<contents_action_info>
                            Select El
                            For Each el As XElement In Capability
                                Camera.MODEL = el.@model
                                Label8.Text = el.@model
                                '                        CBResolution.SelectedItem = Camera.Models(Camera.MODEL)
                                CBResolution.SelectedIndex = CBResolution.FindString(Camera.Models(Camera.MODEL).ToString)

                                CameraFound = True
                            Next

                            If Not CameraFound Then
                                Dim xml As XElement = XElement.Parse(ResponseText)
                                Dim modelName As String = xml.<productinfo>.<modelname>.FirstOrDefault()?.Value

                                If Not String.IsNullOrEmpty(modelName) Then
                                    Camera.MODEL = modelName
                                    Label8.Text = modelName
                                    CameraFound = True
                                End If

                            End If

                            Exit For
                        End If
                    End If
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

                        End Try
                    End Using
                End If
            End Try
        Next

        If CameraConnected Then
            MsgBox("your Camera is already connected to another device", MsgBoxStyle.Information)
        ElseIf (Not CameraFound) Then
            MsgBox("Camera was not found on the network. Make sure the camera is on and connected to your PC network", MsgBoxStyle.Information)
        End If
    End Sub


    Private Sub CameraIPAddress_SelectedIndexChanged(sender As Object, e As EventArgs) Handles CBCameraIPAddress.SelectedIndexChanged
        Camera.IPAddress = CBCameraIPAddress.SelectedItem.ToString
    End Sub

    Private Sub CameraIPAddress_ValueMemberChanged(sender As Object, e As EventArgs) Handles CBCameraIPAddress.ValueMemberChanged
        Camera.IPAddress = CBCameraIPAddress.SelectedItem.ToString
    End Sub

    Private Sub ButtonTemp_Click(sender As Object, e As EventArgs) Handles ButtonTemp.Click
        If FolderBrowserDialog1.ShowDialog = DialogResult.OK Then
            TBTempPath.Text = Path.GetFullPath(FolderBrowserDialog1.SelectedPath + "\")
        End If
    End Sub

    Private Sub Label8_Click(sender As Object, e As EventArgs) Handles Label8.Click

    End Sub

    Private Sub Label4_Click(sender As Object, e As EventArgs) Handles Label4.Click

    End Sub

    Private Sub CBShutterSpeed_SelectedIndexChanged(sender As Object, e As EventArgs) Handles CBShutterSpeed.SelectedIndexChanged

    End Sub

    Private Sub CBReadoutMode_SelectedIndexChanged(sender As Object, e As EventArgs) Handles CBReadoutMode.SelectedIndexChanged

    End Sub

    Private Sub CBResolution_SelectedIndexChanged(sender As Object, e As EventArgs) Handles CBResolution.SelectedIndexChanged
        My.Settings.Resolution = CBResolution.SelectedItem.ToString()
        My.Settings.Save()
    End Sub

    Private Sub OpenFileDialog1_FileOk(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles OpenFileDialog1.FileOk

    End Sub
End Class

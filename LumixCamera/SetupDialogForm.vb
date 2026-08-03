Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Data
Imports System.Net.NetworkInformation
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Collections.Concurrent
Imports System.Xml
Imports System.Xml.Linq
Imports System.Linq

<ComVisible(False)>
Public Class SetupDialogForm

    Private Sub OK_Button_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles OK_Button.Click ' OK button event handler
        ' Flush any pending data-bindings (e.g. CBReadoutMode -> TransferFormat) before reading the controls.
        Me.Validate()

        ' Commit the selected IP straight from the dropdown. Relying on the
        ' SelectedIndexChanged handler alone loses a pre-filled value on first
        ' connect, because that event does not fire for a programmatic selection.
        If CBCameraIPAddress.SelectedItem IsNot Nothing Then
            cam.IPAddress = CBCameraIPAddress.SelectedItem.ToString()
        End If

        ' Value comparison ('IsNot' compared string references, not their contents).
        If cam.IPAddress <> Camera.IPAddressDefault Then
            If CBISO.SelectedItem IsNot Nothing Then
                cam.SendLumixMessage(Camera.ISO + CBISO.SelectedItem.ToString())
            End If
            cam.SendLumixMessage(Camera.SHUTTERSPEED + Camera.ShutterTable(CBShutterSpeed.SelectedIndex, 0))
            cam.SendLumixMessage(Camera.QUALITY + "raw_fine") 'that way we get all the format all the time. drawback is that the SD cards has now both RAW+JPG
        End If

        ' Persist every setting straight from the controls so a pre-filled value
        ' is honoured even when the user never re-selects it from the dropdown.
        If CBResolution.SelectedItem IsNot Nothing Then My.Settings.Resolution = CBResolution.SelectedItem.ToString()
        If CBISO.SelectedItem IsNot Nothing Then My.Settings.ISO = CBISO.SelectedItem.ToString()
        If CBReadoutMode.SelectedItem IsNot Nothing Then My.Settings.TransferFormat = CBReadoutMode.SelectedItem.ToString()
        My.Settings.IPAddress = cam.IPAddress
        My.Settings.TempPath = TBTempPath.Text
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

    ' The Camera driver instance this dialog configures - used for the per-instance
    ' state (IPAddress, MODEL, SendLumixMessage) that is no longer Shared.
    Private ReadOnly cam As Camera

    ''' <summary>
    ''' True when the driver is already connected. Settings that define the connection
    ''' itself cannot be changed live, so those controls are disabled - see
    ''' SetupDialogForm_Load.
    ''' </summary>
    Private ReadOnly _connected As Boolean

    Public Sub New(cameraInstance As Camera, Optional connected As Boolean = False)
        _connected = connected

        ' This call is required by the designer.
        InitializeComponent()
        cam = cameraInstance

        ' Add any initialization after the InitializeComponent() call.
        CBResolution.DataSource = New BindingSource(Camera.ResolutionTable, Nothing)
        ' Offer the same entries ASCOM Gain indexes into: numeric ISOs only. The raw
        ' ISOTable still holds "auto"/"i_iso", so binding to it would offer values the
        ' gain list no longer contains and the saved ISO would not resolve.
        Dim selectableIso As New ArrayList
        For Each isoValue As String In Camera.ISOTable
            Dim numericIso As Integer
            If Integer.TryParse(isoValue, numericIso) Then selectableIso.Add(isoValue)
        Next
        CBISO.DataSource = New BindingSource(selectableIso, Nothing)
        For i = 0 To 58
            CBShutterSpeed.Items.Add(Camera.ShutterTable(i, 1))
        Next
    End Sub

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

    ''' <summary>
    ''' Discover candidate camera IPs robustly, independent of the (often stale)
    ''' ARP cache. Sources, unioned with confirmed cameras listed first:
    '''   1) an active parallel probe of every local /24 that hits the Lumix
    '''      cam.cgi capability endpoint (firewall- and ARP-tolerant),
    '''   2) SSDP M-SEARCH across all interfaces (the camera is a UPnP MediaServer),
    '''   3) the Windows ARP table (previous behaviour, kept as a fallback source).
    ''' </summary>
    Public Shared Function DiscoverLumixCameras() As List(Of IPAddress)
        Dim candidates As New HashSet(Of String)()

        ' Source 2 + 3: SSDP responders and the ARP table (best-effort). Keep only
        ' real unicast host addresses (drops multicast/broadcast ARP entries).
        Try
            For Each ip As String In DiscoverViaSsdp(1000)
                AddIfUsable(candidates, ip)
            Next
        Catch
        End Try
        Try
            For Each ip As IPAddress In GetAllDevicesOnLAN().Keys
                AddIfUsable(candidates, ip.ToString())
            Next
        Catch
        End Try

        ' Source 1: sweep every local /24 (plus everything found above) and keep
        ' only hosts that actually answer the Lumix control API.
        Dim targets As New HashSet(Of String)(candidates)
        For Each localIp As IPAddress In GetLocalIPv4Addresses()
            If Not IsSweepablePrivate(localIp) Then Continue For
            Dim octets() As String = localIp.ToString().Split("."c)
            If octets.Length <> 4 Then Continue For
            Dim prefix As String = String.Join(".", octets(0), octets(1), octets(2)) & "."
            For host As Integer = 1 To 254
                targets.Add(prefix & host)
            Next
        Next

        ' Blocking connect probes starve on thread-pool injection throttling, so
        ' raise the floor to let the sweep actually run wide (restored afterwards).
        Dim minW As Integer, minIo As Integer
        ThreadPool.GetMinThreads(minW, minIo)
        ThreadPool.SetMinThreads(Math.Max(minW, 256), Math.Max(minIo, 256))
        Dim confirmed As New ConcurrentBag(Of String)()
        Try
            Parallel.ForEach(targets, New ParallelOptions With {.MaxDegreeOfParallelism = 256},
                Sub(ip As String)
                    If IsLumixCamera(ip, 500) Then confirmed.Add(ip)
                End Sub)
        Catch
        Finally
            ThreadPool.SetMinThreads(minW, minIo)
        End Try

        ' Confirmed cameras first, then any other known candidate.
        Dim result As New List(Of IPAddress)()
        Dim seen As New HashSet(Of String)()
        For Each ip As String In confirmed
            Dim parsed As IPAddress = Nothing
            If seen.Add(ip) AndAlso IPAddress.TryParse(ip, parsed) Then result.Add(parsed)
        Next
        For Each ip As String In candidates
            Dim parsed As IPAddress = Nothing
            If seen.Add(ip) AndAlso IPAddress.TryParse(ip, parsed) Then result.Add(parsed)
        Next
        Return result
    End Function

    ''' <summary>Add an address to the set only if it is a real unicast host.</summary>
    Private Shared Sub AddIfUsable(bag As HashSet(Of String), ip As String)
        Dim parsed As IPAddress = Nothing
        If IPAddress.TryParse(ip, parsed) AndAlso IsUsableHostAddress(parsed) Then bag.Add(parsed.ToString())
    End Sub

    ''' <summary>Exclude multicast (224-239), reserved (>=240), and .0/.255 addresses.</summary>
    Private Shared Function IsUsableHostAddress(ip As IPAddress) As Boolean
        Dim b() As Byte = ip.GetAddressBytes()
        If b.Length <> 4 Then Return False
        If b(0) = 0 OrElse b(0) >= 224 Then Return False
        If b(3) = 0 OrElse b(3) = 255 Then Return False
        Return True
    End Function

    ''' <summary>True if the host answers the Lumix cam.cgi capability endpoint.</summary>
    Private Shared Function IsLumixCamera(ip As String, timeoutMs As Integer) As Boolean
        ' Fast TCP:80 gate first so dead addresses don't cost a full HTTP timeout.
        Try
            Using tcp As New TcpClient()
                Dim ar As IAsyncResult = tcp.BeginConnect(ip, 80, Nothing, Nothing)
                If Not ar.AsyncWaitHandle.WaitOne(timeoutMs) Then Return False
                tcp.EndConnect(ar)
            End Using
        Catch
            Return False
        End Try
        Try
            Dim req As HttpWebRequest = CType(WebRequest.Create("http://" & ip & "/" & Camera.CAPABILITY), HttpWebRequest)
            req.Timeout = timeoutMs
            Using resp As HttpWebResponse = CType(req.GetResponse(), HttpWebResponse)
                Using sr As New StreamReader(resp.GetResponseStream())
                    Dim body As String = sr.ReadToEnd()
                    Return body.Contains("camrply") OrElse body.Contains("contents_action_info") OrElse body.Contains("productinfo")
                End Using
            End Using
        Catch
            Return False
        End Try
    End Function

    ''' <summary>All operational, non-loopback IPv4 addresses on this host.</summary>
    Private Shared Function GetLocalIPv4Addresses() As List(Of IPAddress)
        Dim addrs As New List(Of IPAddress)()
        For Each ni As NetworkInterface In NetworkInterface.GetAllNetworkInterfaces()
            If ni.OperationalStatus <> OperationalStatus.Up Then Continue For
            If ni.NetworkInterfaceType = NetworkInterfaceType.Loopback Then Continue For
            For Each ua As UnicastIPAddressInformation In ni.GetIPProperties().UnicastAddresses
                If ua.Address.AddressFamily = AddressFamily.InterNetwork Then addrs.Add(ua.Address)
            Next
        Next
        Return addrs
    End Function

    ''' <summary>
    ''' Sweep only RFC1918 LAN ranges (10/8, 172.16-31/12, 192.168/16). Excludes
    ''' Tailscale CGNAT (100.64/10) and other non-LAN interfaces so we never try
    ''' to enumerate a huge or irrelevant address space.
    ''' </summary>
    Private Shared Function IsSweepablePrivate(ip As IPAddress) As Boolean
        Dim b() As Byte = ip.GetAddressBytes()
        If b.Length <> 4 Then Return False
        If b(0) = 10 Then Return True
        If b(0) = 172 AndAlso b(1) >= 16 AndAlso b(1) <= 31 Then Return True
        If b(0) = 192 AndAlso b(1) = 168 Then Return True
        Return False
    End Function

    ''' <summary>SSDP M-SEARCH from every interface; returns responder IPs.</summary>
    Private Shared Function DiscoverViaSsdp(timeoutMs As Integer) As HashSet(Of String)
        Dim found As New HashSet(Of String)()
        Dim mcast As New IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900)
        Dim msg As String =
            "M-SEARCH * HTTP/1.1" & vbCrLf &
            "HOST: 239.255.255.250:1900" & vbCrLf &
            "MAN: ""ssdp:discover""" & vbCrLf &
            "MX: 1" & vbCrLf &
            "ST: urn:schemas-upnp-org:device:MediaServer:1" & vbCrLf & vbCrLf
        Dim payload() As Byte = Encoding.ASCII.GetBytes(msg)
        For Each localIp As IPAddress In GetLocalIPv4Addresses()
            Dim client As UdpClient = Nothing
            Try
                client = New UdpClient(New IPEndPoint(localIp, 0))
                client.Client.ReceiveTimeout = timeoutMs
                client.Send(payload, payload.Length, mcast)
                client.Send(payload, payload.Length, mcast) ' UDP: repeat, datagrams can be lost
                Dim deadline As DateTime = DateTime.UtcNow.AddMilliseconds(timeoutMs)
                While DateTime.UtcNow < deadline
                    Try
                        Dim rep As New IPEndPoint(IPAddress.Any, 0)
                        client.Receive(rep)
                        found.Add(rep.Address.ToString())
                    Catch
                        Exit While ' receive timed out
                    End Try
                End While
            Catch
            Finally
                If client IsNot Nothing Then client.Close()
            End Try
        Next
        Return found
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
        ' Retrieve current values of user settings from the ASCOM Profile
        InitUI()

        ' Everything else can be pushed to a live camera, but these two define the
        ' connection: the IP is what we are connected TO, and the resolution fixes the
        ' reported sensor size, which ASCOM clients read once and cache. Disable them
        ' rather than accept a change we would silently ignore until reconnect.
        If _connected Then
            CBCameraIPAddress.Enabled = False
            CBResolution.Enabled = False
            Me.Text = Me.Text & " - connected (IP and resolution need a reconnect)"
        End If
        ' Set default value for CBShutterSpeed
        If CBShutterSpeed.Items.Count > 0 Then

            CBShutterSpeed.SelectedIndex = 58 ' Bulb shutter speed
        End If

        ' Set default value for CBReadoutMode
        If CBReadoutMode.Items.Count > 0 Then
            CBReadoutMode.SelectedIndex = 2 ' Thumbnail readout mode
        End If



        If CBISO.Items.Count > 0 Then
            ' Select by value, not by a hardcoded position: index 18 meant "3200" only
            ' while "auto"/"i_iso" padded the front of the list.
            Dim iso3200 As Integer = CBISO.FindStringExact("3200")
            CBISO.SelectedIndex = If(iso3200 >= 0, iso3200, 0)
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

        ' Robust discovery: confirmed Lumix cameras first (active cam.cgi sweep +
        ' SSDP), then other LAN devices. No longer depends on the stale ARP cache.
        Dim IPValues As List(Of IPAddress) = DiscoverLumixCameras()
        CBCameraIPAddress.Items.Clear()
        CBCameraIPAddress.DataSource = New BindingSource(IPValues, Nothing)
        ' Pre-select the saved IP. Items are IPAddress objects while cam.IPAddress
        ' is a String, so match on the text form (Items.Contains(String) never matched).
        For Each addr As IPAddress In IPValues
            If addr.ToString() = cam.IPAddress Then
                CBCameraIPAddress.SelectedItem = addr
                Exit For
            End If
        Next

        'trying to connect to the Lumix Cam
        For Each TryIPValue As IPAddress In IPValues
            request = WebRequest.Create("http://" + TryIPValue.ToString + "/" + Camera.CAPABILITY)
            request.Timeout = 2000
            Try
                Dim myWebResponse = CType(request.GetResponse(), HttpWebResponse)


                If myWebResponse.StatusCode = HttpStatusCode.Accepted Or myWebResponse.StatusCode = 200 Then
                    SendStatus = 1 'message sent successfully
                    CBCameraIPAddress.SelectedItem = TryIPValue
                    cam.IPAddress = TryIPValue.ToString
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
                                cam.MODEL = el.@model
                                Label8.Text = el.@model
                                '                        CBResolution.SelectedItem = Camera.Models(cam.MODEL)
                                ' Unlisted bodies (e.g. FZ82) are not in Models -> guard the
                                ' NullReference that ToString() would throw and just leave the
                                ' resolution at its current selection.
                                Dim knownRes As Object = Camera.Models(cam.MODEL)
                                If knownRes IsNot Nothing Then
                                    CBResolution.SelectedIndex = CBResolution.FindString(knownRes.ToString())
                                End If

                                CameraFound = True
                            Next

                            If Not CameraFound Then
                                Dim xml As XElement = XElement.Parse(ResponseText)
                                Dim modelName As String = xml.<productinfo>.<modelname>.FirstOrDefault()?.Value

                                If Not String.IsNullOrEmpty(modelName) Then
                                    cam.MODEL = modelName
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
        ' Fires with SelectedItem = Nothing during the Items.Clear()/DataSource reset in InitUI.
        If CBCameraIPAddress.SelectedItem IsNot Nothing Then cam.IPAddress = CBCameraIPAddress.SelectedItem.ToString()
    End Sub

    Private Sub CameraIPAddress_ValueMemberChanged(sender As Object, e As EventArgs) Handles CBCameraIPAddress.ValueMemberChanged
        If CBCameraIPAddress.SelectedItem IsNot Nothing Then cam.IPAddress = CBCameraIPAddress.SelectedItem.ToString()
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

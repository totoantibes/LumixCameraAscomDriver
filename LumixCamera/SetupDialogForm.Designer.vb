Imports System.DirectoryServices

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class SetupDialogForm
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.TableLayoutPanel1 = New System.Windows.Forms.TableLayoutPanel()
        Me.OK_Button = New System.Windows.Forms.Button()
        Me.Cancel_Button = New System.Windows.Forms.Button()
        Me.PictureBox1 = New System.Windows.Forms.PictureBox()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.ButtonConnect = New System.Windows.Forms.Button()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.ButtonTemp = New System.Windows.Forms.Button()
        Me.ButtonRaw = New System.Windows.Forms.Button()
        Me.ToolTip1 = New System.Windows.Forms.ToolTip(Me.components)
        Me.Label1 = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.TBDCRawPath = New System.Windows.Forms.TextBox()
        Me.CBReadoutMode = New System.Windows.Forms.ComboBox()
        Me.CBShutterSpeed = New System.Windows.Forms.ComboBox()
        Me.CBISO = New System.Windows.Forms.ComboBox()
        Me.CBCameraIPAddress = New System.Windows.Forms.ComboBox()
        Me.chkTrace = New System.Windows.Forms.CheckBox()
        Me.OpenFileDialog1 = New System.Windows.Forms.OpenFileDialog()
        Me.FolderBrowserDialog1 = New System.Windows.Forms.FolderBrowserDialog()
        Me.TBTempPath = New System.Windows.Forms.TextBox()
        Me.TableLayoutPanel1.SuspendLayout()
        CType(Me.PictureBox1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'TableLayoutPanel1
        '
        Me.TableLayoutPanel1.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.TableLayoutPanel1.ColumnCount = 2
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.0!))
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.0!))
        Me.TableLayoutPanel1.Controls.Add(Me.OK_Button, 0, 0)
        Me.TableLayoutPanel1.Controls.Add(Me.Cancel_Button, 1, 0)
        Me.TableLayoutPanel1.Location = New System.Drawing.Point(219, 475)
        Me.TableLayoutPanel1.Margin = New System.Windows.Forms.Padding(4)
        Me.TableLayoutPanel1.Name = "TableLayoutPanel1"
        Me.TableLayoutPanel1.RowCount = 1
        Me.TableLayoutPanel1.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50.0!))
        Me.TableLayoutPanel1.Size = New System.Drawing.Size(195, 36)
        Me.TableLayoutPanel1.TabIndex = 0
        '
        'OK_Button
        '
        Me.OK_Button.Anchor = System.Windows.Forms.AnchorStyles.None
        Me.OK_Button.Location = New System.Drawing.Point(4, 4)
        Me.OK_Button.Margin = New System.Windows.Forms.Padding(4)
        Me.OK_Button.Name = "OK_Button"
        Me.OK_Button.Size = New System.Drawing.Size(89, 28)
        Me.OK_Button.TabIndex = 0
        Me.OK_Button.Text = "OK"
        '
        'Cancel_Button
        '
        Me.Cancel_Button.Anchor = System.Windows.Forms.AnchorStyles.None
        Me.Cancel_Button.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.Cancel_Button.Location = New System.Drawing.Point(101, 4)
        Me.Cancel_Button.Margin = New System.Windows.Forms.Padding(4)
        Me.Cancel_Button.Name = "Cancel_Button"
        Me.Cancel_Button.Size = New System.Drawing.Size(89, 28)
        Me.Cancel_Button.TabIndex = 1
        Me.Cancel_Button.Text = "Cancel"
        '
        'PictureBox1
        '
        Me.PictureBox1.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.PictureBox1.Cursor = System.Windows.Forms.Cursors.Hand
        Me.PictureBox1.Image = Global.ASCOM.LumixG80.My.Resources.Resources.ASCOM
        Me.PictureBox1.Location = New System.Drawing.Point(337, 373)
        Me.PictureBox1.Margin = New System.Windows.Forms.Padding(4)
        Me.PictureBox1.Name = "PictureBox1"
        Me.PictureBox1.Size = New System.Drawing.Size(48, 56)
        Me.PictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize
        Me.PictureBox1.TabIndex = 2
        Me.PictureBox1.TabStop = False
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Location = New System.Drawing.Point(9, 76)
        Me.Label3.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(143, 17)
        Me.Label3.TabIndex = 0
        Me.Label3.Text = "IP Address of camera"
        '
        'ButtonConnect
        '
        Me.ButtonConnect.Location = New System.Drawing.Point(315, 72)
        Me.ButtonConnect.Name = "ButtonConnect"
        Me.ButtonConnect.Size = New System.Drawing.Size(75, 30)
        Me.ButtonConnect.TabIndex = 2
        Me.ButtonConnect.Text = "Connect"
        Me.ButtonConnect.UseVisualStyleBackColor = True
        '
        'Label4
        '
        Me.Label4.AutoSize = True
        Me.Label4.Location = New System.Drawing.Point(12, 18)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(333, 34)
        Me.Label4.TabIndex = 4
        Me.Label4.Text = "Set the Camera to Manual" & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & "Connect to the WIFI network in remote control mode"
        '
        'ButtonTemp
        '
        Me.ButtonTemp.AccessibleDescription = "this is where the files from the camera are stored (JPGor RAW) while trnasorming " &
    "to tiff and then passed on to the calling program as an imagearray. the files ar" &
    "e automatically deleted after each step"
        Me.ButtonTemp.Location = New System.Drawing.Point(15, 393)
        Me.ButtonTemp.Name = "ButtonTemp"
        Me.ButtonTemp.Size = New System.Drawing.Size(180, 23)
        Me.ButtonTemp.TabIndex = 6
        Me.ButtonTemp.Text = "Path to temp folder"
        Me.ToolTip1.SetToolTip(Me.ButtonTemp, "this is where the files from the camera are stored (JPGor RAW) while trnasorming " &
        "to tiff and then passed on to the calling program as an imagearray. the files ar" &
        "e automatically deleted after each step")
        Me.ButtonTemp.UseVisualStyleBackColor = True
        '
        'ButtonRaw
        '
        Me.ButtonRaw.AccessibleDescription = "point to a valid DCRaw.exe.  try  http://www.centrostudiprogressofotografico.it/e" &
    "n/dcraw/"
        Me.ButtonRaw.Location = New System.Drawing.Point(15, 435)
        Me.ButtonRaw.Name = "ButtonRaw"
        Me.ButtonRaw.Size = New System.Drawing.Size(180, 23)
        Me.ButtonRaw.TabIndex = 7
        Me.ButtonRaw.Text = "Path to DCRaw.exe"
        Me.ToolTip1.SetToolTip(Me.ButtonRaw, "point to a valid DCRaw.exe.  try  http://www.centrostudiprogressofotografico.it/e" &
        "n/dcraw/")
        Me.ButtonRaw.UseVisualStyleBackColor = True
        '
        'ToolTip1
        '
        Me.ToolTip1.ToolTipTitle = "tooltip"
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(9, 126)
        Me.Label1.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(31, 17)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "ISO"
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(11, 176)
        Me.Label2.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(99, 17)
        Me.Label2.TabIndex = 0
        Me.Label2.Text = "Shutter Speed"
        '
        'Label5
        '
        Me.Label5.AutoSize = True
        Me.Label5.Location = New System.Drawing.Point(9, 226)
        Me.Label5.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(106, 17)
        Me.Label5.TabIndex = 0
        Me.Label5.Text = "TransferFormat"
        '
        'TBDCRawPath
        '
        Me.TBDCRawPath.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.LumixG80.My.MySettings.Default, "DCRawPath", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.TBDCRawPath.Location = New System.Drawing.Point(15, 479)
        Me.TBDCRawPath.Name = "TBDCRawPath"
        Me.TBDCRawPath.Size = New System.Drawing.Size(25, 22)
        Me.TBDCRawPath.TabIndex = 10
        Me.TBDCRawPath.Text = Global.ASCOM.LumixG80.My.MySettings.Default.DCRawPath
        Me.TBDCRawPath.Visible = False
        '
        'CBReadoutMode
        '
        Me.CBReadoutMode.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.LumixG80.My.MySettings.Default, "TransferFormat", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.CBReadoutMode.FormattingEnabled = True
        Me.CBReadoutMode.Items.AddRange(New Object() {"JPG", "RAW"})
        Me.CBReadoutMode.Location = New System.Drawing.Point(161, 223)
        Me.CBReadoutMode.Name = "CBReadoutMode"
        Me.CBReadoutMode.Size = New System.Drawing.Size(121, 24)
        Me.CBReadoutMode.TabIndex = 9
        Me.CBReadoutMode.Text = Global.ASCOM.LumixG80.My.MySettings.Default.TransferFormat
        '
        'CBShutterSpeed
        '
        Me.CBShutterSpeed.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.LumixG80.My.MySettings.Default, "Speed", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.CBShutterSpeed.FormattingEnabled = True
        Me.CBShutterSpeed.Location = New System.Drawing.Point(161, 173)
        Me.CBShutterSpeed.Name = "CBShutterSpeed"
        Me.CBShutterSpeed.Size = New System.Drawing.Size(121, 24)
        Me.CBShutterSpeed.TabIndex = 9
        Me.CBShutterSpeed.Text = Global.ASCOM.LumixG80.My.MySettings.Default.Speed
        '
        'CBISO
        '
        Me.CBISO.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.LumixG80.My.MySettings.Default, "ISO", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.CBISO.FormattingEnabled = True
        Me.CBISO.Location = New System.Drawing.Point(161, 123)
        Me.CBISO.Name = "CBISO"
        Me.CBISO.Size = New System.Drawing.Size(121, 24)
        Me.CBISO.TabIndex = 8
        Me.CBISO.Text = Global.ASCOM.LumixG80.My.MySettings.Default.ISO
        '
        'CBCameraIPAddress
        '
        Me.CBCameraIPAddress.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.LumixG80.My.MySettings.Default, "IPAddress", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.CBCameraIPAddress.FormattingEnabled = True
        Me.CBCameraIPAddress.Location = New System.Drawing.Point(161, 74)
        Me.CBCameraIPAddress.Name = "CBCameraIPAddress"
        Me.CBCameraIPAddress.Size = New System.Drawing.Size(121, 24)
        Me.CBCameraIPAddress.TabIndex = 1
        Me.CBCameraIPAddress.Text = Global.ASCOM.LumixG80.My.MySettings.Default.IPAddress
        '
        'chkTrace
        '
        Me.chkTrace.AutoSize = True
        Me.chkTrace.Checked = Global.ASCOM.LumixG80.My.MySettings.Default.TraceEnabled
        Me.chkTrace.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkTrace.DataBindings.Add(New System.Windows.Forms.Binding("Checked", Global.ASCOM.LumixG80.My.MySettings.Default, "TraceEnabled", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.chkTrace.Location = New System.Drawing.Point(12, 343)
        Me.chkTrace.Margin = New System.Windows.Forms.Padding(4)
        Me.chkTrace.Name = "chkTrace"
        Me.chkTrace.Size = New System.Drawing.Size(87, 21)
        Me.chkTrace.TabIndex = 5
        Me.chkTrace.Text = "Trace on"
        Me.chkTrace.UseVisualStyleBackColor = True
        '
        'OpenFileDialog1
        '
        Me.OpenFileDialog1.FileName = "dcraw-9.27-ms-64-bit.exe"
        Me.OpenFileDialog1.InitialDirectory = """C:\Users\robert.hasson\source\repos\LumixCamera\packages\NDCRaw.0.5.2\lib\net461" &
    "\"
        '
        'TBTempPath
        '
        Me.TBTempPath.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.LumixG80.My.MySettings.Default, "TempPath", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.TBTempPath.Location = New System.Drawing.Point(46, 479)
        Me.TBTempPath.Name = "TBTempPath"
        Me.TBTempPath.Size = New System.Drawing.Size(25, 22)
        Me.TBTempPath.TabIndex = 10
        Me.TBTempPath.Text = Global.ASCOM.LumixG80.My.MySettings.Default.TempPath
        Me.TBTempPath.Visible = False
        '
        'SetupDialogForm
        '
        Me.AcceptButton = Me.OK_Button
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.CancelButton = Me.Cancel_Button
        Me.ClientSize = New System.Drawing.Size(427, 524)
        Me.Controls.Add(Me.TBTempPath)
        Me.Controls.Add(Me.TBDCRawPath)
        Me.Controls.Add(Me.CBReadoutMode)
        Me.Controls.Add(Me.CBShutterSpeed)
        Me.Controls.Add(Me.CBISO)
        Me.Controls.Add(Me.ButtonRaw)
        Me.Controls.Add(Me.ButtonTemp)
        Me.Controls.Add(Me.ButtonConnect)
        Me.Controls.Add(Me.Label4)
        Me.Controls.Add(Me.CBCameraIPAddress)
        Me.Controls.Add(Me.chkTrace)
        Me.Controls.Add(Me.Label5)
        Me.Controls.Add(Me.Label2)
        Me.Controls.Add(Me.Label1)
        Me.Controls.Add(Me.Label3)
        Me.Controls.Add(Me.PictureBox1)
        Me.Controls.Add(Me.TableLayoutPanel1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.Margin = New System.Windows.Forms.Padding(4)
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "SetupDialogForm"
        Me.ShowInTaskbar = False
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "DeviceName Setup"
        Me.TableLayoutPanel1.ResumeLayout(False)
        CType(Me.PictureBox1, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents TableLayoutPanel1 As System.Windows.Forms.TableLayoutPanel
    Friend WithEvents OK_Button As System.Windows.Forms.Button
    Friend WithEvents Cancel_Button As System.Windows.Forms.Button
    Friend WithEvents PictureBox1 As System.Windows.Forms.PictureBox
    Friend WithEvents chkTrace As System.Windows.Forms.CheckBox
    Friend WithEvents CBCameraIPAddress As ComboBox
    Private WithEvents Label3 As Label
    Friend WithEvents ButtonConnect As Button
    Friend WithEvents Label4 As Label
    Friend WithEvents ButtonTemp As Button
    Friend WithEvents ButtonRaw As Button
    Friend WithEvents OpenFileDialog1 As OpenFileDialog
    Friend WithEvents FolderBrowserDialog1 As FolderBrowserDialog
    Friend WithEvents ToolTip1 As ToolTip
    Friend WithEvents CBISO As ComboBox
    Friend WithEvents CBShutterSpeed As ComboBox
    Friend WithEvents CBReadoutMode As ComboBox
    Private WithEvents Label1 As Label
    Private WithEvents Label2 As Label
    Private WithEvents Label5 As Label
    Friend WithEvents TBDCRawPath As TextBox
    Friend WithEvents TBTempPath As TextBox
End Class

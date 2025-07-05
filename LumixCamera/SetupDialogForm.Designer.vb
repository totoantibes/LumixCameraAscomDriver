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
        Me.Label4 = New System.Windows.Forms.Label()
        Me.ButtonTemp = New System.Windows.Forms.Button()
        Me.ToolTip1 = New System.Windows.Forms.ToolTip(Me.components)
        Me.Label1 = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.OpenFileDialog1 = New System.Windows.Forms.OpenFileDialog()
        Me.FolderBrowserDialog1 = New System.Windows.Forms.FolderBrowserDialog()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.CBResolution = New System.Windows.Forms.ComboBox()
        Me.TBTempPath = New System.Windows.Forms.TextBox()
        Me.CBReadoutMode = New System.Windows.Forms.ComboBox()
        Me.CBShutterSpeed = New System.Windows.Forms.ComboBox()
        Me.CBISO = New System.Windows.Forms.ComboBox()
        Me.CBCameraIPAddress = New System.Windows.Forms.ComboBox()
        Me.chkTrace = New System.Windows.Forms.CheckBox()
        Me.Label7 = New System.Windows.Forms.Label()
        Me.Label8 = New System.Windows.Forms.Label()
        Me.TableLayoutPanel1.SuspendLayout()
        CType(Me.PictureBox1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'TableLayoutPanel1
        '
        Me.TableLayoutPanel1.ColumnCount = 2
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.0!))
        Me.TableLayoutPanel1.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.0!))
        Me.TableLayoutPanel1.Controls.Add(Me.OK_Button, 0, 0)
        Me.TableLayoutPanel1.Controls.Add(Me.Cancel_Button, 1, 0)
        Me.TableLayoutPanel1.Location = New System.Drawing.Point(228, 808)
        Me.TableLayoutPanel1.Margin = New System.Windows.Forms.Padding(6)
        Me.TableLayoutPanel1.Name = "TableLayoutPanel1"
        Me.TableLayoutPanel1.RowCount = 1
        Me.TableLayoutPanel1.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50.0!))
        Me.TableLayoutPanel1.Size = New System.Drawing.Size(498, 109)
        Me.TableLayoutPanel1.TabIndex = 0
        '
        'OK_Button
        '
        Me.OK_Button.Anchor = System.Windows.Forms.AnchorStyles.None
        Me.OK_Button.Location = New System.Drawing.Point(45, 32)
        Me.OK_Button.Margin = New System.Windows.Forms.Padding(6)
        Me.OK_Button.Name = "OK_Button"
        Me.OK_Button.Size = New System.Drawing.Size(158, 44)
        Me.OK_Button.TabIndex = 0
        Me.OK_Button.Text = "OK"
        '
        'Cancel_Button
        '
        Me.Cancel_Button.Anchor = System.Windows.Forms.AnchorStyles.None
        Me.Cancel_Button.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.Cancel_Button.Location = New System.Drawing.Point(306, 32)
        Me.Cancel_Button.Margin = New System.Windows.Forms.Padding(6)
        Me.Cancel_Button.Name = "Cancel_Button"
        Me.Cancel_Button.Size = New System.Drawing.Size(134, 44)
        Me.Cancel_Button.TabIndex = 1
        Me.Cancel_Button.Text = "Cancel"
        '
        'PictureBox1
        '
        Me.PictureBox1.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.PictureBox1.Cursor = System.Windows.Forms.Cursors.Hand
        Me.PictureBox1.Image = Global.ASCOM.Lumix.My.Resources.Resources.ASCOM
        Me.PictureBox1.Location = New System.Drawing.Point(534, 609)
        Me.PictureBox1.Margin = New System.Windows.Forms.Padding(6)
        Me.PictureBox1.Name = "PictureBox1"
        Me.PictureBox1.Size = New System.Drawing.Size(48, 56)
        Me.PictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize
        Me.PictureBox1.TabIndex = 2
        Me.PictureBox1.TabStop = False
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.Location = New System.Drawing.Point(80, 159)
        Me.Label3.Margin = New System.Windows.Forms.Padding(6, 0, 6, 0)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(217, 25)
        Me.Label3.TabIndex = 0
        Me.Label3.Text = "IP Address of camera"
        '
        'Label4
        '
        Me.Label4.AutoSize = True
        Me.Label4.Location = New System.Drawing.Point(80, 59)
        Me.Label4.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(507, 50)
        Me.Label4.TabIndex = 0
        Me.Label4.Text = "Set the Camera to Manual" & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & "Connect to the WIFI network in remote control mode"
        '
        'ButtonTemp
        '
        Me.ButtonTemp.AccessibleDescription = "this is where the files from the camera are stored (JPGor RAW) while trnasorming " &
    "to tiff and then passed on to the calling program as an imagearray. the files ar" &
    "e automatically deleted after each step"
        Me.ButtonTemp.Location = New System.Drawing.Point(84, 625)
        Me.ButtonTemp.Margin = New System.Windows.Forms.Padding(4, 6, 4, 6)
        Me.ButtonTemp.Name = "ButtonTemp"
        Me.ButtonTemp.Size = New System.Drawing.Size(267, 72)
        Me.ButtonTemp.TabIndex = 6
        Me.ButtonTemp.Text = "Path to temp folder"
        Me.ToolTip1.SetToolTip(Me.ButtonTemp, "this is where the files from the camera are stored (JPGor RAW) while trnasorming " &
        "to tiff and then passed on to the calling program as an imagearray. the files ar" &
        "e automatically deleted after each step")
        Me.ButtonTemp.UseVisualStyleBackColor = True
        '
        'ToolTip1
        '
        Me.ToolTip1.ToolTipTitle = "tooltip"
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(81, 384)
        Me.Label1.Margin = New System.Windows.Forms.Padding(6, 0, 6, 0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(47, 25)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "ISO"
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(80, 462)
        Me.Label2.Margin = New System.Windows.Forms.Padding(6, 0, 6, 0)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(149, 25)
        Me.Label2.TabIndex = 0
        Me.Label2.Text = "Shutter Speed"
        '
        'Label5
        '
        Me.Label5.AutoSize = True
        Me.Label5.Location = New System.Drawing.Point(80, 548)
        Me.Label5.Margin = New System.Windows.Forms.Padding(6, 0, 6, 0)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(159, 25)
        Me.Label5.TabIndex = 0
        Me.Label5.Text = "TransferFormat"
        '
        'OpenFileDialog1
        '
        Me.OpenFileDialog1.FileName = "dcraw-9.27-ms-64-bit.exe"
        Me.OpenFileDialog1.InitialDirectory = """C:\Users\robert.hasson\source\repos\LumixCamera\packages\NDCRaw.0.5.2\lib\net461" &
    "\"
        '
        'Label6
        '
        Me.Label6.AutoSize = True
        Me.Label6.Location = New System.Drawing.Point(80, 305)
        Me.Label6.Margin = New System.Windows.Forms.Padding(6, 0, 6, 0)
        Me.Label6.Name = "Label6"
        Me.Label6.Size = New System.Drawing.Size(114, 25)
        Me.Label6.TabIndex = 0
        Me.Label6.Text = "Resolution"
        '
        'CBResolution
        '
        Me.CBResolution.AllowDrop = True
        Me.CBResolution.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest
        Me.CBResolution.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems
        Me.CBResolution.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.Lumix.My.MySettings.Default, "Resolution", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.CBResolution.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.CBResolution.FormattingEnabled = True
        Me.CBResolution.ImeMode = System.Windows.Forms.ImeMode.Disable
        Me.CBResolution.Location = New System.Drawing.Point(324, 294)
        Me.CBResolution.Margin = New System.Windows.Forms.Padding(4, 6, 4, 6)
        Me.CBResolution.Name = "CBResolution"
        Me.CBResolution.Size = New System.Drawing.Size(361, 33)
        Me.CBResolution.TabIndex = 2
        Me.CBResolution.Text = Global.ASCOM.Lumix.My.MySettings.Default.Resolution
        '
        'TBTempPath
        '
        Me.TBTempPath.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.Lumix.My.MySettings.Default, "TempPath", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.TBTempPath.Location = New System.Drawing.Point(63, 745)
        Me.TBTempPath.Margin = New System.Windows.Forms.Padding(4, 6, 4, 6)
        Me.TBTempPath.Name = "TBTempPath"
        Me.TBTempPath.Size = New System.Drawing.Size(600, 31)
        Me.TBTempPath.TabIndex = 7
        Me.TBTempPath.Text = Global.ASCOM.Lumix.My.MySettings.Default.TempPath
        '
        'CBReadoutMode
        '
        Me.CBReadoutMode.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.Lumix.My.MySettings.Default, "TransferFormat", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.CBReadoutMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.CBReadoutMode.FormattingEnabled = True
        Me.CBReadoutMode.Items.AddRange(New Object() {"JPG", "RAW", "Thumb"})
        Me.CBReadoutMode.Location = New System.Drawing.Point(324, 538)
        Me.CBReadoutMode.Margin = New System.Windows.Forms.Padding(4, 6, 4, 6)
        Me.CBReadoutMode.Name = "CBReadoutMode"
        Me.CBReadoutMode.Size = New System.Drawing.Size(361, 33)
        Me.CBReadoutMode.TabIndex = 5
        Me.CBReadoutMode.Text = Global.ASCOM.Lumix.My.MySettings.Default.TransferFormat
        '
        'CBShutterSpeed
        '
        Me.CBShutterSpeed.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.Lumix.My.MySettings.Default, "Speed", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.CBShutterSpeed.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.CBShutterSpeed.FormattingEnabled = True
        Me.CBShutterSpeed.Location = New System.Drawing.Point(324, 452)
        Me.CBShutterSpeed.Margin = New System.Windows.Forms.Padding(4, 6, 4, 6)
        Me.CBShutterSpeed.Name = "CBShutterSpeed"
        Me.CBShutterSpeed.Size = New System.Drawing.Size(361, 33)
        Me.CBShutterSpeed.TabIndex = 4
        Me.CBShutterSpeed.Text = Global.ASCOM.Lumix.My.MySettings.Default.Speed
        '
        'CBISO
        '
        Me.CBISO.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.Lumix.My.MySettings.Default, "ISO", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.CBISO.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.CBISO.FormattingEnabled = True
        Me.CBISO.Location = New System.Drawing.Point(324, 373)
        Me.CBISO.Margin = New System.Windows.Forms.Padding(4, 6, 4, 6)
        Me.CBISO.Name = "CBISO"
        Me.CBISO.Size = New System.Drawing.Size(361, 33)
        Me.CBISO.TabIndex = 3
        Me.CBISO.Text = Global.ASCOM.Lumix.My.MySettings.Default.ISO
        '
        'CBCameraIPAddress
        '
        Me.CBCameraIPAddress.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.Lumix.My.MySettings.Default, "IPAddress", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.CBCameraIPAddress.FormattingEnabled = True
        Me.CBCameraIPAddress.Location = New System.Drawing.Point(324, 155)
        Me.CBCameraIPAddress.Margin = New System.Windows.Forms.Padding(4, 6, 4, 6)
        Me.CBCameraIPAddress.Name = "CBCameraIPAddress"
        Me.CBCameraIPAddress.Size = New System.Drawing.Size(361, 33)
        Me.CBCameraIPAddress.TabIndex = 1
        Me.CBCameraIPAddress.Text = Global.ASCOM.Lumix.My.MySettings.Default.IPAddress
        '
        'chkTrace
        '
        Me.chkTrace.AutoSize = True
        Me.chkTrace.Checked = Global.ASCOM.Lumix.My.MySettings.Default.TraceEnabled
        Me.chkTrace.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkTrace.DataBindings.Add(New System.Windows.Forms.Binding("Checked", Global.ASCOM.Lumix.My.MySettings.Default, "TraceEnabled", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.chkTrace.Location = New System.Drawing.Point(86, 853)
        Me.chkTrace.Margin = New System.Windows.Forms.Padding(6)
        Me.chkTrace.Name = "chkTrace"
        Me.chkTrace.Size = New System.Drawing.Size(129, 29)
        Me.chkTrace.TabIndex = 10
        Me.chkTrace.Text = "Trace on"
        Me.chkTrace.UseVisualStyleBackColor = True
        '
        'Label7
        '
        Me.Label7.AutoSize = True
        Me.Label7.Location = New System.Drawing.Point(80, 227)
        Me.Label7.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label7.Name = "Label7"
        Me.Label7.Size = New System.Drawing.Size(152, 25)
        Me.Label7.TabIndex = 11
        Me.Label7.Text = "Camera Model"
        '
        'Label8
        '
        Me.Label8.AutoSize = True
        Me.Label8.Location = New System.Drawing.Point(326, 227)
        Me.Label8.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label8.Name = "Label8"
        Me.Label8.Size = New System.Drawing.Size(199, 25)
        Me.Label8.TabIndex = 11
        Me.Label8.Text = "No Model found yet"
        '
        'SetupDialogForm
        '
        Me.AcceptButton = Me.OK_Button
        Me.AutoScaleDimensions = New System.Drawing.SizeF(12.0!, 25.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.AutoScroll = True
        Me.AutoSize = True
        Me.CancelButton = Me.Cancel_Button
        Me.ClientSize = New System.Drawing.Size(807, 978)
        Me.Controls.Add(Me.Label8)
        Me.Controls.Add(Me.Label7)
        Me.Controls.Add(Me.CBResolution)
        Me.Controls.Add(Me.TBTempPath)
        Me.Controls.Add(Me.CBReadoutMode)
        Me.Controls.Add(Me.CBShutterSpeed)
        Me.Controls.Add(Me.CBISO)
        Me.Controls.Add(Me.ButtonTemp)
        Me.Controls.Add(Me.Label4)
        Me.Controls.Add(Me.CBCameraIPAddress)
        Me.Controls.Add(Me.chkTrace)
        Me.Controls.Add(Me.Label5)
        Me.Controls.Add(Me.Label2)
        Me.Controls.Add(Me.Label6)
        Me.Controls.Add(Me.Label1)
        Me.Controls.Add(Me.Label3)
        Me.Controls.Add(Me.PictureBox1)
        Me.Controls.Add(Me.TableLayoutPanel1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.Margin = New System.Windows.Forms.Padding(6)
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "SetupDialogForm"
        Me.ShowInTaskbar = False
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "Lumix Setup Form"
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
    Friend WithEvents Label4 As Label
    Friend WithEvents ButtonTemp As Button
    Friend WithEvents OpenFileDialog1 As OpenFileDialog
    Friend WithEvents ToolTip1 As ToolTip
    Friend WithEvents CBISO As ComboBox
    Friend WithEvents CBShutterSpeed As ComboBox
    Friend WithEvents CBReadoutMode As ComboBox
    Private WithEvents Label1 As Label
    Private WithEvents Label2 As Label
    Private WithEvents Label5 As Label
    Friend WithEvents TBTempPath As TextBox
    Friend WithEvents FolderBrowserDialog1 As FolderBrowserDialog
    Friend WithEvents CBResolution As ComboBox
    Private WithEvents Label6 As Label
    Friend WithEvents Label7 As Label
    Friend WithEvents Label8 As Label
End Class

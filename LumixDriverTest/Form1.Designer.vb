<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form1
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
        Me.labelDriverId = New System.Windows.Forms.Label()
        Me.buttonConnect = New System.Windows.Forms.Button()
        Me.buttonChoose = New System.Windows.Forms.Button()
        Me.ButtonStartExpo = New System.Windows.Forms.Button()
        Me.ButtonStopExpo = New System.Windows.Forms.Button()
        Me.GetImageArray = New System.Windows.Forms.Button()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.CBSpeed = New System.Windows.Forms.ComboBox()
        Me.TestLibraw = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'labelDriverId
        '
        Me.labelDriverId.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.labelDriverId.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.Lumix.My.MySettings.Default, "DriverId", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.labelDriverId.Location = New System.Drawing.Point(32, 89)
        Me.labelDriverId.Margin = New System.Windows.Forms.Padding(8, 0, 8, 0)
        Me.labelDriverId.Name = "labelDriverId"
        Me.labelDriverId.Size = New System.Drawing.Size(772, 47)
        Me.labelDriverId.TabIndex = 5
        Me.labelDriverId.Text = Global.ASCOM.Lumix.My.MySettings.Default.DriverId
        Me.labelDriverId.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'buttonConnect
        '
        Me.buttonConnect.Location = New System.Drawing.Point(842, 85)
        Me.buttonConnect.Margin = New System.Windows.Forms.Padding(8)
        Me.buttonConnect.Name = "buttonConnect"
        Me.buttonConnect.Size = New System.Drawing.Size(192, 54)
        Me.buttonConnect.TabIndex = 4
        Me.buttonConnect.Text = "Connect"
        Me.buttonConnect.UseVisualStyleBackColor = True
        '
        'buttonChoose
        '
        Me.buttonChoose.Location = New System.Drawing.Point(842, 17)
        Me.buttonChoose.Margin = New System.Windows.Forms.Padding(8)
        Me.buttonChoose.Name = "buttonChoose"
        Me.buttonChoose.Size = New System.Drawing.Size(192, 54)
        Me.buttonChoose.TabIndex = 3
        Me.buttonChoose.Text = "Choose"
        Me.buttonChoose.UseVisualStyleBackColor = True
        '
        'ButtonStartExpo
        '
        Me.ButtonStartExpo.Location = New System.Drawing.Point(32, 260)
        Me.ButtonStartExpo.Margin = New System.Windows.Forms.Padding(6)
        Me.ButtonStartExpo.Name = "ButtonStartExpo"
        Me.ButtonStartExpo.Size = New System.Drawing.Size(246, 45)
        Me.ButtonStartExpo.TabIndex = 6
        Me.ButtonStartExpo.Text = "Start Exposure"
        Me.ButtonStartExpo.UseVisualStyleBackColor = True
        '
        'ButtonStopExpo
        '
        Me.ButtonStopExpo.Location = New System.Drawing.Point(32, 341)
        Me.ButtonStopExpo.Margin = New System.Windows.Forms.Padding(6)
        Me.ButtonStopExpo.Name = "ButtonStopExpo"
        Me.ButtonStopExpo.Size = New System.Drawing.Size(246, 45)
        Me.ButtonStopExpo.TabIndex = 6
        Me.ButtonStopExpo.Text = "Stop Exposure"
        Me.ButtonStopExpo.UseVisualStyleBackColor = True
        '
        'GetImageArray
        '
        Me.GetImageArray.Location = New System.Drawing.Point(784, 339)
        Me.GetImageArray.Margin = New System.Windows.Forms.Padding(6)
        Me.GetImageArray.Name = "GetImageArray"
        Me.GetImageArray.Size = New System.Drawing.Size(242, 62)
        Me.GetImageArray.TabIndex = 7
        Me.GetImageArray.Text = "GetImageArray"
        Me.GetImageArray.UseVisualStyleBackColor = True
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(26, 186)
        Me.Label1.Margin = New System.Windows.Forms.Padding(6, 0, 6, 0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(93, 32)
        Me.Label1.TabIndex = 9
        Me.Label1.Text = "speed"
        '
        'CBSpeed
        '
        Me.CBSpeed.FormattingEnabled = True
        Me.CBSpeed.Items.AddRange(New Object() {"4000", "3200", "2500", "2000", "1600", "1300", "1000", "800", "640", "500", "400", "320", "250", "200", "160", "125", "100", "80", "60", "50", "40", "30", "25", "20", "15", "13", "10", "8", "6", "5", "4", "3", "2.5", "2", "1.6", "1.3", "1"})
        Me.CBSpeed.Location = New System.Drawing.Point(166, 186)
        Me.CBSpeed.Margin = New System.Windows.Forms.Padding(6)
        Me.CBSpeed.Name = "CBSpeed"
        Me.CBSpeed.Size = New System.Drawing.Size(238, 39)
        Me.CBSpeed.TabIndex = 10
        '
        'TestLibraw
        '
        Me.TestLibraw.Location = New System.Drawing.Point(32, 523)
        Me.TestLibraw.Name = "TestLibraw"
        Me.TestLibraw.Size = New System.Drawing.Size(246, 47)
        Me.TestLibraw.TabIndex = 11
        Me.TestLibraw.Text = "TestLibRaw"
        Me.TestLibraw.UseVisualStyleBackColor = True
        '
        'Form1
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(16.0!, 31.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1066, 723)
        Me.Controls.Add(Me.TestLibraw)
        Me.Controls.Add(Me.CBSpeed)
        Me.Controls.Add(Me.Label1)
        Me.Controls.Add(Me.GetImageArray)
        Me.Controls.Add(Me.ButtonStopExpo)
        Me.Controls.Add(Me.ButtonStartExpo)
        Me.Controls.Add(Me.labelDriverId)
        Me.Controls.Add(Me.buttonConnect)
        Me.Controls.Add(Me.buttonChoose)
        Me.Margin = New System.Windows.Forms.Padding(8)
        Me.Name = "Form1"
        Me.Text = "Form1"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Private WithEvents labelDriverId As System.Windows.Forms.Label
    Private WithEvents buttonConnect As System.Windows.Forms.Button
    Private WithEvents buttonChoose As System.Windows.Forms.Button
    Friend WithEvents ButtonStartExpo As Button
    Friend WithEvents ButtonStopExpo As Button
    Friend WithEvents GetImageArray As Button
    Friend WithEvents Label1 As Label
    Friend WithEvents CBSpeed As ComboBox
    Friend WithEvents TestLibraw As Button
End Class

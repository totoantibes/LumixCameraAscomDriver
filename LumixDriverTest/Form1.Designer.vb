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
        Me.SuspendLayout()
        '
        'labelDriverId
        '
        Me.labelDriverId.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.labelDriverId.DataBindings.Add(New System.Windows.Forms.Binding("Text", Global.ASCOM.LumixG80.My.MySettings.Default, "DriverId", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.labelDriverId.Location = New System.Drawing.Point(16, 46)
        Me.labelDriverId.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.labelDriverId.Name = "labelDriverId"
        Me.labelDriverId.Size = New System.Drawing.Size(387, 25)
        Me.labelDriverId.TabIndex = 5
        Me.labelDriverId.Text = Global.ASCOM.LumixG80.My.MySettings.Default.DriverId
        Me.labelDriverId.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        '
        'buttonConnect
        '
        Me.buttonConnect.Location = New System.Drawing.Point(421, 44)
        Me.buttonConnect.Margin = New System.Windows.Forms.Padding(4)
        Me.buttonConnect.Name = "buttonConnect"
        Me.buttonConnect.Size = New System.Drawing.Size(96, 28)
        Me.buttonConnect.TabIndex = 4
        Me.buttonConnect.Text = "Connect"
        Me.buttonConnect.UseVisualStyleBackColor = True
        '
        'buttonChoose
        '
        Me.buttonChoose.Location = New System.Drawing.Point(421, 9)
        Me.buttonChoose.Margin = New System.Windows.Forms.Padding(4)
        Me.buttonChoose.Name = "buttonChoose"
        Me.buttonChoose.Size = New System.Drawing.Size(96, 28)
        Me.buttonChoose.TabIndex = 3
        Me.buttonChoose.Text = "Choose"
        Me.buttonChoose.UseVisualStyleBackColor = True
        '
        'ButtonStartExpo
        '
        Me.ButtonStartExpo.Location = New System.Drawing.Point(16, 134)
        Me.ButtonStartExpo.Name = "ButtonStartExpo"
        Me.ButtonStartExpo.Size = New System.Drawing.Size(123, 23)
        Me.ButtonStartExpo.TabIndex = 6
        Me.ButtonStartExpo.Text = "Start Exposure"
        Me.ButtonStartExpo.UseVisualStyleBackColor = True
        '
        'ButtonStopExpo
        '
        Me.ButtonStopExpo.Location = New System.Drawing.Point(16, 176)
        Me.ButtonStopExpo.Name = "ButtonStopExpo"
        Me.ButtonStopExpo.Size = New System.Drawing.Size(123, 23)
        Me.ButtonStopExpo.TabIndex = 6
        Me.ButtonStopExpo.Text = "Stop Exposure"
        Me.ButtonStopExpo.UseVisualStyleBackColor = True
        '
        'Form1
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(8.0!, 16.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(533, 229)
        Me.Controls.Add(Me.ButtonStopExpo)
        Me.Controls.Add(Me.ButtonStartExpo)
        Me.Controls.Add(Me.labelDriverId)
        Me.Controls.Add(Me.buttonConnect)
        Me.Controls.Add(Me.buttonChoose)
        Me.Margin = New System.Windows.Forms.Padding(4)
        Me.Name = "Form1"
        Me.Text = "Form1"
        Me.ResumeLayout(False)

    End Sub
    Private WithEvents labelDriverId As System.Windows.Forms.Label
    Private WithEvents buttonConnect As System.Windows.Forms.Button
    Private WithEvents buttonChoose As System.Windows.Forms.Button
    Friend WithEvents ButtonStartExpo As Button
    Friend WithEvents ButtonStopExpo As Button
End Class

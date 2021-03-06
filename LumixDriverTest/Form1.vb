﻿Imports System
Imports System.Runtime.InteropServices


Public Class Form1

    Private driver As ASCOM.DriverAccess.Camera

    ''' <summary>
    ''' This event is where the driver is choosen. The device ID will be saved in the settings.
    ''' </summary>
    ''' <param name="sender">The source of the event.</param>
    ''' <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
    Private Sub buttonChoose_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles buttonChoose.Click
        My.Settings.DriverId = ASCOM.DriverAccess.Camera.Choose(My.Settings.DriverId)
        SetUIState()
    End Sub

    ''' <summary>
    ''' Connects to the device to be tested.
    ''' </summary>
    ''' <param name="sender">The source of the event.</param>
    ''' <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
    Private Sub buttonConnect_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles buttonConnect.Click
        If (IsConnected) Then
            driver.Connected = False
        Else
            driver = New ASCOM.DriverAccess.Camera(My.Settings.DriverId)
            driver.Connected = True
            'CBSpeed.DataSource = New BindingSource(driver.)
        End If
        SetUIState()
    End Sub

    Private Sub Form1_FormClosing(ByVal sender As System.Object, ByVal e As System.Windows.Forms.FormClosingEventArgs) Handles MyBase.FormClosing
        If IsConnected Then
            driver.Connected = False
        End If
        ' the settings are saved automatically when this application is closed.
    End Sub

    ''' <summary>
    ''' Sets the state of the UI depending on the device state
    ''' </summary>
    Private Sub SetUIState()
        buttonConnect.Enabled = Not String.IsNullOrEmpty(My.Settings.DriverId)
        buttonChoose.Enabled = Not IsConnected
        buttonConnect.Text = IIf(IsConnected, "Disconnect", "Connect")
    End Sub

    ''' <summary>
    ''' Gets a value indicating whether this instance is connected.
    ''' </summary>
    ''' <value>
    ''' 
    ''' <c>true</c> if this instance is connected; otherwise, <c>false</c>.
    ''' 
    ''' </value>
    Private ReadOnly Property IsConnected() As Boolean
        Get
            If Me.driver Is Nothing Then Return False
            Return driver.Connected
        End Get
    End Property

    Private Sub labelDriverId_Click(sender As Object, e As EventArgs) Handles labelDriverId.Click

    End Sub

    Private Sub ButtonStopExpo_Click(sender As Object, e As EventArgs) Handles ButtonStopExpo.Click
        Me.driver.StopExposure()
    End Sub

    Private Sub ButtonStartExpo_Click(sender As Object, e As EventArgs) Handles ButtonStartExpo.Click
        If IsConnected Then

            Me.driver.StartExposure(0.5, True)

        Else
            MessageBox.Show("camera not connected")

        End If


    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles GetImageArray.Click
        If Me.driver.ImageReady() Then
            Dim currentimagearray(Me.driver.CameraXSize, Me.driver.CameraYSize) As Integer
            currentimagearray = Me.driver.ImageArray()
        End If
    End Sub


    ' Private Shared Function GetIpNetTable(pIpNetTable As IntPtr, <MarshalAs(UnmanagedType.U4)> ByRef pdwSize As Integer, bOrder As Boolean) As <MarshalAs(UnmanagedType.U4)> Integer


    <DllImport("C:\Users\robert.hasson\Downloads\LibRaw-0.19.5-Win32\LibRaw-0.19.5\bin\libraw.dll", ThrowOnUnmappableChar:=False, CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_init(flag As UInteger) As IntPtr

    End Function

    <DllImport("C:\Users\robert.hasson\Downloads\LibRaw-0.19.5-Win32\LibRaw-0.19.5\bin\libraw.dll", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_open_file(ByVal libraw_data As IntPtr, ByVal filename As String) As Int32

    End Function

    <DllImport("C:\Users\robert.hasson\Downloads\LibRaw-0.19.5-Win32\LibRaw-0.19.5\bin\libraw.dll", CallingConvention:=CallingConvention.Cdecl)>
    Public Shared Function libraw_dcraw_ppm_tiff_writer(ByVal libraw_data As IntPtr, ByVal outfile As String) As Int32
    End Function



    Private Sub TestLibraw_Click(sender As Object, e As EventArgs) Handles TestLibraw.Click
        Try
            Dim libraw_data_t As IntPtr
            libraw_data_t = libraw_init(1)
            libraw_open_file(libraw_data_t, "C:\Users\robert.hasson\source\repos\SharedProject2\P1229019.RW2")
            libraw_dcraw_ppm_tiff_writer(libraw_data_t, "C:\Users\robert.hasson\source\repos\SharedProject2\P1229019.tiff")
        Catch ef As exception

        End Try

    End Sub


    ' TODO: Add additional UI and controls to test more of the driver being tested.

End Class

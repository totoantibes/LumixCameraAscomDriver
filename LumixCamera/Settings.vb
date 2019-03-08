Imports System.Configuration
Namespace My

    'This class allows you to handle specific events on the settings class:
    ' The SettingChanging event is raised before a setting's value is changed.
    ' The PropertyChanged event is raised after a setting's value is changed.
    ' The SettingsLoaded event is raised after the setting values are loaded.
    ' The SettingsSaving event is raised before the setting values are saved.
    <SettingsProvider(GetType(SettingsProvider))>
    <DeviceId(Camera.driverID, DeviceName:=Camera.driverDescription)>
    Partial Friend NotInheritable Class MySettings
    End Class
End Namespace

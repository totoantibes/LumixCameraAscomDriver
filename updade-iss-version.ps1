# Get latest tag
$latestTag = git tag --sort=-v:refname | Select-Object -First 1
if ($latestTag -match '^v?(\d+)\.(\d+)\.(\d+)$') {
  $major = [int]$matches[1]
  $minor = [int]$matches[2] + 1
  $patch = 0
  $newTag = "$major.$minor.$patch"
} else {
  $newTag = "1.0.0"
}
git tag "v$newTag"
git push origin "v$newTag"

# Update AssemblyInfo.vb
$assemblyInfoPath = "LumixCamera\My Project\AssemblyInfo.vb"
(Get-Content $assemblyInfoPath) -replace 'AssemblyVersion\(".*"\)', "AssemblyVersion(`"$newTag`")" `
                                -replace 'AssemblyFileVersion\(".*"\)', "AssemblyFileVersion(`"$newTag`")" |
  Set-Content $assemblyInfoPath

# Update .iss files
$issFiles = @(
  "LumixCamera\ASCOM.Lumix.Camera Setup.iss",
  "LumixCamera\ASCOM.Lumix.Camera Setup32.iss"
)
foreach ($issFile in $issFiles) {
  (Get-Content $issFile) |
    ForEach-Object {
      if ($_ -match '^(AppVerName=.*?)(\d+\.\d+\.\d+)(.*)$') {
        "$($matches[1])$newTag$($matches[3])"
      } elseif ($_ -match '^(AppVersion\s*=\s*)(.+)$') {
        "$($matches[1])$newTag"
      } elseif ($_ -match '^(VersionInfoVersion\s*=\s*)(.+)$') {
        "$($matches[1])$newTag"
      } else {
        $_
      }
    } | Set-Content $issFile
}

# Build solution
msbuild "LumixCamera\LumixCamera.sln" /p:Configuration=Release /p:Platform="Any CPU"
msbuild "LumixCamera\LumixCamera.sln" /p:Configuration=Release /p:Platform="x86"

# Compile Inno Setup
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "LumixCamera\ASCOM.Lumix.Camera Setup.iss"
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "LumixCamera\ASCOM.Lumix.Camera Setup32.iss"

# Push changes and create release (manual step or use GitHub CLI)
git add .
git commit -m "Release $newTag"
git push
gh release create "v$newTag" LumixCamera\OutputDir\*.exe --title "Release $newTag"
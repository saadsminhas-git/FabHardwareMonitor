Option Explicit

' Immediate UI action: remember the checkbox even if the elevated
' execute process never sees INSTALLPAWNIO.
Function WritePawnIoFlag()
  On Error Resume Next
  Dim fso, sh, path, f
  Set fso = CreateObject("Scripting.FileSystemObject")
  Set sh = CreateObject("WScript.Shell")
  path = sh.ExpandEnvironmentStrings("%TEMP%\FabHwMon-install-pawnio.flag")
  Set f = fso.CreateTextFile(path, True)
  f.WriteLine "1"
  f.Close
  WritePawnIoFlag = 0
End Function

' Commit / deferred: CustomActionData is "setupPath;installFolder".
' Copies to %TEMP%\PawnIO_setup.exe so the official name is used, then
' runs -install -silent as SYSTEM after the MSI transaction commits.
Function InstallPawnIoDeferred()
  On Error Resume Next
  Dim fso, sh, logPath, dst, src, root, data, parts, rc, f
  Set fso = CreateObject("Scripting.FileSystemObject")
  Set sh = CreateObject("WScript.Shell")
  logPath = sh.ExpandEnvironmentStrings("%TEMP%\FabHwMon-PawnIO.log")
  dst = sh.ExpandEnvironmentStrings("%TEMP%\PawnIO_setup.exe")

  data = Session.Property("CustomActionData")
  LogMsg fso, logPath, "CustomActionData=" & data
  parts = Split(data, ";")
  src = ""
  root = ""
  If UBound(parts) >= 0 Then src = parts(0)
  If UBound(parts) >= 1 Then root = parts(1)

  If src = "" Or Not fso.FileExists(src) Then
    src = FindPawnIoExe(fso, root)
  End If
  LogMsg fso, logPath, "src=" & src

  If src = "" Then
    LogMsg fso, logPath, "PawnIO_setup.exe not found"
    InstallPawnIoDeferred = 0
    Exit Function
  End If

  WriteInstallDirFlag fso, root
  Set f = fso.CreateTextFile(sh.ExpandEnvironmentStrings("%TEMP%\FabHwMon-install-pawnio.flag"), True)
  If Err.Number = 0 Then
    f.WriteLine "1"
    f.Close
  End If
  Err.Clear

  fso.CopyFile src, dst, True
  If Err.Number <> 0 Then
    LogMsg fso, logPath, "copy failed " & Err.Number & " " & Err.Description
    dst = src
    Err.Clear
  End If

  rc = sh.Run("""" & dst & """ -install -silent", 0, True)
  LogMsg fso, logPath, "exit=" & rc & " err=" & Err.Number & " " & Err.Description
  InstallPawnIoDeferred = 0
End Function

Sub WriteInstallDirFlag(fso, root)
  On Error Resume Next
  Dim path, f
  If root = "" Then Exit Sub
  path = fso.BuildPath(root, "install-pawnio.flag")
  Set f = fso.CreateTextFile(path, True)
  If Err.Number = 0 Then
    f.WriteLine "1"
    f.Close
  End If
  Err.Clear
  path = fso.BuildPath(fso.BuildPath(root, "current"), "install-pawnio.flag")
  Set f = fso.CreateTextFile(path, True)
  If Err.Number = 0 Then
    f.WriteLine "1"
    f.Close
  End If
End Sub

Function FindPawnIoExe(fso, folder)
  Dim f, subf, p
  FindPawnIoExe = ""
  If folder = "" Then Exit Function
  If Not fso.FolderExists(folder) Then Exit Function
  p = fso.BuildPath(folder, "PawnIO_setup.exe")
  If fso.FileExists(p) Then
    FindPawnIoExe = p
    Exit Function
  End If
  For Each f In fso.GetFolder(folder).Files
    If LCase(f.Name) = "pawnio_setup.exe" Then
      FindPawnIoExe = f.Path
      Exit Function
    End If
  Next
  For Each subf In fso.GetFolder(folder).SubFolders
    p = FindPawnIoExe(fso, subf.Path)
    If p <> "" Then
      FindPawnIoExe = p
      Exit Function
    End If
  Next
End Function

' Runs as SYSTEM after Velopack/MSI have removed the install folder.
' Clears the logon task, roaming settings, leftover LocalAppData copies,
' shortcuts, and temp helper files. Does not uninstall PawnIO.
Function UninstallCleanup()
  On Error Resume Next
  Dim fso, sh, users, profile, name
  Set fso = CreateObject("Scripting.FileSystemObject")
  Set sh = CreateObject("WScript.Shell")

  sh.Run "taskkill /F /IM FabHardwareMonitor.exe", 0, True
  sh.Run "taskkill /F /IM ""Fab Hardware Monitor.exe""", 0, True
  sh.Run "schtasks.exe /Delete /F /TN FabHardwareMonitor", 0, True
  DeleteIfExists fso, sh.ExpandEnvironmentStrings("%SystemRoot%\System32\Tasks\FabHardwareMonitor")
  DeleteRunValues sh

  DeleteTempJunk fso, sh.ExpandEnvironmentStrings("%TEMP%")
  DeleteTempJunk fso, sh.ExpandEnvironmentStrings("%SystemRoot%\Temp")

  DeleteIfExists fso, sh.ExpandEnvironmentStrings("%Public%\Desktop\Fab Hardware Monitor.lnk")
  DeleteIfExists fso, sh.ExpandEnvironmentStrings("%ProgramData%\Microsoft\Windows\Start Menu\Programs\Fab Hardware Monitor.lnk")

  users = sh.ExpandEnvironmentStrings("%SystemDrive%\Users")
  If fso.FolderExists(users) Then
    For Each profile In fso.GetFolder(users).SubFolders
      name = LCase(profile.Name)
      If name <> "public" And name <> "default" And name <> "default user" And name <> "all users" And name <> "allusers" Then
        DeleteFolderIfExists fso, fso.BuildPath(profile.Path, "AppData\Roaming\FabHardwareMonitor")
        DeleteFolderIfExists fso, fso.BuildPath(profile.Path, "AppData\Local\FabHardwareMonitor")
        DeleteIfExists fso, fso.BuildPath(profile.Path, "Desktop\Fab Hardware Monitor.lnk")
        DeleteIfExists fso, fso.BuildPath(profile.Path, "AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Fab Hardware Monitor.lnk")
        DeleteTempJunk fso, fso.BuildPath(profile.Path, "AppData\Local\Temp")
      End If
    Next
  End If

  UninstallCleanup = 0
End Function

Sub DeleteRunValues(sh)
  On Error Resume Next
  Dim reg, sids, sid
  sh.RegDelete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run\FabHardwareMonitor"
  Set reg = GetObject("winmgmts:\\.\root\default:StdRegProv")
  If reg Is Nothing Then Exit Sub
  reg.EnumKey &H80000003, "", sids
  If IsArray(sids) Then
    For Each sid In sids
      reg.DeleteValue &H80000003, sid & "\Software\Microsoft\Windows\CurrentVersion\Run", "FabHardwareMonitor"
    Next
  End If
End Sub

Sub DeleteTempJunk(fso, folder)
  On Error Resume Next
  If folder = "" Then Exit Sub
  DeleteIfExists fso, fso.BuildPath(folder, "FabHwMon-install-pawnio.flag")
  DeleteIfExists fso, fso.BuildPath(folder, "FabHwMon-PawnIO.log")
  DeleteIfExists fso, fso.BuildPath(folder, "PawnIO_setup.exe")
End Sub

Sub DeleteIfExists(fso, path)
  On Error Resume Next
  If path = "" Then Exit Sub
  If fso.FileExists(path) Then fso.DeleteFile path, True
End Sub

Sub DeleteFolderIfExists(fso, path)
  On Error Resume Next
  If path = "" Then Exit Sub
  If fso.FolderExists(path) Then fso.DeleteFolder path, True
End Sub

Sub LogMsg(fso, path, msg)
  On Error Resume Next
  Dim f
  Set f = fso.OpenTextFile(path, 8, True)
  f.WriteLine Now & " " & msg
  f.Close
End Sub

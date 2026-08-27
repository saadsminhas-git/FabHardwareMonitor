Option Explicit
' Adds a PawnIO checkbox to VerifyReadyDlg and runs the bundled
' PawnIO_setup.exe after files are copied when the box is checked.
'
' cscript patch-msi-pawnio.vbs <msi> <PawnIO_setup.exe> [dialog.bmp] [banner.bmp] [app.ico]

If WScript.Arguments.Count < 2 Then
  WScript.Echo "usage: patch-msi-pawnio.vbs <msi> <PawnIO_setup.exe>"
  WScript.Quit 1
End If

Dim msiPath, installer, db, view, rec, pawnFileKey
msiPath = WScript.Arguments(0)

Set installer = CreateObject("WindowsInstaller.Installer")
Set db = installer.OpenDatabase(msiPath, 1)

Sub RunSql(sql)
  Dim v
  Set v = db.OpenView(sql)
  v.Execute
  v.Close
End Sub

Sub ReplaceStream(tableName, keyColumn, keyValue, streamPath)
  Dim v, row
  Set v = db.OpenView("SELECT `" & keyColumn & "`, `Data` FROM `" & tableName & "` WHERE `" & keyColumn & "`='" & keyValue & "'")
  v.Execute
  Set row = v.Fetch
  If row Is Nothing Then
    WScript.Echo "missing " & tableName & "." & keyValue
    WScript.Quit 1
  End If
  row.SetStream 2, streamPath
  v.Modify 2, row
  v.Close
End Sub

Sub UpsertControl(dialog, name, typeName, x, y, w, h, attrs, prop, text, nextCtl)
  Dim rec, v, existing
  Set v = db.OpenView("SELECT `Dialog_`, `Control`, `Type`, `X`, `Y`, `Width`, `Height`, `Attributes`, `Property`, `Text`, `Control_Next`, `Help` FROM `Control` WHERE `Dialog_`='" & dialog & "' AND `Control`='" & name & "'")
  v.Execute
  Set existing = v.Fetch
  If existing Is Nothing Then
    v.Close
    Set rec = installer.CreateRecord(12)
    rec.StringData(1) = dialog
    rec.StringData(2) = name
    rec.StringData(3) = typeName
    rec.IntegerData(4) = x
    rec.IntegerData(5) = y
    rec.IntegerData(6) = w
    rec.IntegerData(7) = h
    rec.IntegerData(8) = attrs
    rec.StringData(9) = prop
    rec.StringData(10) = text
    rec.StringData(11) = nextCtl
    rec.StringData(12) = ""
    Set v = db.OpenView("SELECT `Dialog_`, `Control`, `Type`, `X`, `Y`, `Width`, `Height`, `Attributes`, `Property`, `Text`, `Control_Next`, `Help` FROM `Control`")
    v.Execute
    v.Modify 1, rec
    If Err.Number <> 0 Then
      WScript.Echo "insert control " & name & " failed: " & Err.Description
      WScript.Quit 1
    End If
    v.Close
  Else
    existing.StringData(3) = typeName
    existing.IntegerData(4) = x
    existing.IntegerData(5) = y
    existing.IntegerData(6) = w
    existing.IntegerData(7) = h
    existing.IntegerData(8) = attrs
    existing.StringData(9) = prop
    existing.StringData(10) = text
    existing.StringData(11) = nextCtl
    v.Modify 2, existing
    v.Close
  End If
End Sub

On Error Resume Next
RunSql "INSERT INTO `Property` (`Property`, `Value`) VALUES ('ARPPRODUCTICON', 'appicon')"
If Err.Number <> 0 Then
  Err.Clear
  RunSql "UPDATE `Property` SET `Value`='appicon' WHERE `Property`='ARPPRODUCTICON'"
End If
Err.Clear

RunSql "INSERT INTO `Property` (`Property`, `Value`) VALUES ('INSTALLPAWNIO', '1')"
If Err.Number <> 0 Then Err.Clear

RunSql "INSERT INTO `CheckBox` (`Property`, `Value`) VALUES ('INSTALLPAWNIO', '1')"
If Err.Number <> 0 Then Err.Clear

' Leave room under the stock "Click Install..." line for the PawnIO explanation.
RunSql "UPDATE `Control` SET `Height`=32 WHERE `Dialog_`='VerifyReadyDlg' AND `Control`='InstallText'"
Err.Clear

Dim pawnWhy
pawnWhy = "CPU temperature needs PawnIO, a signed kernel driver that reads the processor sensors. Windows blocks older drivers such as WinRing0, so without PawnIO the widget shows -- for CPU temp." & vbCrLf & vbCrLf & "Network, CPU %, RAM, GPU, and GPU temperature still work if you skip it. You can install PawnIO later from Settings."
On Error GoTo 0
UpsertControl "VerifyReadyDlg", "PawnIoWhy", "Text", 25, 108, 320, 92, 2, "", pawnWhy, ""
UpsertControl "VerifyReadyDlg", "InstallPawnIo", "CheckBox", 25, 208, 330, 18, 3, "INSTALLPAWNIO", "Install PawnIO for CPU temperature", "Install"

On Error Resume Next
RunSql "UPDATE `Control` SET `Control_Next`='InstallPawnIo' WHERE `Dialog_`='VerifyReadyDlg' AND `Control`='BannerBitmap'"
Err.Clear

RunSql "INSERT INTO `ControlCondition` (`Dialog_`, `Control_`, `Action`, `Condition`) VALUES ('VerifyReadyDlg', 'InstallPawnIo', 'Hide', 'Installed')"
Err.Clear
RunSql "INSERT INTO `ControlCondition` (`Dialog_`, `Control_`, `Action`, `Condition`) VALUES ('VerifyReadyDlg', 'InstallPawnIo', 'Disable', 'Installed')"
Err.Clear
RunSql "INSERT INTO `ControlCondition` (`Dialog_`, `Control_`, `Action`, `Condition`) VALUES ('VerifyReadyDlg', 'PawnIoWhy', 'Hide', 'Installed')"
Err.Clear

On Error GoTo 0

' Elevated MSI drops public properties unless they are listed here, so a
' checked PawnIO box never reached the execute sequence.
Set view = db.OpenView("SELECT `Property`, `Value` FROM `Property` WHERE `Property`='SecureCustomProperties'")
view.Execute
Set rec = view.Fetch
If rec Is Nothing Then
  WScript.Echo "SecureCustomProperties missing"
  WScript.Quit 1
End If
If InStr(1, rec.StringData(2), "INSTALLPAWNIO", vbTextCompare) = 0 Then
  rec.StringData(2) = rec.StringData(2) & ";INSTALLPAWNIO"
  view.Modify 2, rec
End If
view.Close

pawnFileKey = ""
Set view = db.OpenView("SELECT `File`, `FileName` FROM `File`")
view.Execute
Set rec = view.Fetch
Do While Not rec Is Nothing
  If InStr(1, rec.StringData(2), "PawnIO_setup.exe", vbTextCompare) > 0 Then
    pawnFileKey = rec.StringData(1)
    Exit Do
  End If
  Set rec = view.Fetch
Loop
view.Close
If pawnFileKey = "" Then
  WScript.Echo "PawnIO_setup.exe is not in the File table"
  WScript.Quit 1
End If

Dim fso, vbsCaPath
Set fso = CreateObject("Scripting.FileSystemObject")
vbsCaPath = fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName), "InstallPawnIo.vbs")
If Not fso.FileExists(vbsCaPath) Then
  WScript.Echo "missing " & vbsCaPath
  WScript.Quit 1
End If

On Error Resume Next
Set rec = installer.CreateRecord(2)
rec.StringData(1) = "InstallPawnIoVbs"
rec.SetStream 2, vbsCaPath
Set view = db.OpenView("SELECT `Name`, `Data` FROM `Binary`")
view.Execute
view.Modify 1, rec
If Err.Number <> 0 Then
  Err.Clear
  view.Close
  ReplaceStream "Binary", "Name", "InstallPawnIoVbs", vbsCaPath
Else
  view.Close
End If
Err.Clear

' Remember the checkbox in the UI process (does not need elevation).
RunSql "INSERT INTO `CustomAction` (`Action`, `Type`, `Source`, `Target`) VALUES ('WritePawnIoFlag', 6, 'InstallPawnIoVbs', 'WritePawnIoFlag')"
If Err.Number <> 0 Then
  Err.Clear
  RunSql "UPDATE `CustomAction` SET `Type`=6, `Source`='InstallPawnIoVbs', `Target`='WritePawnIoFlag' WHERE `Action`='WritePawnIoFlag'"
End If
Err.Clear

Set view = db.OpenView("SELECT `Control` FROM `Control` WHERE `Dialog_`='VerifyReadyDlg'")
view.Execute
Set rec = view.Fetch
Do While Not rec Is Nothing
  If rec.StringData(1) = "Install" Or rec.StringData(1) = "InstallNoShield" Then
    RunSql "INSERT INTO `ControlEvent` (`Dialog_`, `Control_`, `Event`, `Argument`, `Condition`, `Ordering`) VALUES ('VerifyReadyDlg', '" & rec.StringData(1) & "', 'DoAction', 'WritePawnIoFlag', 'INSTALLPAWNIO=""1""', 1)"
    Err.Clear
  End If
  Set rec = view.Fetch
Loop
view.Close
Err.Clear

' Bake the installed setup path into the deferred script. Type 51 Source is
' the deferred action name so it becomes CustomActionData.
RunSql "INSERT INTO `CustomAction` (`Action`, `Type`, `Source`, `Target`) VALUES ('SetPawnIoData', 51, 'InstallPawnIoCommit', '[#" & pawnFileKey & "];[INSTALLFOLDER]')"
If Err.Number <> 0 Then
  Err.Clear
  RunSql "UPDATE `CustomAction` SET `Type`=51, `Source`='InstallPawnIoCommit', `Target`='[#" & pawnFileKey & "];[INSTALLFOLDER]' WHERE `Action`='SetPawnIoData'"
End If
Err.Clear

' Type 6 VBS from Binary + 64 continue + 256 commit + 1024 deferred + 2048 SYSTEM.
' Sequenced before InstallFinalize so MSI runs it *after* the transaction commits.
RunSql "INSERT INTO `CustomAction` (`Action`, `Type`, `Source`, `Target`) VALUES ('InstallPawnIoCommit', 3398, 'InstallPawnIoVbs', 'InstallPawnIoDeferred')"
If Err.Number <> 0 Then
  Err.Clear
  RunSql "UPDATE `CustomAction` SET `Type`=3398, `Source`='InstallPawnIoVbs', `Target`='InstallPawnIoDeferred' WHERE `Action`='InstallPawnIoCommit'"
End If
Err.Clear

RunSql "INSERT INTO `InstallExecuteSequence` (`Action`, `Condition`, `Sequence`) VALUES ('SetPawnIoData', 'INSTALLPAWNIO=""1"" AND NOT REMOVE', 4104)"
If Err.Number <> 0 Then
  Err.Clear
  RunSql "UPDATE `InstallExecuteSequence` SET `Sequence`=4104, `Condition`='INSTALLPAWNIO=""1"" AND NOT REMOVE' WHERE `Action`='SetPawnIoData'"
End If
Err.Clear

RunSql "INSERT INTO `InstallExecuteSequence` (`Action`, `Condition`, `Sequence`) VALUES ('InstallPawnIoCommit', 'INSTALLPAWNIO=""1"" AND NOT REMOVE', 4105)"
If Err.Number <> 0 Then
  Err.Clear
  RunSql "UPDATE `InstallExecuteSequence` SET `Sequence`=4105, `Condition`='INSTALLPAWNIO=""1"" AND NOT REMOVE' WHERE `Action`='InstallPawnIoCommit'"
End If
Err.Clear

' Type 6 VBS + 64 continue + 1024 deferred + 2048 SYSTEM. After Velopack
' RustCleanup so leftover LocalAppData copies can be removed safely.
RunSql "INSERT INTO `CustomAction` (`Action`, `Type`, `Source`, `Target`) VALUES ('UninstallCleanup', 3142, 'InstallPawnIoVbs', 'UninstallCleanup')"
If Err.Number <> 0 Then
  Err.Clear
  RunSql "UPDATE `CustomAction` SET `Type`=3142, `Source`='InstallPawnIoVbs', `Target`='UninstallCleanup' WHERE `Action`='UninstallCleanup'"
End If
Err.Clear

RunSql "INSERT INTO `InstallExecuteSequence` (`Action`, `Condition`, `Sequence`) VALUES ('UninstallCleanup', 'REMOVE=""ALL""', 3610)"
If Err.Number <> 0 Then
  Err.Clear
  RunSql "UPDATE `InstallExecuteSequence` SET `Sequence`=3610, `Condition`='REMOVE=""ALL""' WHERE `Action`='UninstallCleanup'"
End If
Err.Clear

On Error GoTo 0

If WScript.Arguments.Count >= 4 Then
  ReplaceStream "Binary", "Name", "WixUI_Bmp_Dialog", WScript.Arguments(2)
  ReplaceStream "Binary", "Name", "WixUI_Bmp_Banner", WScript.Arguments(3)
End If
If WScript.Arguments.Count >= 5 Then
  ReplaceStream "Icon", "Name", "appicon", WScript.Arguments(4)
End If

db.Commit
WScript.Echo "patched " & msiPath & " PawnIO file=" & pawnFileKey & " commit CA + UI flag + uninstall cleanup"

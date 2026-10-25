Imports System.IO
Public Class PageOtherTest

    Public Sub InitDownloadFolder() Handles Me.Loaded
        If String.IsNullOrWhiteSpace(TextDownloadFolder.Text) Then
            TextDownloadFolder.Text = PathPure.Value & "MyDownload\"
        End If
    End Sub

    ' 从直链推断一个安全的文件名
    Private Shared Function GetFileNameFromUrl(Url As String) As String
        Try
            Dim Plain = Url
            Dim q = Plain.IndexOf("?"c)
            If q >= 0 Then Plain = Plain.Substring(0, q)
            Dim h = Plain.IndexOf("#"c)
            If h >= 0 Then Plain = Plain.Substring(0, h)
            Dim name = Uri.UnescapeDataString(Plain.TrimEnd("/"c, "\"c))
            Dim slash = Math.Max(name.LastIndexOf("/"c), name.LastIndexOf("\"c))
            If slash >= 0 Then name = name.Substring(slash + 1)
            If String.IsNullOrWhiteSpace(name) Then name = "download"
            Return MakeValidFileName(name)
        Catch
            Return "download"
        End Try
    End Function

    Private Shared Function MakeValidFileName(Name As String) As String
        Dim invalid = Path.GetInvalidFileNameChars()
        Dim sb As New Text.StringBuilder
        For Each ch In Name
            If Array.IndexOf(invalid, ch) < 0 AndAlso ch <> ControlChars.Quote Then sb.Append(ch) Else sb.Append("_"c)
        Next
        Dim result = sb.ToString().Trim()
        If String.IsNullOrWhiteSpace(result) Then result = "download"
        Return result
    End Function

    Private Sub BtnSelectFolder_Click() Handles BtnSelectFolder.Click
        Using dlg As New System.Windows.Forms.FolderBrowserDialog
            Dim cur = TextDownloadFolder.Text.TrimEnd("\"c)
            If Directory.Exists(cur) Then dlg.SelectedPath = cur
            If dlg.ShowDialog() = System.Windows.Forms.DialogResult.OK Then
                TextDownloadFolder.Text = dlg.SelectedPath.TrimEnd("\"c) & "\"
            End If
        End Using
    End Sub

    Private Sub BtnDownloadOpen_Click() Handles BtnDownloadOpen.Click
        Dim folder = TextDownloadFolder.Text.Trim()
        Try
            If String.IsNullOrWhiteSpace(folder) Then folder = PathPure.Value & "MyDownload\"
            Directory.CreateDirectory(folder)
            System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(folder) With {.UseShellExecute = True})
        Catch ex As Exception
            Hint("无法打开文件夹：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnDownloadStart_Click() Handles BtnDownloadStart.Click
        Dim url = TextDownloadUrl.Text.Trim()
        Dim folder = TextDownloadFolder.Text.Trim()
        If String.IsNullOrWhiteSpace(url) Or Uri.IsWellFormedUriString(url, UriKind.Absolute) = False Then
            Hint("请填写有效的下载直链（http/https）。", HintType.Red)
            Exit Sub
        End If
        If String.IsNullOrWhiteSpace(folder) Then folder = PathPure.Value & "MyDownload\"
        If folder.EndsWith("\"c) = False Then folder &= "\"
        Dim name = TextDownloadName.Text.Trim()
        If String.IsNullOrWhiteSpace(name) Then name = GetFileNameFromUrl(url) Else name = MakeValidFileName(name)
        Dim local = folder & name
        BtnDownloadStart.IsEnabled = False
        RunInThread(Sub()
                       Try
                           Directory.CreateDirectory(folder)
                           NetDownloadByLoader(url, local)
                           Hint("下载完成：" & name, HintType.Green)
                       Catch ex As Exception
                           Hint("下载失败：" & ex.Message, HintType.Red)
                       Finally
                           RunInUi(Sub() BtnDownloadStart.IsEnabled = True)
                       End Try
                   End Sub)
    End Sub

    ''' <summary>
    ''' 事件系统 / 自定义下载调用：把直链下载到指定文件夹（不给文件夹则用 KCL\MyDownload）。
    ''' </summary>
    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        If String.IsNullOrWhiteSpace(Url) Then
            Hint("下载链接为空。", HintType.Red)
            Exit Sub
        End If
        If String.IsNullOrWhiteSpace(Folder) Then Folder = PathPure.Value & "MyDownload\"
        If Folder.EndsWith("\"c) = False Then Folder &= "\"
        If String.IsNullOrWhiteSpace(FileName) Then FileName = "未知文件"
        Dim local = Folder & MakeValidFileName(FileName)
        RunInThread(Sub()
                       Try
                           Directory.CreateDirectory(Folder)
                           NetDownloadByLoader(Url, local)
                           Hint("已下载：" & FileName, HintType.Green)
                       Catch ex As Exception
                           Hint("下载失败：" & ex.Message, HintType.Red)
                       End Try
                   End Sub)
    End Sub

    Public Shared Sub Jrrp()
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub RubbishClear()
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        If ShowHint Then Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        If ShowHint Then Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Function GetRandomCave() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function
    Public Shared Function GetRandomHint() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function
    Public Shared Function GetRandomPresetHint() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function

End Class

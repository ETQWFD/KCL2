Imports System.IO
Imports System.Diagnostics

Public Class PageDownloadBedrock

    Private Shadows IsLoaded As Boolean = False
    Private ReadOnly EntryMap As New Dictionary(Of String, ModBedrock.BedrockEntry)

    Private Sub PageLoaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PanBack.ScrollToHome()
        If IsLoaded Then Return
        IsLoaded = True

        For Each Item As ModBedrock.BedrockEntry In ModBedrock.Entries()
            EntryMap(Item.Id) = Item
        Next

        TextRoot.Text = "安装目录：" & ModBedrock.BedrockRoot
        SetMeta(Meta1, "bedrock-2665-gdk")
        SetMeta(Meta2, "bedrock-121114-x64")
        SetMeta(Meta3, "bedrock-121114-x86")
        SetMeta(Meta4, "edu-2633-uwp")
        SetMeta(Meta5, "edu-2633-win32")
        RefreshStatus()
    End Sub

    Private Sub SetMeta(Block As TextBlock, Id As String)
        Dim E As ModBedrock.BedrockEntry = EntryMap(Id)
        Dim Parts As New List(Of String) From {
            E.Version, E.BuildType, E.PackageType.ToUpperInvariant(), E.Arch, E.SizeText, E.DateText}
        If E.Md5 <> "" Then Parts.Add("MD5 " & E.Md5)
        Block.Text = String.Join("  ·  ", Parts) & If(E.Note = "", "", vbCrLf & E.Note)
    End Sub

    Private Function StatusBlock(Id As String) As TextBlock
        Select Case Id
            Case "bedrock-2665-gdk" : Return Status1
            Case "bedrock-121114-x64" : Return Status2
            Case "bedrock-121114-x86" : Return Status3
            Case "edu-2633-uwp" : Return Status4
            Case "edu-2633-win32" : Return Status5
            Case Else : Return Nothing
        End Select
    End Function

    Private Sub RefreshStatus()
        For Each E As ModBedrock.BedrockEntry In EntryMap.Values
            Dim Block As TextBlock = StatusBlock(E.Id)
            If Block Is Nothing Then Continue For
            Dim Installed As Boolean = ModBedrock.IsInstalled(E)
            Dim Pkg As String = ModBedrock.FindPackage(E)
            Dim Text As String
            If E.PackageType = "appx" Then
                Dim Ver As String = ModBedrock.GetUwpVersion(E.PackageFamilyName)
                Text = If(Ver <> "", "状态：已安装（系统包版本 " & Ver & "）", "状态：未安装")
            Else
                Text = If(Installed, "状态：已安装（" & ModBedrock.FindGameExe(E) & "）", "状态：未安装")
            End If
            Text &= If(Pkg Is Nothing, "；本地没有安装包", "；安装包：" & Path.GetFileName(Pkg) & "（" & Math.Round(New FileInfo(Pkg).Length / 1024.0 / 1024.0, 1) & " MB）")
            Block.Text = Text
        Next
    End Sub

    Private Function EntryFromSender(Sender As Object) As ModBedrock.BedrockEntry
        Dim B As FrameworkElement = TryCast(Sender, FrameworkElement)
        If B Is Nothing OrElse B.Tag Is Nothing Then Return Nothing
        Dim Id As String = B.Tag.ToString()
        If EntryMap.ContainsKey(Id) Then Return EntryMap(Id)
        Return Nothing
    End Function

    Private Sub BtnRefresh_Click() Handles BtnRefresh.Click
        RefreshStatus()
        Hint("状态已刷新。", HintType.Blue)
    End Sub

    Private Sub BtnOpenRoot_Click() Handles BtnOpenRoot.Click
        Try
            Directory.CreateDirectory(ModBedrock.BedrockRoot)
            Process.Start(New ProcessStartInfo(ModBedrock.BedrockRoot) With {.UseShellExecute = True})
        Catch Ex As Exception
            Hint("无法打开目录：" & Ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnOpenPage_Click(Sender As Object, Args As RoutedEventArgs)
        Dim E As ModBedrock.BedrockEntry = EntryFromSender(Sender)
        If E Is Nothing Then Return
        Try
            Process.Start(New ProcessStartInfo(E.PageUrl) With {.UseShellExecute = True})
        Catch Ex As Exception
            Hint("无法打开下载页：" & Ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnOpenMirror_Click(Sender As Object, Args As RoutedEventArgs)
        Dim E As ModBedrock.BedrockEntry = EntryFromSender(Sender)
        If E Is Nothing Then Return
        If E.MirrorUrl = "" Then
            Hint("这一版没有备用网盘链接，请用下载页。", HintType.Red)
            Return
        End If
        Try
            OpenWebsite(E.MirrorUrl)
        Catch Ex As Exception
            Hint("无法打开网盘链接：" & Ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnFolder_Click(Sender As Object, Args As RoutedEventArgs)
        Dim E As ModBedrock.BedrockEntry = EntryFromSender(Sender)
        If E Is Nothing Then Return
        ModBedrock.OpenFolder(E)
    End Sub

    Private Sub BtnLaunch_Click(Sender As Object, Args As RoutedEventArgs)
        Dim E As ModBedrock.BedrockEntry = EntryFromSender(Sender)
        If E Is Nothing Then Return
        Dim Err As String = ModBedrock.Launch(E)
        If Err = "" Then Hint("已启动：" & E.Name, HintType.Green) Else Hint(Err, HintType.Red)
        RefreshStatus()
    End Sub

    Private Sub BtnUninstall_Click(Sender As Object, Args As RoutedEventArgs)
        Dim E As ModBedrock.BedrockEntry = EntryFromSender(Sender)
        If E Is Nothing Then Return
        If MyMsgBox("确定要卸载 " & E.Name & " 吗？", "卸载确认", "卸载", "取消") <> 1 Then Return
        Dim Err As String = ModBedrock.Uninstall(E)
        If Err = "" Then Hint("已卸载。", HintType.Green) Else Hint(Err, HintType.Red)
        RefreshStatus()
    End Sub

    Private Sub BtnImport_Click(Sender As Object, Args As RoutedEventArgs)
        Dim E As ModBedrock.BedrockEntry = EntryFromSender(Sender)
        If E Is Nothing Then Return
        Using Dlg As New System.Windows.Forms.OpenFileDialog
            Dlg.Title = "选择已下载的安装包"
            Dlg.Filter = "安装包|*.appx;*.appxbundle;*.msix;*.msixbundle;*.exe|所有文件|*.*"
            If Dlg.ShowDialog() <> System.Windows.Forms.DialogResult.OK Then Return
            Dim Src As String = Dlg.FileName
            Dim Folder As String = ModBedrock.EntryFolder(E)
            Dim Dst As String = Folder & Path.GetFileName(Src)
            Hint("开始安装 " & E.Name & "，大包可能需要几分钟……", HintType.Blue)
            RunInThread(Sub()
                           Try
                               Directory.CreateDirectory(Folder)
                               If Not String.Equals(Path.GetFullPath(Src), Path.GetFullPath(Dst), StringComparison.OrdinalIgnoreCase) Then
                                   File.Copy(Src, Dst, True)
                               End If
                               Dim Err As String = ModBedrock.InstallPackage(E, Dst)
                               RunInUi(Sub()
                                           If Err = "" Then
                                               Hint("安装完成：" & E.Name, HintType.Green)
                                           Else
                                               Hint(Err, HintType.Red)
                                           End If
                                           RefreshStatus()
                                       End Sub)
                           Catch Ex As Exception
                               RunInUi(Sub() Hint("安装失败：" & Ex.Message, HintType.Red))
                           End Try
                       End Sub)
        End Using
    End Sub

    Private Sub BtnInstallAgain_Click(Sender As Object, Args As RoutedEventArgs)
        Dim E As ModBedrock.BedrockEntry = EntryFromSender(Sender)
        If E Is Nothing Then Return
        Dim Pkg As String = ModBedrock.FindPackage(E)
        If Pkg Is Nothing Then
            Hint("这一版还没有本地安装包，先用「下载页」下载，或者用「导入安装包」选一个。", HintType.Red)
            Return
        End If
        If MyMsgBox("用 " & Path.GetFileName(Pkg) & " 重新安装 " & E.Name & "？", "重新安装", "安装", "取消") <> 1 Then Return
        Hint("开始安装，大包可能需要几分钟……", HintType.Blue)
        RunInThread(Sub()
                       Dim Err As String = ModBedrock.InstallPackage(E, Pkg)
                       RunInUi(Sub()
                                   If Err = "" Then Hint("安装完成：" & E.Name, HintType.Green) Else Hint(Err, HintType.Red)
                                   RefreshStatus()
                               End Sub)
                   End Sub)
    End Sub

End Class

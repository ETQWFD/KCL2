Imports System.IO
Imports System.Linq
Imports System.Diagnostics
Imports System.Text

''' <summary>
''' 基岩版 / 教育版的安装与启动支持。
''' 安装包统一放在 Minecraft 根目录下的 bedrock\ 里，和 Java 版本共用同一个根目录。
''' UWP（Appx）走系统包注册，GDK / Win32 走安装器的静默安装（/DIR 指到 bedrock 子目录）。
''' </summary>
Public Module ModBedrock

    Public Class BedrockEntry
        Public Id As String = ""
        Public Name As String = ""
        Public Version As String = ""
        ''' <summary>GDK / UWP / Win32。</summary>
        Public BuildType As String = ""
        ''' <summary>exe / appx。</summary>
        Public PackageType As String = ""
        Public SizeText As String = ""
        Public DateText As String = ""
        Public Arch As String = "x64"
        Public Md5 As String = ""
        Public PageUrl As String = ""
        Public DirectUrl As String = ""
        ''' <summary>123 云盘镜像（不经过 Cloudflare，可当备用通道）。</summary>
        Public MirrorUrl As String = ""
        ''' <summary>UWP 的包族名，例如 Microsoft.MinecraftUWP_8wekyb3d8bbwe。</summary>
        Public PackageFamilyName As String = ""
        ''' <summary>UWP 的 AUMID，例如 Microsoft.MinecraftUWP_8wekyb3d8bbwe!App。</summary>
        Public AppUserModelId As String = ""
        ''' <summary>优先查找的可执行文件名。</summary>
        Public ExpectedExe As String = ""
        Public Note As String = ""
    End Class

    ''' <summary>
    ''' 全部可安装的条目（版本信息取自 MCAPPX 版本库，2026-11-25）。
    ''' </summary>
    Public Function Entries() As List(Of BedrockEntry)
        Dim L As New List(Of BedrockEntry)

        L.Add(New BedrockEntry With {
            .Id = "bedrock-2665-gdk", .Name = "基岩版 26.65（GDK 安装包）", .Version = "26.65",
            .BuildType = "GDK", .PackageType = "exe", .SizeText = "875.71 MB", .DateText = "2026-11-16",
            .Arch = "x64", .Md5 = "252fa99c2e9795728da32bdc1ffe7780",
            .PageUrl = "https://www.mcappx.com/bedrock/26.60/26.65/",
            .MirrorUrl = "https://1817430.share.123pan.cn/123pan/gZD9-0Te7v",
            .DirectUrl = "https://dl.mcappx.com/be-1-26-6501-x64-jp",
            .ExpectedExe = "Minecraft.Windows.exe",
            .Note = "安装时可指定目录，装完直接点启动；需要 Xbox Gaming Services。"})

        L.Add(New BedrockEntry With {
            .Id = "bedrock-121114-x64", .Name = "基岩版 1.21.114（UWP / 64 位）", .Version = "1.21.114",
            .BuildType = "UWP", .PackageType = "appx", .SizeText = "886.76 MB", .DateText = "2025-10-16",
            .Arch = "x64", .Md5 = "b181ac1d6608e5cf6049518cd9701a7e",
            .PageUrl = "https://www.mcappx.com/bedrock/1.21/1.21.114/",
            .MirrorUrl = "https://1817430.share.123pan.cn/123pan/gZD9-G73lv",
            .DirectUrl = "https://dl.mcappx.com/be-1-21-11401-x64-jp",
            .PackageFamilyName = "Microsoft.MinecraftUWP_8wekyb3d8bbwe",
            .AppUserModelId = "Microsoft.MinecraftUWP_8wekyb3d8bbwe!App",
            .Note = "Appx 为系统级注册，安装包本体仍保存在 bedrock 目录。"})

        L.Add(New BedrockEntry With {
            .Id = "bedrock-121114-x86", .Name = "基岩版 1.21.114（UWP / 32 位）", .Version = "1.21.114",
            .BuildType = "UWP", .PackageType = "appx", .SizeText = "859.82 MB", .DateText = "2025-10-16",
            .Arch = "x86", .Md5 = "75664f682c4dceda7f498d1827ac9249",
            .PageUrl = "https://www.mcappx.com/bedrock/1.21/1.21.114/",
            .MirrorUrl = "https://1817430.share.123pan.cn/123pan/gZD9-aJ3Sv",
            .DirectUrl = "https://dl.mcappx.com/be-1-21-11401-x86-jp",
            .PackageFamilyName = "Microsoft.MinecraftUWP_8wekyb3d8bbwe",
            .AppUserModelId = "Microsoft.MinecraftUWP_8wekyb3d8bbwe!App",
            .Note = "仅 32 位系统或 x86 环境需要。"})

        L.Add(New BedrockEntry With {
            .Id = "edu-2633-uwp", .Name = "教育版 26.33（UWP）", .Version = "26.33",
            .BuildType = "UWP", .PackageType = "appx", .SizeText = "1.18 GB", .DateText = "2026-10-28",
            .Arch = "x64", .Md5 = "",
            .PageUrl = "https://www.mcappx.com/education-uwp/26.30/26.33/",
            .MirrorUrl = "https://1817430.share.123pan.cn/123pan/gZD9-IZ67v",
            .DirectUrl = "https://dl.mcappx.com/uwpee-1-26-3300-x64-jp",
            .PackageFamilyName = "Microsoft.MinecraftEducationEdition_8wekyb3d8bbwe",
            .AppUserModelId = "Microsoft.MinecraftEducationEdition_8wekyb3d8bbwe!App",
            .Note = "完整功能需要学校 / 机构的教育账号。"})

        L.Add(New BedrockEntry With {
            .Id = "edu-2633-win32", .Name = "教育版 26.33（Win32 安装包）", .Version = "26.33",
            .BuildType = "Win32", .PackageType = "exe", .SizeText = "1.16 GB", .DateText = "2026-10-28",
            .Arch = "x64", .Md5 = "",
            .PageUrl = "https://www.mcappx.com/education-win32/26.30/26.33/",
            .MirrorUrl = "https://1817430.share.123pan.cn/123pan/gZD9-8gN7v",
            .DirectUrl = "https://dl.mcappx.com/win32ee-1-26-3300-x64-jp",
            .ExpectedExe = "MinecraftEducationEdition.exe",
            .Note = "桌面安装包，可装到 bedrock 子目录；需要教育账号。"})

        Return L
    End Function

    ''' <summary>基岩版根目录，和 Java 版本共用 McFolderSelected。</summary>
    Public ReadOnly Property BedrockRoot As String
        Get
            Return McFolderSelected & "bedrock\"
        End Get
    End Property

    Public Function EntryFolder(Entry As BedrockEntry) As String
        Return BedrockRoot & Entry.Id & "\"
    End Function

    ''' <summary>在条目目录里找已下载的安装包。</summary>
    Public Function FindPackage(Entry As BedrockEntry) As String
        Dim Folder As String = EntryFolder(Entry)
        If Not Directory.Exists(Folder) Then Return Nothing
        Try
            Dim Pattern As String = If(Entry.PackageType = "appx", "*.appx", "*.exe")
            Dim Files = Directory.GetFiles(Folder, Pattern)
            If Entry.PackageType <> "appx" Then
                ' 装完之后目录里会多出 unins000.exe 之类，别把卸载器当安装包
                Files = Files.Where(Function(F) Not Path.GetFileName(F).ToLowerInvariant().StartsWith("unins") AndAlso New FileInfo(F).Length > 10 * 1024 * 1024).ToArray()
            End If
            If Files.Length = 0 AndAlso Entry.PackageType = "appx" Then Files = Directory.GetFiles(Folder, "*.msix")
            If Files.Length = 0 Then Return Nothing
            Array.Sort(Files, Function(A, B) File.GetLastWriteTimeUtc(B).CompareTo(File.GetLastWriteTimeUtc(A)))
            Return Files(0)
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>在安装目录里找游戏可执行文件。</summary>
    Public Function FindGameExe(Entry As BedrockEntry) As String
        Dim Folder As String = EntryFolder(Entry)
        If Not Directory.Exists(Folder) Then Return Nothing
        Dim Preferred As String = Folder & Entry.ExpectedExe
        If Entry.ExpectedExe <> "" AndAlso File.Exists(Preferred) Then Return Preferred
        Try
            For Each F As String In Directory.GetFiles(Folder, "*.exe", SearchOption.AllDirectories)
                Dim N As String = Path.GetFileName(F).ToLowerInvariant()
                If N.StartsWith("unins") OrElse N.Contains("installer") OrElse N.Contains("setup") OrElse N.Contains("vcredist") OrElse N.Contains("gameinput") Then Continue For
                If N.Contains("minecraft") Then Return F
            Next
        Catch
        End Try
        ' 兜底：安装器没按 /DIR 落地时，去系统默认位置找一次
        For Each Root As String In {Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) & "\Programs"}
            Try
                If Not Directory.Exists(Root) Then Continue For
                For Each D As String In Directory.GetDirectories(Root)
                    If Not Path.GetFileName(D).ToLowerInvariant().Contains("minecraft") Then Continue For
                    Dim F As String = Path.Combine(D, "Minecraft.Windows.exe")
                    If File.Exists(F) Then Return F
                Next
            Catch
            End Try
        Next
        Return Nothing
    End Function

    ''' <summary>查询 UWP 包是否已安装，返回版本号；未安装返回空字符串。</summary>
    Public Function GetUwpVersionCached(PackageFamilyName As String) As String
        If PackageFamilyName = "" Then Return ""
        If UwpVersionCache.ContainsKey(PackageFamilyName) Then Return UwpVersionCache(PackageFamilyName)
        Dim V As String = GetUwpVersion(PackageFamilyName)
        UwpVersionCache(PackageFamilyName) = V
        Return V
    End Function

    Public Function GetUwpVersion(PackageFamilyName As String) As String
        If String.IsNullOrWhiteSpace(PackageFamilyName) Then Return ""
        Try
            Dim Cmd As String = "(Get-AppxPackage -Name '" & PackageFamilyName.Split("_"c)(0) & "' | Select-Object -First 1).Version"
            Dim Psi As New ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command """ & Cmd & """") With {
                .UseShellExecute = False, .CreateNoWindow = True,
                .RedirectStandardOutput = True, .RedirectStandardError = True}
            Using P As Process = Process.Start(Psi)
                Dim Out As String = P.StandardOutput.ReadToEnd()
                P.WaitForExit(8000)
                Return Out.Trim()
            End Using
        Catch
            Return ""
        End Try
    End Function

    Private ReadOnly UwpVersionCache As New Dictionary(Of String, String)

    ''' <summary>安装 / 卸载后清空缓存，让下一次检测拿到真实状态。</summary>
    Public Sub InvalidateCache()
        UwpVersionCache.Clear()
    End Sub

    Public Function IsInstalled(Entry As BedrockEntry) As Boolean
        If Entry.PackageType = "appx" Then Return GetUwpVersionCached(Entry.PackageFamilyName) <> ""
        Return FindGameExe(Entry) IsNot Nothing
    End Function

    ''' <summary>用 Appx 安装（不对商店做任何改动，只是本地包注册）。</summary>
    Public Function InstallAppx(PackagePath As String) As String
        Try
            Dim Cmd As String = "Add-AppxPackage -Path '" & PackagePath.Replace("'", "''") & "' -ForceApplicationShutdown -ErrorAction Stop"
            Dim Psi As New ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command """ & Cmd & """") With {
                .UseShellExecute = False, .CreateNoWindow = True,
                .RedirectStandardOutput = True, .RedirectStandardError = True}
            Using P As Process = Process.Start(Psi)
                Dim ErrText As String = P.StandardError.ReadToEnd()
                P.WaitForExit()
                If P.ExitCode = 0 Then Return ""
                Return "Appx 安装失败：" & ErrText.Trim()
            End Using
        Catch Ex As Exception
            Return "Appx 安装失败：" & Ex.Message
        End Try
    End Function

    ''' <summary>执行安装器的静默安装，把游戏装到 bedrock 子目录。</summary>
    Public Function InstallExe(Entry As BedrockEntry, InstallerPath As String) As String
        Dim Target As String = EntryFolder(Entry).TrimEnd("\"c)
        Directory.CreateDirectory(Target)
        Try
            ' mcappx 的 GDK / Win32 安装器基于 Inno Setup，支持 /VERYSILENT 与 /DIR
            Dim Args As String = "/VERYSILENT /DIR=""" & Target & """ /NORESTART /LOG=""" & Target & "\install.log"""
            Dim Psi As New ProcessStartInfo(InstallerPath, Args) With {
                .UseShellExecute = False, .WorkingDirectory = Path.GetDirectoryName(InstallerPath)}
            Using P As Process = Process.Start(Psi)
                P.WaitForExit()
                If P.ExitCode = 0 Then Return ""
                Return "安装器返回错误码 " & P.ExitCode & "，详见 " & Target & "\install.log"
            End Using
        Catch Ex As Exception
            Return "安装失败：" & Ex.Message
        End Try
    End Function

    ''' <summary>按扩展名分派安装。</summary>
    Public Function InstallPackage(Entry As BedrockEntry, PackagePath As String) As String
        If Not File.Exists(PackagePath) Then Return "文件不存在：" & PackagePath
        InvalidateCache()
        Dim Ext As String = Path.GetExtension(PackagePath).ToLowerInvariant()
        Select Case Ext
            Case ".appx", ".appxbundle", ".msix", ".msixbundle"
                Return InstallAppx(PackagePath)
            Case ".exe"
                Dim Err As String = InstallExe(Entry, PackagePath)
                If Err = "" AndAlso FindGameExe(Entry) = "" Then
                    Hint("安装器已完成，但 bedrock 目录里没找到游戏主程序，可能装进了系统默认位置；点「启动」会让 KCL 再找一次。", HintType.Blue)
                End If
                Return Err
            Case ".msixvc"
                Return "msixvc 不能直接安装：请使用 mcappx 的 GDK 安装器（下载页里带 exe 的那个包）。"
            Case Else
                Return "不支持的安装包格式：" & Ext
        End Select
    End Function

    ''' <summary>启动游戏。</summary>
    Public Function Launch(Entry As BedrockEntry) As String
        Try
            If Entry.PackageType = "appx" Then
                If GetUwpVersion(Entry.PackageFamilyName) = "" Then Return "还没有安装，先安装 Appx。"
                Process.Start(New ProcessStartInfo("explorer.exe", "shell:AppsFolder\" & Entry.AppUserModelId) With {.UseShellExecute = True})
                Return ""
            End If
            Dim Exe As String = FindGameExe(Entry)
            If Exe Is Nothing Then Return "没有找到游戏可执行文件，可能还没安装完成。"
            Process.Start(New ProcessStartInfo(Exe) With {.WorkingDirectory = Path.GetDirectoryName(Exe), .UseShellExecute = True})
            Return ""
        Catch Ex As Exception
            Return "启动失败：" & Ex.Message
        End Try
    End Function

    ''' <summary>卸载 UWP 包（exe 版只提示手动删除目录）。</summary>
    Public Function Uninstall(Entry As BedrockEntry) As String
        Try
            If Entry.PackageType = "appx" Then
                Dim Cmd As String = "Get-AppxPackage -Name '" & Entry.PackageFamilyName.Split("_"c)(0) & "' | Remove-AppxPackage -ErrorAction Stop"
                Dim Psi As New ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -Command """ & Cmd & """") With {
                    .UseShellExecute = False, .CreateNoWindow = True,
                    .RedirectStandardError = True}
                Using P As Process = Process.Start(Psi)
                    Dim ErrText As String = P.StandardError.ReadToEnd()
                    P.WaitForExit()
                    InvalidateCache()
                    If P.ExitCode = 0 Then Return ""
                    Return "卸载失败：" & ErrText.Trim()
                End Using
            End If
            ' exe 版（GDK / Win32）是用 Inno Setup 装进 bedrock 的，目录里有 unins*.exe 就走它自带的卸载
            Dim Folder As String = EntryFolder(Entry)
            Dim Unins As String = ""
            If Directory.Exists(Folder) Then
                Dim Found As String() = Directory.GetFiles(Folder, "unins*.exe")
                If Found.Length > 0 Then Unins = Found(0)
            End If
            If Unins <> "" Then
                Dim P As Process = Process.Start(New ProcessStartInfo(Unins, "/VERYSILENT /NORESTART") With {
                    .UseShellExecute = False, .WorkingDirectory = Folder})
                P.WaitForExit(60000)
            Else
                Return "没有找到卸载程序，直接删除 " & Folder & " 即可。"
            End If
            InvalidateCache()
            Return ""
        Catch Ex As Exception
            Return "卸载失败：" & Ex.Message
        End Try
    End Function

    ''' <summary>打开条目目录。</summary>
    Public Sub OpenFolder(Entry As BedrockEntry)
        Try
            Dim Folder As String = EntryFolder(Entry)
            Directory.CreateDirectory(Folder)
            Process.Start(New ProcessStartInfo(Folder) With {.UseShellExecute = True})
        Catch Ex As Exception
            Hint("无法打开目录：" & Ex.Message, HintType.Red)
        End Try
    End Sub

End Module

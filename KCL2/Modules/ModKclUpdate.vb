Imports System.IO
Imports System.Diagnostics
Imports Newtonsoft.Json.Linq

Friend Module ModKclUpdate

    '更新与公告来源（均指向 ETQWFD/KCL2 自有仓库与其 GitHub Pages）
    '更新判定比对 continuous 发布“实际构建 exe 所用提交”（release 的 target_commitish），
    '而不是 main 分支 HEAD：只改官网网页等非代码提交也会推进 main HEAD，但不会重建 exe，
    '若比对 HEAD 会导致下载到的仍是旧 exe、每次启动都误报“发现新版本”的死循环。
    Private Const ReleaseApi As String = "https://api.github.com/repos/ETQWFD/KCL2/releases/tags/continuous"
    Private Const ExeUrl As String = "https://github.com/ETQWFD/KCL2/releases/download/continuous/KCL2.exe"
    Private Const AnnounceUrl As String = "https://etqwfd.github.io/KCL2/announce.json"

    Private _startupChecked As Boolean = False
    Private _updating As Boolean = False

    ''' <summary>关于页“检查更新”按钮：立即查询并提示，无论版本是否相同。</summary>
    Public Sub CheckUpdateByButton()
        RunInNewThread(
        Sub()
            Dim Remote As String = GetRemoteShortSha()
            If Remote.Length < 7 Then
                RunInUi(Sub() MyMsgBox("暂时无法获取更新信息，请检查网络后重试。", "KCL2 更新", "好的"))
                Return
            End If
            Dim Current As String = If(CommitHash, "").ToLowerInvariant().Trim()
            If Current.Length >= 7 Then Current = Current.Substring(0, 7)
            If Remote = Current Then
                RunInUi(Sub() MyMsgBox("当前已是最新版本。", "KCL2 更新", "好的"))
                Return
            End If
            RunInUiWait(Sub()
                            Dim Result As Integer = MyMsgBox(
                                $"发现 KCL2 新版本。{vbCrLf}当前版本：{If(Current.Length >= 7, Current, "未知")}{vbCrLf}最新版本：{Remote}{vbCrLf}{vbCrLf}是否立即更新？",
                                "KCL2 更新", "立即更新", "忽略此版本", "以后再说")
                            If Result = 1 Then
                                StartSelfUpdate(Remote)
                            ElseIf Result = 2 Then
                                Settings.Set("KCL2UpdateIgnoreVersion", Remote)
                            End If
                        End Sub)
        End Sub, "KCL2 手动检查更新")
    End Sub

    ''' <summary>主窗体第三阶段加载完成后调用：后台检查公告与更新，不阻塞启动。</summary>
    Public Sub RunStartupRemoteChecks()
        If _startupChecked Then Return
        _startupChecked = True
        RunInNewThread(
        Sub()
            Try
                CheckAnnounceOnStartup()
            Catch ex As Exception
                Logger.Warn(ex, "KCL2 公告检查失败")
            End Try
            Try
                CheckUpdateOnStartup()
            Catch ex As Exception
                Logger.Warn(ex, "KCL2 自动更新检查失败")
            End Try
        End Sub, "KCL2 启动更新检查", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>线上 continuous 发布实际构建所用提交的短 SHA（= 当前可下载 exe 的构建号）；网络失败返回空串。</summary>
    Private Function GetRemoteShortSha() As String
        Dim Json As String = NetRequestByClientRetry(ReleaseApi, RequireJson:=True, SimulateBrowserHeaders:=True)
        Dim Sha As String = JObject.Parse(Json).Value(Of String)("target_commitish")
        If String.IsNullOrEmpty(Sha) Then Return ""
        Sha = Sha.Trim().ToLowerInvariant()
        'target_commitish 理论上是 40 位完整 SHA；若异常返回分支名等非哈希值则判定为取不到，避免误报
        If Sha.Length < 7 Then Return ""
        Dim hex As Integer = 0
        For Each ch As Char In Sha
            If Not ((ch >= "0"c And ch <= "9"c) OrElse (ch >= "a"c And ch <= "f"c)) Then Return ""
            hex += 1
            If hex >= 40 Then Exit For
        Next
        Return Sha.Substring(0, 7)
    End Function

    Private Sub CheckUpdateOnStartup()
        '仅在 CI 注入了 CommitHash 时比较，避免本地开发构建误报
        Dim Current As String = If(CommitHash, "").ToLowerInvariant().Trim()
        If Current.Length < 7 Then Return
        Current = Current.Substring(0, 7)
        Dim Remote As String = GetRemoteShortSha()
        If Remote.Length < 7 OrElse Remote = Current Then Return

        Dim Ignored As String = Settings.Get(Of String)("KCL2UpdateIgnoreVersion").ToLowerInvariant().Trim()
        If Ignored = Remote Then Return '用户已忽略该版本，等更新的提交出现后才会再提示

        RunInUiWait(Sub()
                        Dim Result As Integer = MyMsgBox(
                            $"发现 KCL2 新版本。{vbCrLf}当前版本：{Current}{vbCrLf}最新版本：{Remote}{vbCrLf}{vbCrLf}是否立即更新？",
                            "KCL2 更新", "立即更新", "忽略此版本", "以后再说")
                        If Result = 1 Then
                            StartSelfUpdate(Remote)
                        ElseIf Result = 2 Then
                            Settings.Set("KCL2UpdateIgnoreVersion", Remote)
                        End If
                        'Result = 3（以后再说）：不记录，下次启动继续提示
                    End Sub)
    End Sub

    ''' <summary>下载单文件 KCL2.exe，退出后由批处理替换并重启。</summary>
    Public Sub StartSelfUpdate(RemoteSha As String)
        If _updating Then Return
        _updating = True
        RunInNewThread(
        Sub()
            Try
                Dim Dir As String = Paths.Base
                Dim CurrentExe As String = PathExe
                Dim NewExe As String = Dir & "KCL2.update.exe"
                Dim BatPath As String = Dir & "KCL2-update.bat"

                FileUtils.Delete(NewExe)
                Logger.Info($"开始下载 KCL2 更新：{ExeUrl}")
                NetDownloadByClient(ExeUrl, NewExe)

                Dim Fi As New FileInfo(NewExe)
                If Not Fi.Exists OrElse Fi.Length < 1024 * 1024 Then
                    Throw New Exception("更新文件不完整，下载大小异常")
                End If

                '写英文批处理：等待进程退出 -> 替换 exe -> 重启 -> 自删
                Dim Bat As New System.Text.StringBuilder()
                Bat.AppendLine("@echo off")
                Bat.AppendLine("chcp 65001 >nul")
                Bat.AppendLine("set OLD=" & EscapeBat(CurrentExe))
                Bat.AppendLine("set NEW=" & EscapeBat(NewExe))
                Bat.AppendLine(":waitloop")
                Bat.AppendLine("timeout /t 1 /nobreak >nul")
                Bat.AppendLine("move /y ""%NEW%"" ""%OLD%"" >nul 2>nul")
                Bat.AppendLine("if exist ""%NEW%"" goto waitloop")
                Bat.AppendLine("start """" ""%OLD%""")
                Bat.AppendLine("(goto) 2>nul & del ""%~f0""")
                File.WriteAllText(BatPath, Bat.ToString(), New Text.UTF8Encoding(False))

                Logger.Info("更新下载完成，执行替换脚本并退出当前进程")
                Process.Start(New ProcessStartInfo With {
                    .FileName = "cmd.exe",
                    .Arguments = "/c """ & BatPath & """",
                    .WindowStyle = ProcessWindowStyle.Hidden,
                    .CreateNoWindow = True,
                    .UseShellExecute = False
                })
                Environment.Exit(0)
            Catch ex As Exception
                _updating = False
                Logger.Warn(ex, "KCL2 自动更新失败")
                RunInUi(Sub() MyMsgBox("更新失败，请稍后再试或前往官网手动下载。" & vbCrLf & ex.Message, "KCL2 更新", "好的", IsWarn:=True))
            End Try
        End Sub, "KCL2 下载更新")
    End Sub

    Private Function EscapeBat(Path As String) As String
        Return Path.Replace("&", "^&").Replace("%", "%%")
    End Function

    ''' <summary>拉取 Pages 上的公告，若为新公告则弹窗一次。</summary>
    Private Sub CheckAnnounceOnStartup()
        Dim Json As String = NetRequestByClientRetry(AnnounceUrl, RequireJson:=True, SimulateBrowserHeaders:=True)
        Dim Obj As JObject = JObject.Parse(Json)
        If Not Obj.Value(Of Boolean?)("enabled").GetValueOrDefault(False) Then Return
        Dim Id As String = Obj.Value(Of String)("id")
        If String.IsNullOrEmpty(Id) Then Return
        If Id = Settings.Get(Of String)("KCL2AnnounceSeenId") Then Return
        Dim Title As String = If(Obj.Value(Of String)("title"), "KCL2 公告")
        Dim Content As String = If(Obj.Value(Of String)("content"), "")
        If Content.Trim() = "" Then Return
        RunInUiWait(Sub()
                        MyMsgBox(Content, Title, "我知道了")
                        Settings.Set("KCL2AnnounceSeenId", Id)
                    End Sub)
    End Sub

End Module

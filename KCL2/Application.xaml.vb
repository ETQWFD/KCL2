Imports System.Reflection
Imports System.Windows.Interop
Imports System.Windows.Threading

Public Class Application

    '开始
    Private Sub Application_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
        Try
            '核心初始化
            MeloongCore.Main.Init("KCL")
            MeloongCore.Wpf.Main.Init()
            If Not (e.Args.Contains("--update") OrElse e.Args.Contains("--gpu") OrElse e.Args.Contains("--memory")) Then '若是通过命令行调用，则不检查单例、不轮换日志（#9137）
                WaitingMutex(e)
                Logger.Instance = New PclLogger With {.logFolder = Paths.BaseThenName, .MinLevel = If(ModeDebug, LogLevel.Trace, LogLevel.Info)}
            End If
            '兼容渲染模式：显卡驱动异常时自动使用软件渲染（必须在创建任何 WPF 窗口前设置才会生效）
            Try
                If Settings.Get(Of Boolean)("SystemSoftRender") Then
                    RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly
                    Logger.Warn("已启用兼容渲染模式（软件渲染）")
                End If
            Catch
            End Try
            '提升主线程优先级
            Thread.CurrentThread.Priority = ThreadPriority.Highest
            '执行开发版测试
            If BuildType = BuildTypes.Debug Then
                Try
                    ModDevelop.Start()
                Catch ex As Exception
                    Logger.Error(ex, "开发者模式测试出错", LogBehavior.Alert)
                End Try
            End If
            '检查 .NET Framework 版本
            Try
                If CInt(RegistryUtils.TryRead("HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", "Release", 999999)) < 528040 Then OldEnvironmentAssert()
            Catch ex As Exception
                Logger.Warn(ex, "检查 .NET Framework 版本失败")
            End Try
            '修复 WPF 字体加载（#3467）
            Try
                Dim _unused As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First, 96, New MyColor, DPI)
            Catch ex As UriFormatException
                Environment.SetEnvironmentVariable("windir", Environment.GetEnvironmentVariable("SystemRoot"), EnvironmentVariableTarget.User)
                Dim _unused As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First, 96, New MyColor, DPI)
            End Try
            '检查参数调用
            If e.Args.Length > 0 Then
                If e.Args(0) = "--update" Then
                    '自动更新
                    UpdateReplace(e.Args(1), e.Args(2).Trim(""""), e.Args(3).Trim(""""), e.Args(4))
                    Environment.Exit(ProcessReturnValues.TaskDone)
                ElseIf e.Args(0) = "--gpu" Then
                    '调整显卡设置
                    Try
                        SetGPUPreference(e.Args(1).Trim(""""))
                        Environment.Exit(ProcessReturnValues.TaskDone)
                    Catch ex As Exception
                        Environment.Exit(ProcessReturnValues.Fail)
                    End Try
                ElseIf e.Args(0) = "--memory" Then
                    '内存优化
                    Dim Ram = My.Computer.Info.AvailablePhysicalMemory
                    Try
                        PageOtherTest.MemoryOptimizeInternal(False)
                    Catch ex As Exception
                        MsgBox(ex.Message, MsgBoxStyle.Critical, "内存优化失败")
                        Environment.Exit(-1)
                    End Try
                    If My.Computer.Info.AvailablePhysicalMemory < Ram Then '避免 ULong 相减出现负数
                        Environment.Exit(0)
                    Else
                        Environment.Exit((My.Computer.Info.AvailablePhysicalMemory - Ram) / 1024) '返回清理的内存量（K）
                    End If
                End If
            End If
            '初始化文件结构
            Try
                DirectoryUtils.Create(Paths.Base & "KCL\Pictures\")
                DirectoryUtils.Create(Paths.Base & "KCL\Musics\")
                CheckPermissionWithException(Paths.Base & "KCL\")
            Catch ex As Exception
                MsgBox($"KCL2 没有对当前文件夹的权限（{Paths.Base}KCL\），请尝试：" & vbCrLf &
                  "1. 将 KCL2 移动到其他文件夹" & If(PathUtils.GetDiskName(Paths.Base) = "C"c, "，例如 C 盘和桌面以外的其他位置。", "。") & vbCrLf &
                  "2. 删除当前目录中的 KCL2 文件夹，然后再试。" & vbCrLf &
                  "3. 右键 KCL2 选择属性，打开 兼容性 中的 以管理员身份运行此程序。",
                MsgBoxStyle.Critical, "运行环境错误")
                Environment.[Exit](ProcessReturnValues.Cancel)
            End Try
RetryCacheCheck:
            Try
                DirectoryUtils.Create(PathTemp)
                CheckPermissionWithException(PathTemp)
            Catch ex As Exception
                If PathUtils.IsSame(PathTemp, Path.GetTempPath() & "KCL\") Then
                    MyMsgBox("KCL2 无法访问缓存文件夹，可能导致程序出错或无法正常使用！" & vbCrLf & vbCrLf & "错误原因：" & ex.GetDisplay(True), "缓存文件夹不可用")
                Else
                    MyMsgBox("手动设置的缓存文件夹不可用，KCL2 将使用默认缓存文件夹。" & vbCrLf & vbCrLf & "错误原因：" & ex.GetDisplay(True), "缓存文件夹不可用")
                    Settings.Set("SystemSystemCache", "")
                    PathTemp = Path.GetTempPath() & "KCL\"
                    GoTo RetryCacheCheck
                End If
            End Try
            DirectoryUtils.Create(PathTemp & "Cache\")
            DirectoryUtils.Create(Paths.AppDataThenName)
            If Not EnsureLicenseAgreement() Then
                FormMain.EndProgramForce(ProcessReturnValues.Cancel)
                Return
            End If
            '设置 ToolTipService 默认值
            ToolTipService.InitialShowDelayProperty.OverrideMetadata(GetType(DependencyObject), New FrameworkPropertyMetadata(100))
            '设置网络配置默认值
            ServicePointManager.Expect100Continue = False
            ServicePointManager.DefaultConnectionLimit = 10000
            ServicePointManager.UseNagleAlgorithm = False
            ServicePointManager.EnableDnsRoundRobin = True
            ServicePointManager.ReusePort = True
            If Environment.OSVersion.Version < New Version(6, 2) Then 'Windows 7 回退（#8519）
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 Or SecurityProtocolType.Tls Or SecurityProtocolType.Tls11 Or SecurityProtocolType.Tls12
            End If
            '设置初始窗口
            If Settings.Get(Of Boolean)("UiLauncherLogo") Then
                FrmStart = New SplashScreen("Images\icon.ico")
                FrmStart.Show(False, True)
            End If
            '基础信息
            Logger.Info($"程序版本：{VersionDisplay} ({VersionCode}{If(CommitHash = "", "", $"，#{CommitHash}")})")
            If BuildType = BuildTypes.Snapshot Then
                Logger.Info($"识别码：{Identify}{If(ThemeCheckOne(9), "，已解锁反馈主题", "，未解锁反馈主题")}")
            Else
                Logger.Info($"识别码：{Identify}")
            End If
            Logger.Info($"程序路径：{PathExe}")
            Logger.Info($"系统编码：{Encoding.Default.HeaderName} ({Encoding.Default.CodePage}, GBK={IsGBKEncoding})")
            Logger.Info($"管理员权限：{WindowsUtils.HasAdminRole()}")
            '检测异常环境
            If Paths.Base.Contains(Path.GetTempPath()) OrElse Paths.Base.Contains("AppData\Local\Temp\") Then
                MyMsgBox("请将 KCL2 从压缩包中解压后再使用！" & vbCrLf & "如果不会解压，可以在网上寻找教程。", "需要解压！", "我知道了", IsWarn:=True, ForceWait:=True)
                FormMain.EndProgramForce(ProcessReturnValues.Cancel)
            End If
            If Not Environment.Is64BitOperatingSystem Then
                MyMsgBox("KCL2 和新版 Minecraft 均不再支持 32 位系统，请重装为 64 位系统后再进行游戏！", "环境警告", "我知道了", IsWarn:=True, ForceWait:=True)
                FormMain.EndProgramForce(ProcessReturnValues.Cancel)
            End If
            '计时
            Logger.Info($"第一阶段加载用时：{GetTimeMs() - ApplicationStartTick} ms")
            ApplicationStartTick = GetTimeMs()
            AniControlEnabled += 1
        Catch ex As Exception
            Dim FilePath As String = Nothing
            Try
                FilePath = PathExe
            Catch
            End Try
            MsgBox(ex.GetDisplay(True) & vbCrLf & "KCL2 所在路径：" & If(String.IsNullOrEmpty(FilePath), "获取失败", FilePath), MsgBoxStyle.Critical, "KCL2 初始化错误")
            FormMain.EndProgramForce(ProcessReturnValues.Exception)
        End Try
    End Sub
    ''' <summary>
    ''' 要求程序以单例运行。
    ''' 如果已在运行，则将其窗口拖出来并播放提示音，然后结束当前进程。
    ''' </summary>
    Private Shared Sub WaitingMutex(e As StartupEventArgs)
        If BuildType = BuildTypes.Debug Then Return
        Try
            Dim MutexCreatedNew As Boolean
            KclMutex = New Mutex(True, "KCL_SingletonMutex", MutexCreatedNew)
            If MutexCreatedNew Then Return
            Logger.Warn("已有一个 KCL2 实例正在运行")
            '等待已有的 KCL2 退出
            If e.Args.Length > 0 AndAlso e.Args(0) = "--wait" Then
                Try
                    If KclMutex.WaitOne(10000) Then Return '等待至多 10 秒
                Catch Ignored As AbandonedMutexException
                    Return '已有实例异常退出，继续启动
                End Try
            End If
            '将已有的 KCL2 窗口拖出来
            Dim WindowHwnd As IntPtr = FindWindow(Nothing, "KCL2　")
            If WindowHwnd = IntPtr.Zero Then WindowHwnd = FindWindow(Nothing, "KCL2　")
            If WindowHwnd <> IntPtr.Zero Then ShowWindowToTop(WindowHwnd)
            '播放提示音并退出
            Beep()
            Environment.[Exit](ProcessReturnValues.Cancel)
        Catch ex As Exception
            Logger.Warn(ex, "单例检查失败")
        End Try
    End Sub
    Private Shared KclMutex As Mutex

    '结束
    Private Sub Application_SessionEnding(sender As Object, e As SessionEndingCancelEventArgs) Handles Me.SessionEnding
        FrmMain?.EndProgram(False)
    End Sub

    '异常
    Private Sub Application_DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs) Handles Me.DispatcherUnhandledException
        On Error Resume Next
        '触发页面的 Dispatcher
        If FrmMain?.PageLeft IsNot Nothing AndAlso TypeOf FrmMain.PageLeft Is IDispatcherUnhandledException Then
            CType(FrmMain.PageLeft, IDispatcherUnhandledException).DispatcherUnhandledException(sender, e)
            If e.Handled Then Return
        End If
        If FrmMain?.PageRight IsNot Nothing AndAlso TypeOf FrmMain.PageRight Is IDispatcherUnhandledException Then
            CType(FrmMain.PageRight, IDispatcherUnhandledException).DispatcherUnhandledException(sender, e)
            If e.Handled Then Return
        End If
        '正常处理
        e.Handled = True
        If IsProgramEnding Then Return
        FeedbackInfo()
        '显卡/渲染不兼容导致的崩溃：切换软件渲染兼容模式
        If IsRenderCrash(e.Exception) Then
            HandleRenderCrash(e.Exception)
            Return
        End If
        Dim Detail As String = e.Exception.GetDisplay(True)
        If Detail.Contains("System.Windows.Threading.Dispatcher.Invoke") OrElse Detail.Contains("MS.Internal.AppModel.ITaskbarList.HrInit") OrElse Detail.Contains("未能加载文件或程序集") OrElse
           Detail.Contains(".NET Framework") Then ' “自动错误判断” 的结果分析
            OldEnvironmentAssert(e.Exception)
        Else
            Logger.Error(e.Exception, "程序出现未知错误", LogBehavior.AlertThenCrash)
        End If
    End Sub

    '==================== 全局崩溃兜底 ====================
    '防重入锁：保证任意时刻只有一个线程在处理崩溃弹窗
    Private Shared CrashDialogLock As New Object

    ''' <summary>
    ''' 非 UI 线程（下载线程、定时器线程、网络线程等）未处理异常的兜底。
    ''' 此事件触发后进程即将终止，这里只做日志抢救与用户提示，不尝试恢复。
    ''' </summary>
    Private Shared Sub OnAppDomainUnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
        On Error Resume Next
        ReportFatalCrash(TryCast(e.ExceptionObject, Exception), "后台线程发生未处理异常")
    End Sub

    ''' <summary>
    ''' 未观察的 Task 异常兜底。此类异常默认不会终止进程，记录即可。
    ''' </summary>
    Private Shared Sub OnTaskUnobservedException(sender As Object, e As UnobservedTaskExceptionEventArgs)
        On Error Resume Next
        e.SetObserved() '防止进程因未观察任务异常而终止
        If IsProgramEnding Then Return
        Logger.Error(e.Exception, "后台任务发生未观察异常", LogBehavior.None)
        PclLogger.SaveCrashReport(e.Exception, "UnobservedTaskException", "")
    End Sub

    ''' <summary>
    ''' 致命崩溃处理：自身绝不允许抛出任何异常。
    ''' 全程 On Error Resume Next + Try/Finally 保证锁一定释放；
    ''' 使用原生 MsgBox 而非 WPF 弹窗，避免 UI 线程已死时无法交互。
    ''' </summary>
    Private Shared Sub ReportFatalCrash(Ex As Exception, Source As String)
        ' 外层 Try/Catch：崩溃处理自身绝不能再抛任何异常（替代 On Error Resume Next，
        ' VB 不允许同一方法混用 On Error 与 Try），二次异常静默吞掉。
        Try
        If IsProgramEnding Then
            Try
                Logger.Error(Ex, Source, LogBehavior.None)
            Catch
            End Try
            Exit Sub
        End If
        '防重入：已有线程在处理崩溃时，其余线程只写日志直接返回，避免弹窗风暴与递归
        If Not Monitor.TryEnter(CrashDialogLock, 0) Then
            Try
                Logger.Error(Ex, Source & "（崩溃处理重入，仅记录）", LogBehavior.None)
            Catch
            End Try
            Exit Sub
        End If
        Try
            '1. 先抢救日志与崩溃报告（Logger 可能尚未初始化，每个调用单独吞异常）
            Try
                Logger.Error(Ex, Source, LogBehavior.None)
            Catch
            End Try
            Try
                FeedbackInfo()
            Catch
            End Try
            Try
                PclLogger.SaveCrashReport(Ex, Source, "")
            Catch
            End Try
            '2. 显卡/渲染不兼容：走兼容模式流程
            If IsRenderCrash(Ex) Then
                HandleRenderCrash(Ex)
                Return
            End If
            '3. 原生弹窗提示（不依赖 WPF UI 线程，线程亲和安全）
            Dim BriefEx As String = "（无法获取异常信息）"
            If Ex IsNot Nothing Then BriefEx = Brief(Ex.GetDisplay(True))
            Dim Message As String =
                "KCL2 遇到了意外问题并需要关闭。" & vbCrLf & vbCrLf &
                "错误摘要：" & BriefEx & vbCrLf & vbCrLf &
                "详细日志已保存到：" & Paths.Base & "KCL\Log1.txt" & vbCrLf &
                "如果问题反复出现，请将 Log1.txt 发送至崩溃邮箱 2416444244@qq.com 反馈。" & vbCrLf &
                "—— 龙腾OS"
            MsgBox(Message, MsgBoxStyle.Critical, "KCL2 已崩溃")
            '4. 打开日志文件夹，方便用户取证
            OpenExplorer(Paths.Base & "KCL\")
        Finally
            Monitor.Exit(CrashDialogLock)
        End Try
        '5. 走既定崩溃退出流程（保存配置、刷新日志、打开日志文件）
        FormMain.EndProgramForce(ProcessReturnValues.Exception)
        Catch
            '崩溃处理自身再异常时静默吞掉，绝不让二次崩溃掩盖原始崩溃
        End Try
    End Sub

    ''' <summary>
    ''' 判断异常是否由显卡/渲染环境不兼容引起（Milcore / D3D / DXGI / WGL 等特征）。
    ''' </summary>
    Private Shared Function IsRenderCrash(Ex As Exception) As Boolean
        On Error Resume Next
        If Ex Is Nothing Then Return False
        Dim Text As String = Ex.ToString().ToUpper()
        If Ex.InnerException IsNot Nothing Then Text &= Ex.InnerException.ToString().ToUpper()
        Return Text.Contains("MILCORE") OrElse Text.Contains("D3D") OrElse Text.Contains("DXGI") OrElse
               Text.Contains("WGL") OrElse Text.Contains("DIRECT3D") OrElse Text.Contains("硬件加速") OrElse
               Text.Contains("HARDWARE ACCELERATION")
    End Function

    ''' <summary>
    ''' 处理显卡/渲染不兼容崩溃：持久化兼容设置、切换软件渲染、提示用户重启。
    ''' </summary>
    Private Shared Sub HandleRenderCrash(Ex As Exception)
        On Error Resume Next
        '1. 记录崩溃现场
        Logger.Error(Ex, "检测到显卡/渲染环境不兼容", LogBehavior.None)
        FeedbackInfo()
        PclLogger.SaveCrashReport(Ex, "RenderCrash", "")
        '2. 持久化兼容设置（下次启动在创建窗口前启用软件渲染）
        Settings.Set("SystemSoftRender", True)
        '3. 尝试立即切换软件渲染（当前进程通常需重启后才完全生效）
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly
        '4. 原生弹窗提示
        MsgBox("检测到你的显卡驱动或渲染环境存在兼容性问题，KCL2 已自动切换为兼容模式（软件渲染）。" & vbCrLf & vbCrLf &
               "请重启 KCL2 使兼容模式生效。若此后仍频繁崩溃，请将日志发送至 2416444244@qq.com 反馈。" & vbCrLf &
               "日志位置：" & Paths.Base & "KCL\Log1.txt" & vbCrLf &
               "—— 龙腾OS",
               MsgBoxStyle.Information, "KCL2 兼容模式")
        OpenExplorer(Paths.Base & "KCL\")
        '5. 优雅退出，等待用户重启
        FormMain.EndProgramForce(ProcessReturnValues.Exception)
    End Sub

    ''' <summary>
    ''' 截断异常文本，避免把超长堆栈塞进弹窗标题与正文。
    ''' </summary>
    Private Shared Function Brief(Text As String, Optional MaxLength As Integer = 600) As String
        On Error Resume Next
        If String.IsNullOrEmpty(Text) Then Return "（无详细信息）"
        Text = Text.ReplaceLineEndings(" ", mergeMultiple:=True)
        If Text.Length <= MaxLength Then Return Text
        Return Text.Substring(0, MaxLength) & " ……（过长，详见日志文件）"
    End Function

    Private Shared Function EnsureLicenseAgreement() As Boolean
        If Settings.Get(Of Boolean)("KCL2LicenseAccepted") Then Return True
        Dim Agreement As String =
            "KCL2 License and First-Run Notice" & vbCrLf & vbCrLf &
            "KCL2 is an independent third-party modification based on Plain Craft Launcher 2 (PCL2)." & vbCrLf &
            "Original work: Plain Craft Launcher, created by 龙腾猫跃 (LTCat), with contributors credited in the About page." & vbCrLf &
            "The original PCL distribution license and copyright notices remain included in this repository." & vbCrLf & vbCrLf &
            "KCL2 modifications are maintained by the ETC Team and are provided AS IS, without warranty of any kind. " &
            "The original PCL authors do not operate, endorse, or provide support for this third-party build." & vbCrLf &
            "Minecraft account, purchase, server, recharge, and multiplayer disputes should be handled through Mojang/Microsoft, " &
            "the payment provider, the server operator, or the appropriate official dispute channel." & vbCrLf & vbCrLf &
            "By continuing, you confirm that you understand and accept these terms."
        Dim Result As Integer = MyMsgBox(Agreement, "KCL2 User Agreement", "I agree and continue", "Exit", IsWarn:=False, ForceWait:=True)
        If Result = 1 Then
            Settings.Set("KCL2LicenseAccepted", True)
            Return True
        End If
        Return False
    End Function

    Private Shared Sub OldEnvironmentAssert(Optional ex As Exception = Nothing)
        OpenWebsite("https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/thank-you/net48-offline-installer")
        If ex Is Nothing Then
            Logger.Error($"你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.8！{vbCrLf}若安装失败，可以先卸载高版本的 .NET Framework 然后再试。", LogBehavior.AlertThenCrash)
        Else
            Logger.Error(ex, $"你的 .NET Framework 版本过低或损坏，请下载并重新安装 .NET Framework 4.8！{vbCrLf}若安装失败，可以先卸载高版本的 .NET Framework 然后再试。", LogBehavior.AlertThenCrash)
        End If
    End Sub

    '动态 DLL 加载
    Private Sub New() '这里必须尽早调用，且不能使用任何库，否则加载 MeloongCore 就会导致崩溃
        Static Prefixes As String() = {"NAudio", "Newtonsoft.Json", "Ookii.Dialogs.Wpf", "Imazen.WebP", "CacheCow.Common", "CacheCow.Client.FileStore", "CacheCow.Client", "ThrottleDebounce", "System.Net.Http.Formatting", "Microsoft.Win32.Registry", "PCLCS", "MeloongCore.Wpf", "MeloongCore"}
        Static LoadedAssemblies As New ConcurrentDictionary(Of String, Lazy(Of Assembly))(StringComparer.Ordinal) '缓存
        AddHandler AppDomain.CurrentDomain.AssemblyResolve,
        Function(sender As Object, Args As ResolveEventArgs) As Assembly
            Dim Prefix As String = Prefixes.FirstOrDefault(Function(p) Args.Name.StartsWith(p, StringComparison.Ordinal))
            If Prefix Is Nothing Then Return Nothing
            Dim LazyAssembly = LoadedAssemblies.GetOrAdd(Prefix, Function(p) New Lazy(Of Assembly)(
            Function()
                Dim LoadedAssembly = Assembly.Load(DirectCast(My.Resources.ResourceManager.GetObject(p), Byte()))
                If p = "Imazen.WebP" Then ExtractLibwebp() 'WebP 特判
                Return LoadedAssembly
            End Function, LazyThreadSafetyMode.ExecutionAndPublication))
            Return LazyAssembly.Value
        End Function
        '挂载全局崩溃兜底（方法体内全程 Try 保护，此处不加载任何库）：
        '1. 非 UI/后台线程未处理异常 → 进程静默终止的根源；
        '2. 未观察 Task 异常 → 后台任务崩溃却无日志无提示；
        '3. WinForms 互操作线程异常（Ookii 对话框等偶发走 WinForms 消息循环）。
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnAppDomainUnhandledException
        AddHandler Threading.Tasks.TaskScheduler.UnobservedTaskException, AddressOf OnTaskUnobservedException
        Try
            System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException)
            AddHandler System.Windows.Forms.Application.ThreadException, AddressOf OnWinFormsThreadException
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' WinForms 互操作线程异常兜底。正常 WPF 程序走不到这里，但 Ookii 文件夹对话框、
    ''' 部分 Shell COM 互操作可能在 WinForms 消息循环里抛异常；统一路由到崩溃报告。
    ''' </summary>
    Private Shared Sub OnWinFormsThreadException(sender As Object, e As System.Threading.ThreadExceptionEventArgs)
        On Error Resume Next
        ReportFatalCrash(e.Exception, "WinForms 线程未处理异常")
    End Sub
    Private Shared Sub ExtractLibwebp() '这个方法会调用 ModBase，进而调用 MeloongCore，所以不能放在 New 里
        SetDllDirectory(PathUtils.RemoveSlashSuffix(PathPure.Value))
        Try
            ExtractResources(PathPure.Value & "libwebp.dll", "libwebp64")
        Catch ex As Exception
            Logger.Warn(ex, "写入 libwebp.dll 失败") '防止同时加载多个图片时，同时写入文件导致文件占用，进而导致崩溃
        End Try
    End Sub
    Private Declare Function SetDllDirectory Lib "kernel32" Alias "SetDllDirectoryA" (lpPathName As String) As Boolean

    '控件模板事件
    Private Sub MyIconButton_Click(sender As Object, e As EventArgs)
        Select Case Settings.Get(Of McLoginType)("LoginType")
            Case McLoginType.Ms
                '微软
                Dim MsJson As JObject = Settings.Get(Of String)("LoginMsJson").DeserializeJson()
                MsJson.Remove(sender.Tag)
                Settings.Set("LoginMsJson", MsJson.ToString(Newtonsoft.Json.Formatting.None))
                If FrmLoginMs.ComboAccounts.SelectedItem Is sender.Parent Then FrmLoginMs.ComboAccounts.SelectedIndex = 0
                FrmLoginMs.ComboAccounts.Items.Remove(sender.Parent)
            Case McLoginType.Legacy
                '离线
                Dim Names As New List(Of String)
                Names.AddRange(Settings.Get(Of String)("LoginLegacyName").ToString.Split("¨"))
                Names.Remove(sender.Tag)
                Settings.Set("LoginLegacyName", Names.Join("¨"c))
                FrmLoginLegacy.ComboName.ItemsSource = Names
                FrmLoginLegacy.ComboName.Text = If(Names.Any, Names(0), "")
            Case Else
                '第三方
                Dim Token As String = Settings.Get(Of McLoginType)("LoginType").ToString()
                Dim Dict As New Dictionary(Of String, String)
                Dim Names As New List(Of String)
                Dim Passs As New List(Of String)
                If Not Settings.Get(Of String)("Login" & Token & "Email") = "" Then Names.AddRange(Settings.Get(Of String)("Login" & Token & "Email").Split("¨"))
                If Not Settings.Get(Of String)("Login" & Token & "Pass") = "" Then Passs.AddRange(Settings.Get(Of String)("Login" & Token & "Pass").Split("¨"))
                For i = 0 To Names.Count - 1
                    Dict.Add(Names(i), Passs(i))
                Next
                Dict.Remove(sender.Tag)
                Settings.Set("Login" & Token & "Email", Dict.Keys.Join("¨"c))
                Settings.Set("Login" & Token & "Pass", Dict.Values.Join("¨"c))
                Select Case Token
                    Case "Nide"
                        FrmLoginNide.ComboName.ItemsSource = Dict.Keys
                        FrmLoginNide.ComboName.Text = If(Dict.Keys.Any, Dict.Keys(0), "")
                        FrmLoginNide.TextPass.Password = If(Dict.Values.Any, Dict.Values(0), "")
                    Case "Auth"
                        FrmLoginAuth.ComboName.ItemsSource = Dict.Keys
                        FrmLoginAuth.ComboName.Text = If(Dict.Keys.Any, Dict.Keys(0), "")
                        FrmLoginAuth.TextPass.Password = If(Dict.Values.Any, Dict.Values(0), "")
                End Select
        End Select
    End Sub

End Class

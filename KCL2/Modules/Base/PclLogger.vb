Public Class PclLogger
    Inherits FileLogger

    ''' <inheritdoc/>
    Public Overrides Function Format(Text As String, Level As LogLevel, FilePath As String, Ex As Exception) As String
        Text = MyBase.Format(Text, Level, FilePath, Ex)
        Text = FilterUserName(FilterAccessToken(Text, "*"c), "*"c)
        Return Text
    End Function

    ''' <inheritdoc/>
    Public Overrides Sub HandleBehavior(RawMessage As String, FormattedMessage As String, Behavior As LogBehavior, Ex As Exception)
        If IsProgramEnding Then Return
        MyBase.HandleBehavior(RawMessage, FormattedMessage, Behavior, Ex)
        Dim BriefText = If(Ex Is Nothing, RawMessage, If(RawMessage Is Nothing, "", $"{RawMessage}：") & Ex.GetDisplay(False))
        BriefText = FilterUserName(FilterAccessToken(BriefText, "*"c), "*"c)
        Dim DetailText = If(Ex Is Nothing, RawMessage, If(RawMessage Is Nothing, "", $"{RawMessage}：") & Ex.GetDisplay(True))
        DetailText = FilterUserName(FilterAccessToken(DetailText, "*"c), "*"c)
        Select Case Behavior
            Case LogBehavior.None
                '啥也不干
            Case LogBehavior.ToastIfDebug
                If BuildType = BuildTypes.Debug OrElse ModeDebug Then Hint("[调试模式] " & BriefText, HintType.Blue, False)
            Case LogBehavior.Toast
                Hint(BriefText, HintType.Red, False)
            Case LogBehavior.Alert
                MyMsgBox(DetailText, "错误", IsWarn:=True)
            Case LogBehavior.AlertThenFeedback
                If PageSetupSystem.IsLauncherNewest Then
                    If MyMsgBox(DetailText & vbCrLf & vbCrLf & "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！", "错误", "反馈", "取消", IsWarn:=True) = 1 Then Feedback(False, True)
                Else
                    MyMsgBox(DetailText & vbCrLf & vbCrLf & "将 KCL2 更新至最新版或许可以解决这个问题……", "错误", IsWarn:=True)
                End If
            Case LogBehavior.AlertThenCrash
                Static FirstTrigger As Boolean = True
                If FirstTrigger Then
                    '首次触发
                    FirstTrigger = False
                    If PageSetupSystem.IsLauncherNewest Then
                        If MsgBox(DetailText & vbCrLf & vbCrLf & "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！", MsgBoxStyle.Critical + MsgBoxStyle.YesNo, "错误") = MsgBoxResult.Yes Then Feedback(False, True)
                    Else
                        MsgBox(DetailText & vbCrLf & vbCrLf & "将 KCL2 更新至最新版或许可以解决这个问题……", MsgBoxStyle.Critical, "错误")
                    End If
                Else
                    '多次触发，直接使程序崩溃（这通常代表着在其他线程循环触发严重异常）
                    Thread.Sleep(2000)
                End If
                FormMain.EndProgramForce(ProcessReturnValues.Exception)
        End Select
        '遥测
        If Behavior >= LogBehavior.Toast Then Telemetry("错误日志", "Exception", DetailText)
    End Sub

    ''' <summary>
    ''' 崩溃兜底：将崩溃信息独立、同步地追加到 KCL\CrashReports.txt。
    ''' 不依赖日志后台线程（线程已死时仍可写盘），自身绝不抛出任何异常。
    ''' </summary>
    Public Shared Sub SaveCrashReport(Ex As Exception, Source As String, Extra As String)
        Try
            Dim Folder As String = Paths.Base & "KCL\"
            DirectoryUtils.Create(Folder)
            Dim Builder As New Text.StringBuilder()
            Builder.AppendLine("===== KCL2 崩溃报告 " & Date.Now.ToString("yyyy-MM-dd HH:mm:ss") & " =====")
            Builder.AppendLine("来源：" & If(Source, ""))
            Builder.AppendLine("异常：" & If(Ex Is Nothing, "（无）", Ex.GetDisplay(True)))
            If Not String.IsNullOrEmpty(Extra) Then Builder.AppendLine("附加信息：" & Extra)
            Builder.AppendLine()
            File.AppendAllText(Folder & "CrashReports.txt", Builder.ToString())
        Catch
            '写日志本身失败时静默放弃，绝不能让崩溃处理器二次崩溃
        End Try
    End Sub

End Class

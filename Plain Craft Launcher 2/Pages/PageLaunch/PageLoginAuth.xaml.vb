''' <summary>
''' 启动页的第三方登录面板。
''' 顶部可以在「mcpixelart 皮肤站」与「其他登录方式」之间切换；
''' 选择皮肤站时，可以直接在面板里登录并进入服务器，或切换到注册、找回密码。
''' </summary>
Public Class PageLoginAuth

    Private IsFirstLoad As Boolean = True
    ''' <summary>当前面板模式：login / register / reset。</summary>
    Private Shared PanelMode As String = "login"
    ''' <summary>是否选择了 mcpixelart 皮肤站。</summary>
    Private Shared UseSkinSite As Boolean = True
    ''' <summary>注册/改密时邮箱是否已经验证通过。</summary>
    Private MailVerified As Boolean = False
    ''' <summary>皮肤站操作串行执行的锁。</summary>
    Private Shared ReadOnly AuthLock As New Object
    ''' <summary>正在执行皮肤站操作。</summary>
    Private IsBusy As Boolean = False

    ''' <summary>刷新页面显示的所有信息。</summary>
    Public Sub Reload(KeepInput As Boolean)
        '记住密码
        CheckRemember.Checked = Settings.Get(Of Boolean)("LoginRemember")
        If KeepInput AndAlso Not IsFirstLoad Then '避免第一次就以 KeepInput 的方式加载，导致文本框里没东西
            '保留输入，只刷新下拉框列表
            Dim Input As String = ComboName.Text
            ComboName.ItemsSource = If(Settings.Get(Of String)("LoginAuthEmail") = "", Nothing, Settings.Get(Of String)("LoginAuthEmail").Split("¨"))
            ComboName.Text = Input
        Else
            '不保留输入，刷新列表后自动选择第一项
            If Settings.Get(Of String)("LoginAuthEmail") = "" Then
                ComboName.ItemsSource = Nothing
            Else
                ComboName.ItemsSource = Settings.Get(Of String)("LoginAuthEmail").Split("¨")
                ComboName.Text = Settings.Get(Of String)("LoginAuthEmail").BeforeFirst("¨")
                If Settings.Get(Of Boolean)("LoginRemember") Then TextPass.Password = Settings.Get(Of String)("LoginAuthPass").BeforeFirst("¨").Trim
            End If
        End If
        '皮肤站的账号默认填上次登录过的邮箱
        If ComboName.Text = "" AndAlso SkinMail <> "" Then
            ComboName.Text = SkinMail
            If SkinRemember AndAlso SkinPassword <> "" Then TextPass.Password = SkinPassword
        End If
        IsFirstLoad = False
        RadioSkinSite.Checked = UseSkinSite
        RadioOther.Checked = Not UseSkinSite
        ApplyPanelMode()
    End Sub

    ''' <summary>获取当前页面的登录信息。</summary>
    Public Shared Function GetLoginData() As McLoginServer
        Dim Server As String = If(IsNothing(McInstanceSelected), Settings.Get(Of String)("CacheAuthServerServer"), Settings.Get(Of String)("VersionServerAuthServer", Instance:=McInstanceSelected)) & "/authserver"
        If FrmLoginAuth Is Nothing Then
            Return New McLoginServer(McLoginType.Auth) With {.Token = "Auth", .BaseUrl = Server, .UserName = "", .Password = "", .Description = "Authlib-Injector", .Type = McLoginType.Auth}
        Else
            Return New McLoginServer(McLoginType.Auth) With {.Token = "Auth", .BaseUrl = Server, .UserName = FrmLoginAuth.ComboName.Text.Replace("¨", "").Trim, .Password = FrmLoginAuth.TextPass.Password.Replace("¨", "").Trim, .Description = "Authlib-Injector", .Type = McLoginType.Auth}
        End If
    End Function

    ''' <summary>当前页面的登录信息是否有效。</summary>
    Public Shared Function IsVaild(LoginData As McLoginServer) As String
        If LoginData.UserName = "" Then Return "账号不能为空！"
        If LoginData.Password = "" Then Return "密码不能为空！"
        Return ""
    End Function
    Public Function IsVaild() As String
        Return IsVaild(GetLoginData())
    End Function

#Region "登录方式切换"

    ''' <summary>切换皮肤站与其他登录方式。</summary>
    Private Sub RadioMode_Check(sender As Object, raiseByMouse As Boolean) Handles RadioSkinSite.Check, RadioOther.Check
        Dim UseSkin As Boolean = ReferenceEquals(sender, RadioSkinSite)
        If UseSkin = UseSkinSite Then Return
        UseSkinSite = UseSkin
        PanelMode = "login"
        MailVerified = False
        ApplyPanelMode()
    End Sub

    ''' <summary>根据当前模式刷新界面。</summary>
    Private Sub ApplyPanelMode()
        Dim IsSkin As Boolean = UseSkinSite
        PanSkinLogin.Visibility = If(IsSkin AndAlso PanelMode = "login", Visibility.Visible, Visibility.Collapsed)
        PanSkinVerify.Visibility = If(IsSkin AndAlso PanelMode <> "login", Visibility.Visible, Visibility.Collapsed)
        BtnReset.Visibility = If(IsSkin, Visibility.Visible, Visibility.Collapsed)
        BtnLink.Content = If(IsSkin AndAlso PanelMode <> "login", "返回登录", "注册账号")
        '验证步骤里不再需要记住密码
        CheckRemember.Visibility = If(IsSkin AndAlso PanelMode <> "login", Visibility.Collapsed, Visibility.Visible)
        '注册 / 改密时隐藏启动、版本选择与版本设置按钮
        If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.IsLoginCompact = IsSkin AndAlso PanelMode <> "login"
        '皮肤站的注册 / 找回密码提示
        If IsSkin AndAlso PanelMode <> "login" Then
            LabVerifyCode.Text = SkinCurrentCode()
            BtnVerifyDone.Text = If(PanelMode = "register", "完成注册", "更新密码")
            LabVerifyHint.Text = $"用上面的邮箱把下面这串数字作为标题发到 {SkinMailAddress}，正文留空，然后点{vbLQ}我已发送{vbRQ}。"
            BtnVerifyDone.IsEnabled = MailVerified
            BtnCheckMail.Text = If(MailVerified, "已收到", "我已发送")
            BtnCheckMail.IsEnabled = Not MailVerified
        End If
        HideHint()
    End Sub

#End Region

#Region "皮肤站登录"

    ''' <summary>用填写的邮箱与密码登录皮肤站，并直接启动游戏进入服务器。</summary>
    Private Sub BtnSkinLogin_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSkinLogin.Click
        Dim Mail As String = ComboName.Text.Replace("¨", "").Trim
        Dim Password As String = TextPass.Password
        Dim ErrorMessage As String = SkinValidateMail(Mail)
        If ErrorMessage <> "" Then
            ShowHint(ErrorMessage, True)
            Return
        End If
        If Password = "" Then
            ShowHint("请输入密码。", True)
            Return
        End If
        If IsBusy Then Return
        IsBusy = True
        ShowHint("正在登录皮肤站……", False)
        RunInThread(
        Sub()
            Try
                Dim Result As SkinSiteLoginResult = Nothing
                SyncLock AuthLock
                    SkinToken = SkinLogin(Mail, Password)
                    SkinMail = Mail
                    '顺带确认账号可以进入游戏，并拿到要使用的角色
                    Result = SkinYggdrasilLogin(Mail, Password)
                End SyncLock
                RunInUi(
                Sub()
                    IsBusy = False
                    SkinPassword = If(CheckRemember.Checked, Password, "")
                    SkinRemember = CheckRemember.Checked
                    SaveLoginRecord(Mail, Password)
                    SkinApplyLoginAccount(McInstanceSelected, Mail, Password, Result)
                    ShowHint($"登录成功，正在用 {Result.Name} 启动游戏……", False)
                    FrmMain.PageChange(FormMain.PageType.Launch)
                    McLaunchStart()
                End Sub)
            Catch ex As Exception
                RunInUi(
                Sub()
                    IsBusy = False
                    Dim Message As String = If(TypeOf ex Is SkinSiteException, ex.Message, "登录失败，请检查网络后重试。")
                    If Not TypeOf ex Is SkinSiteException Then Logger.Error(ex, "皮肤站登录失败")
                    ShowHint(Message, True)
                End Sub)
            End Try
        End Sub)
    End Sub

    '保存登录记录到 PCL 的自动填充列表
    Private Sub SaveLoginRecord(Mail As String, Password As String)
        Try
            Dim Emails As New List(Of String), Passwords As New List(Of String)
            If Settings.Get(Of String)("LoginAuthEmail") <> "" Then Emails.AddRange(Settings.Get(Of String)("LoginAuthEmail").Split("¨"))
            If Settings.Get(Of String)("LoginAuthPass") <> "" Then Passwords.AddRange(Settings.Get(Of String)("LoginAuthPass").Split("¨"))
            While Passwords.Count < Emails.Count
                Passwords.Add("")
            End While
            Dim Index As Integer = Emails.IndexOf(Mail)
            If Index >= 0 Then
                Passwords(Index) = Password
            Else
                Emails.Insert(0, Mail)
                Passwords.Insert(0, Password)
            End If
            If Emails.Count > 10 Then
                Emails = Emails.Take(10).ToList()
                Passwords = Passwords.Take(10).ToList()
            End If
            Settings.Set("LoginAuthEmail", Emails.Join("¨"c))
            Settings.Set("LoginAuthPass", Passwords.Join("¨"c))
            Settings.Set("LoginRemember", CheckRemember.Checked)
        Catch ex As Exception
            Logger.Warn(ex, "保存皮肤站登录记录失败")
        End Try
    End Sub

    '打开皮肤站页面管理皮肤与材质
    Private Sub BtnManageSkin_Click(sender As Object, e As EventArgs) Handles BtnManageSkin.Click
        SkinOpenPage()
    End Sub

#End Region

#Region "注册与找回密码"

    '注册账号 / 返回登录
    Private Sub BtnLink_Click(sender As Object, e As EventArgs) Handles BtnLink.Click
        If Not UseSkinSite Then
            '其他登录方式：保持 PCL 原本的注册页跳转
            Dim Website As String = If(McInstanceSelected IsNot Nothing, Settings.Get(Of String)("VersionServerAuthRegister", Instance:=McInstanceSelected), Settings.Get(Of String)("CacheAuthServerRegister"))
            If ComboName.Text = "" Then
                OpenWebsite(Website)
            Else
                OpenWebsite(Website.Replace("/auth/register", "/auth/forgot"))
            End If
            Return
        End If
        If PanelMode = "login" Then
            PanelMode = "register"
        Else
            PanelMode = "login"
        End If
        MailVerified = False
        ApplyPanelMode()
    End Sub

    '找回密码
    Private Sub BtnReset_Click(sender As Object, e As EventArgs) Handles BtnReset.Click
        If Not UseSkinSite Then Return
        PanelMode = If(PanelMode = "reset", "login", "reset")
        MailVerified = False
        ApplyPanelMode()
    End Sub

    '复制验证码
    Private Sub BorVerifyCode_Click(sender As Object, e As MouseButtonEventArgs) Handles BorVerifyCode.MouseLeftButtonUp
        ClipboardSet(SkinCurrentCode(), True, $"验证码已复制，请发送到 {SkinMailAddress}")
    End Sub

    '检查邮件是否已收到
    Private Sub BtnCheckMail_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnCheckMail.Click
        Dim Mail As String = ComboName.Text.Replace("¨", "").Trim
        Dim ErrorMessage As String = SkinValidateMail(Mail)
        If ErrorMessage <> "" Then
            ShowHint(ErrorMessage, True)
            Return
        End If
        Dim Code As String = SkinCurrentCode()
        IsBusy = True
        BtnCheckMail.IsEnabled = False
        RunInThread(
        Sub()
            Try
                Dim Valid As Boolean
                SyncLock AuthLock
                    Valid = SkinVerifyMail(Mail, Code)
                End SyncLock
                RunInUi(
                Sub()
                    IsBusy = False
                    BtnCheckMail.IsEnabled = True
                    If Valid Then
                        MailVerified = True
                        ApplyPanelMode()
                        ShowHint("验证邮件已收到，可以继续了。", False)
                    Else
                        ShowHint("还没有收到你的邮件。请确认已经把验证码发送到 " & SkinMailAddress & "，稍等十几秒再试。", True)
                    End If
                End Sub)
            Catch ex As Exception
                RunInUi(
                Sub()
                    IsBusy = False
                    BtnCheckMail.IsEnabled = True
                    ShowHint(If(TypeOf ex Is SkinSiteException, ex.Message, "检查失败，请稍后再试。"), True)
                End Sub)
            End Try
        End Sub)
    End Sub

    '完成注册 / 更新密码
    Private Sub BtnVerifyDone_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnVerifyDone.Click
        Dim Mail As String = ComboName.Text.Replace("¨", "").Trim
        Dim Password As String = TextPass.Password
        Dim ErrorMessage As String = SkinValidateMail(Mail)
        If ErrorMessage <> "" Then
            ShowHint(ErrorMessage, True)
            Return
        End If
        ErrorMessage = SkinValidatePassword(Password)
        If ErrorMessage <> "" Then
            ShowHint(ErrorMessage, True)
            Return
        End If
        If Not MailVerified Then
            ShowHint($"请先点{vbLQ}我已发送{vbRQ}确认邮件已经收到。", True)
            Return
        End If
        Dim Code As String = SkinCurrentCode()
        Dim IsRegister As Boolean = PanelMode = "register"
        IsBusy = True
        BtnVerifyDone.IsEnabled = False
        RunInThread(
        Sub()
            Try
                SyncLock AuthLock
                    If IsRegister Then
                        SkinToken = SkinRegister(Mail, Password, Code)
                        SkinMail = Mail
                    Else
                        SkinModifyPassword(Mail, Password, Code)
                    End If
                End SyncLock
                RunInUi(
                Sub()
                    IsBusy = False
                    BtnVerifyDone.IsEnabled = True
                    MailVerified = False
                    PanelMode = "login"
                    SkinNewCode()
                    If IsRegister Then
                        SkinPassword = Password
                        SkinRemember = True
                        CheckRemember.Checked = True
                        Settings.Set("LoginRemember", True)
                        SaveLoginRecord(Mail, Password)
                        ShowHint("注册成功，已为你填好账号，可以直接开始游戏了。", False)
                    Else
                        SkinPassword = If(CheckRemember.Checked, Password, "")
                        ShowHint("密码已更新，下次登录请使用新密码。", False)
                    End If
                    ApplyPanelMode()
                End Sub)
            Catch ex As Exception
                RunInUi(
                Sub()
                    IsBusy = False
                    BtnVerifyDone.IsEnabled = True
                    ShowHint(If(TypeOf ex Is SkinSiteException, ex.Message, "操作失败，请稍后再试。"), True)
                End Sub)
            End Try
        End Sub)
    End Sub

#End Region

#Region "输入处理与提示"

    '保存输入信息
    Private Sub ComboName_TextChanged(sender As Object, e As TextChangedEventArgs) Handles ComboName.TextChanged
        If sender.Text = "" Then TextPass.Password = ""
        If AniControlEnabled = 0 Then Settings.Set("CacheAuthAccess", "") '迫使其不进行 Validate
    End Sub
    Private Sub TextPass_PasswordChanged(sender As Object, e As RoutedEventArgs) Handles TextPass.PasswordChanged
        If AniControlEnabled = 0 Then Settings.Set("CacheAuthAccess", "")
    End Sub
    Private Sub ComboName_SelectionChanged(sender As MyComboBox, e As SelectionChangedEventArgs) Handles ComboName.SelectionChanged
        If sender.SelectedIndex = -1 OrElse Not Settings.Get(Of Boolean)("LoginRemember") Then
            TextPass.Password = ""
        Else
            TextPass.Password = Settings.Get(Of String)("LoginAuthPass").ToString.Split("¨")(sender.SelectedIndex).Trim
        End If
    End Sub
    Private Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckRemember.Change
        If AniControlEnabled = 0 Then Settings.Set(sender.Tag, sender.Checked)
    End Sub

    Private Sub ShowHint(Text As String, IsError As Boolean)
        HintAuth.Text = Text
        HintAuth.Theme = If(IsError, MyHint.Themes.Red, MyHint.Themes.Blue)
        HintAuth.Visibility = Visibility.Visible
    End Sub
    Private Sub HideHint()
        HintAuth.Text = ""
        HintAuth.Visibility = Visibility.Collapsed
    End Sub

    '切换注册按钮可见性
    Private Sub ReloadRegisterButton() Handles Me.Loaded
        ApplyPanelMode()
    End Sub

#End Region

End Class

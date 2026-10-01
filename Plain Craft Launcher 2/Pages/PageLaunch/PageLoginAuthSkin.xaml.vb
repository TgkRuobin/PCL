Public Class PageLoginAuthSkin

    Public Sub New()
        InitializeComponent()
        Skin.Loader = PageLaunchLeft.SkinAuth
    End Sub
    Private Sub PageLoginLegacy_Loaded() Handles Me.Loaded
        Skin.Loader.Start()
    End Sub

    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        TextName.Text = Settings.Get(Of String)("CacheAuthName")
        TextEmail.Text = Settings.Get(Of String)("CacheAuthUsername")
        TextEmail.Visibility = If(Settings.Get(Of Boolean)("UiLauncherEmail"), Visibility.Collapsed, Visibility.Visible)
        PageLoginLegacy_Loaded()
    End Sub
    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginServer
        Dim Server As String = If(IsNothing(McInstanceSelected), Settings.Get(Of String)("CacheAuthServerServer"), Settings.Get(Of String)("VersionServerAuthServer", Instance:=McInstanceSelected)) & "/authserver"
        Return New McLoginServer(McLoginType.Auth) With {.Token = "Auth", .BaseUrl = Server, .UserName = Settings.Get(Of String)("CacheAuthUsername"), .Password = Settings.Get(Of String)("CacheAuthPass"), .Description = "Authlib-Injector", .Type = McLoginType.Auth}
    End Function

    Private Sub PageLoginAuthSkin_MouseEnter(sender As Object, e As MouseEventArgs) Handles PanData.MouseEnter
        AniStart({
                 AaOpacity(BtnEdit, 1 - BtnEdit.Opacity, 80),
                 AaHeight(BtnEdit, 25.5 - BtnEdit.Height, 140,, New AniEaseOutFluent),
                 AaHeight(BtnEdit, -1.5, 50, 140, New AniEaseInFluent),
                 AaOpacity(BtnExit, 1 - BtnExit.Opacity, 80),
                 AaHeight(BtnExit, 25.5 - BtnExit.Height, 140,, New AniEaseOutFluent),
                 AaHeight(BtnExit, -1.5, 50, 140, New AniEaseInFluent)
        }, "PageLoginAuthSkin Button")
    End Sub
    Private Sub PageLoginAuthSkin_MouseLeave(sender As Object, e As MouseEventArgs) Handles PanData.MouseLeave
        AniStart({
                 AaOpacity(BtnEdit, -BtnEdit.Opacity, 120,, New AniEaseOutFluent),
                 AaHeight(BtnEdit, 14 - BtnEdit.Height, 120,, New AniEaseInFluent),
                 AaOpacity(BtnExit, -BtnExit.Opacity, 120,, New AniEaseOutFluent),
                 AaHeight(BtnExit, 14 - BtnExit.Height, 120,, New AniEaseInFluent)
        }, "PageLoginAuthSkin Button")
    End Sub

    Private Sub BtnEdit_Click(sender As Object, e As EventArgs) Handles BtnEdit.Click
        '皮肤站账号：更换角色就是到皮肤站页面改默认角色，不再弹出启动器的角色选择框
        If IsSkinSiteAccount() Then
            If McLaunchLoader.State = LoadState.Loading Then
                Hint("正在启动中，请稍后再更换角色！", HintType.Red, False)
                Return
            End If
            '清空角色缓存，下次启动会用当前默认角色重新登录
            Settings.Set("CacheAuthAccess", "")
            Settings.Set("CacheAuthUuid", "")
            Settings.Set("CacheAuthName", "")
            McLoginAuthLoader.Input = Nothing
            Hint("可以在皮肤站页面里把要用的角色设为默认角色。", HintType.Blue, False)
            SkinOpenPage()
            Return
        End If
        If McLoginLoader.State = LoadState.Loading Then
            Logger.Warn("要求更换角色，但登录加载器繁忙")
            If TypeOf McLoginLoader.Input Is McLoginServer AndAlso CType(McLoginLoader.Input, McLoginServer).ForceReselectProfile Then
                Hint("正在尝试更换，请稍候！")
            Else
                Hint("正在登录中，请稍后再更换角色！", HintType.Red)
            End If
            Return
        End If
        Hint("正在尝试更换，请稍候！")
        Settings.Set("CacheAuthUuid", "") '清空选择缓存
        Settings.Set("CacheAuthName", "")
        RunInThread(
        Sub()
            Try
                Dim Data As McLoginServer = GetLoginData()
                Data.ForceReselectProfile = True
                McLoginLoader.WaitForExit(Data, IsForceRestart:=True)
                RunInUi(Sub() Reload(True))
            Catch ex As Exception
                Logger.Error(ex, "更换角色失败", LogBehavior.Toast)
            End Try
        End Sub)
    End Sub
    Public Shared Sub ExitLogin() Handles BtnExit.Click
        Settings.Set("CacheAuthAccess", "")
        Settings.Set("CacheAuthUuid", "")
        Settings.Set("CacheAuthName", "")
        McLoginAuthLoader.Input = Nothing '防止因为输入的用户名密码相同，直接使用了上次登录的加载器结果
        FrmLaunchLeft.RefreshPage(False, True)
    End Sub

    Private Sub Skin_Click(sender As Object, e As MouseButtonEventArgs) Handles Skin.Click
        '皮肤站账号直接打开皮肤站页面，其他认证服务器仍然跳转网页
        If IsSkinSiteAccount() Then
            SkinOpenPage()
            Return
        End If
        Dim Address As String = If(McInstanceSelected IsNot Nothing, Settings.Get(Of String)("VersionServerAuthRegister", Instance:=McInstanceSelected), Settings.Get(Of String)("CacheAuthServerRegister"))
        If String.IsNullOrEmpty(New ValidateHttp().Validate(Address)) Then OpenWebsite(Address.Replace("/auth/register", "/user/closet"))
    End Sub

    ''' <summary>当前版本使用的是否是 mcpixelart 皮肤站。</summary>
    Private Function IsSkinSiteAccount() As Boolean
        Dim Server As String = If(McInstanceSelected Is Nothing, Settings.Get(Of String)("CacheAuthServerServer"), Settings.Get(Of String)("VersionServerAuthServer", Instance:=McInstanceSelected))
        Return Server = SkinAuthServer
    End Function

End Class

Imports System.Net
Imports System.Net.Http
Imports System.Text

''' <summary>
''' mcpixelart 皮肤站（https://skin.mcpixelart.com）的网页端接口封装。
''' 接口、校验规则与提示文案均与皮肤站前端保持一致，可参考 C:\codes\mcpixelart-skin-site-front。
''' 除特别说明外，网络请求都必须在非 UI 线程调用。
''' </summary>
Public Module ModSkinSite

#Region "常量"

    ''' <summary>皮肤站网页端 API 根地址。</summary>
    Public Const SkinApiRoot As String = "https://skin.mcpixelart.com/api/skin"
    ''' <summary>材质图片根地址，后面拼接 64 位 Hash。</summary>
    Public Const SkinTextureRoot As String = "https://skin.mcpixelart.com/texture/"
    ''' <summary>皮肤站主页。</summary>
    Public Const SkinSiteHome As String = "https://skin.mcpixelart.com"
    ''' <summary>接收验证码的邮箱，由玩家主动发信。</summary>
    Public Const SkinMailAddress As String = "v@mcpixelart.com"
    ''' <summary>游戏服务器地址。</summary>
    Public Const SkinGameServer As String = "game.mcpixelart.com"
    ''' <summary>Authlib 认证服务器地址。</summary>
    Public Const SkinAuthServer As String = "https://skin.mcpixelart.com/api/yggdrasil"
    ''' <summary>登录页面显示的服务器名称。</summary>
    Public Const SkinServerName As String = "mcpixelart皮肤站"
    ''' <summary>玩家群号码。</summary>
    Public Const SkinQQGroup As String = "719886477"
    ''' <summary>单个账号的角色上限。</summary>
    Public Const SkinMaxProfile As Integer = 10
    ''' <summary>单个账号的材质上限。</summary>
    Public Const SkinMaxTexture As Integer = 20
    ''' <summary>允许上传的图片体积上限（MB）。</summary>
    Public Const SkinMaxImageMb As Double = 2
    ''' <summary>允许上传的图片边长上限（像素）。</summary>
    Public Const SkinMaxImageSize As Integer = 6400
    ''' <summary>验证码有效期（分钟）。</summary>
    Public Const SkinCodeValidMinutes As Integer = 5
    ''' <summary>角色名正则，与网页版一致。</summary>
    Private SkinNamePattern As New Text.RegularExpressions.Regex("^[a-zA-Z0-9](?!.*__)[a-zA-Z0-9_]{2,15}$")
    ''' <summary>邮箱正则，与网页版一致。</summary>
    Private SkinMailPattern As New Text.RegularExpressions.Regex("^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")

#End Region

#Region "配色与字体"

    Public Const SkinColorInk As String = "#14221E"
    Public Const SkinColorMuted As String = "#66726E"
    Public Const SkinColorLine As String = "#DFE4DA"
    Public Const SkinColorPaper As String = "#F5F6F0"
    Public Const SkinColorForest As String = "#163D32"
    Public Const SkinColorForestDark As String = "#0D2A23"
    Public Const SkinColorMint As String = "#87DDC4"
    Public Const SkinColorLime As String = "#CAF174"
    Public Const SkinColorLimeHover As String = "#D5F78B"
    Public Const SkinColorDanger As String = "#D45353"
    Public Const SkinColorDangerDark As String = "#A84747"
    Public Const SkinColorCardLine As String = "#DBE1DA"
    Public Const SkinColorSoftFill As String = "#EEF2EE"
    Public Const SkinColorSoftText As String = "#536B62"
    Public Const SkinColorSidebarText As String = "#65716C"
    ''' <summary>服务器名称的配色，与网页版 .minecraft-motd 一致。</summary>
    Public Const SkinColorName1 As String = "#5ED8D2"
    Public Const SkinColorName2 As String = "#F18DED"
    Public Const SkinColorName3 As String = "#FFF278"
    Public Const SkinColorName4 As String = "#FFBF58"
    Public Const SkinColorName5 As String = "#DB5353"
    ''' <summary>等宽字体，用于角色名、验证码与服务器地址。</summary>
    Public Const SkinFontMono As String = "Consolas, Courier New, monospace"

    Private SkinBrushCache As New ConcurrentDictionary(Of String, SolidColorBrush)
    ''' <summary>按十六进制色值获取画刷（带缓存）。</summary>
    Public Function SkinBrush(Hex As String) As SolidColorBrush
        Dim Cached As SolidColorBrush = Nothing
        If SkinBrushCache.TryGetValue(Hex, Cached) Then Return Cached
        Dim B As New SolidColorBrush(CType(ColorConverter.ConvertFromString(Hex), Color))
        B.Freeze()
        SkinBrushCache(Hex) = B
        Return B
    End Function

#End Region

#Region "数据模型"

    ''' <summary>一份材质（皮肤 + 可选的披风）。</summary>
    Public Class SkinSiteTexture
        Public Property Tid As Integer
        Public Property SkinHash As String = ""
        Public Property Model As String = "default"
        Public Property CapeHash As String = ""
        Friend ReadOnly Property SkinUrl As String
            Get
                Return If(SkinHash = "", "", SkinTextureRoot & SkinHash)
            End Get
        End Property
        Friend ReadOnly Property CapeUrl As String
            Get
                Return If(CapeHash = "", "", SkinTextureRoot & CapeHash)
            End Get
        End Property
        Friend ReadOnly Property ModelTag As String
            Get
                Return If(Model = "slim", "ALEX", "STEVE")
            End Get
        End Property
        Friend ReadOnly Property ModelText As String
            Get
                Return If(Model = "slim", "Alex 细手臂", "Steve 标准")
            End Get
        End Property
    End Class

    ''' <summary>一个角色（游戏名 + 该角色名下的所有材质）。</summary>
    Public Class SkinSiteProfile
        Public Property Pid As String = ""
        Public Property Name As String = ""
        Public Property CurrentTid As Integer?
        Public Property Textures As New List(Of SkinSiteTexture)

        Friend ReadOnly Property ActiveTexture As SkinSiteTexture
            Get
                If CurrentTid Is Nothing Then Return Nothing
                Return Textures.FirstOrDefault(Function(t) t.Tid = CurrentTid.Value)
            End Get
        End Property
        ''' <summary>与网页版一致的一句话描述。</summary>
        Friend ReadOnly Property SummaryText As String
            Get
                If Textures.Count = 0 Then Return "还没有上传皮肤"
                Dim Active = ActiveTexture
                Return $"{Textures.Count} 套材质 · {If(Active?.ModelText, "Steve 标准")}"
            End Get
        End Property
    End Class

    ''' <summary>皮肤站账号的全部数据。</summary>
    Public Class SkinSiteAccount
        Public Property CurrentPid As String = ""
        Public Property Profiles As New List(Of SkinSiteProfile)

        Friend ReadOnly Property DefaultProfile As SkinSiteProfile
            Get
                Return FindProfile(CurrentPid)
            End Get
        End Property
        Friend ReadOnly Property TotalTextures As Integer
            Get
                Return Profiles.Sum(Function(p) p.Textures.Count)
            End Get
        End Property
        Friend Function FindProfile(Pid As String) As SkinSiteProfile
            If String.IsNullOrEmpty(Pid) Then Return Nothing
            Return Profiles.FirstOrDefault(Function(p) p.Pid = Pid)
        End Function
    End Class

    ''' <summary>服务器状态。</summary>
    Public Class SkinSiteServerStatus
        Public Property Online As Boolean
        Public Property Host As String = SkinGameServer
        Public Property Port As Integer = 25565
        Public Property Version As String = ""
        Public Property PlayersOnline As Integer
        Public Property PlayersMax As Integer
        Public Property Motd As String = ""
        Public Property Favicon As ImageSource
    End Class

    ''' <summary>Yggdrasil 登录结果。</summary>
    Public Class SkinSiteLoginResult
        Public Property AccessToken As String = ""
        Public Property ClientToken As String = ""
        Public Property Uuid As String = ""
        Public Property Name As String = ""
    End Class

    ''' <summary>皮肤站返回的错误，StatusCode 为 HTTP 状态码。</summary>
    Public Class SkinSiteException
        Inherits Exception
        Public ReadOnly StatusCode As Integer
        Public Sub New(Message As String, Optional StatusCode As Integer = 0)
            MyBase.New(Message)
            Me.StatusCode = StatusCode
        End Sub
    End Class

#End Region

#Region "本地持久化"

    Private SkinTokenCache As String = Nothing
    Public Property SkinToken As String
        Get
            If SkinTokenCache Is Nothing Then SkinTokenCache = Settings.Get(Of String)("SkinSiteToken")
            Return SkinTokenCache
        End Get
        Set(value As String)
            SkinTokenCache = If(value, "")
            Settings.Set("SkinSiteToken", SkinTokenCache)
        End Set
    End Property

    ''' <summary>上次登录使用的邮箱，已加密保存。</summary>
    Public Property SkinMail As String
        Get
            Return Settings.Get(Of String)("SkinSiteMail")
        End Get
        Set(value As String)
            Settings.Set("SkinSiteMail", If(value, ""))
        End Set
    End Property

    ''' <summary>记住的密码，已加密保存；为空时不自动填充。</summary>
    Public Property SkinPassword As String
        Get
            Return Settings.Get(Of String)("SkinSitePass")
        End Get
        Set(value As String)
            Settings.Set("SkinSitePass", If(value, ""))
        End Set
    End Property

    ''' <summary>是否记住密码。</summary>
    Public Property SkinRemember As Boolean
        Get
            Return Settings.Get(Of Boolean)("SkinSiteRemember")
        End Get
        Set(value As Boolean)
            Settings.Set("SkinSiteRemember", value)
        End Set
    End Property

    ''' <summary>当前的四位验证码，持久化保存，直到被成功使用。</summary>
    Public Property SkinVerifyCode As String
        Get
            Return Settings.Get(Of String)("SkinSiteVerifyCode")
        End Get
        Set(value As String)
            Settings.Set("SkinSiteVerifyCode", If(value, ""))
        End Set
    End Property

    ''' <summary>是否在登录后自动把账号设为游戏登录账号。</summary>
    Public Property SkinAutoApply As Boolean
        Get
            Return Settings.Get(Of Boolean)("SkinSiteOneKey")
        End Get
        Set(value As Boolean)
            Settings.Set("SkinSiteOneKey", value)
        End Set
    End Property

    ''' <summary>是否已登录（本地存有令牌）。</summary>
    Public ReadOnly Property SkinLoggedIn As Boolean
        Get
            Return SkinToken <> ""
        End Get
    End Property

    ''' <summary>把角色与材质写入本地缓存，用于下次秒开。</summary>
    Public Sub SkinSaveCache(Account As SkinSiteAccount)
        Try
            Settings.Set("SkinSiteCache", JsonConvert.SerializeObject(Account))
        Catch ex As Exception
            Logger.Warn(ex, "保存皮肤站缓存失败")
        End Try
    End Sub

    ''' <summary>读取本地缓存的角色与材质。</summary>
    Public Function SkinLoadCache() As SkinSiteAccount
        Try
            Dim Text As String = Settings.Get(Of String)("SkinSiteCache")
            If String.IsNullOrWhiteSpace(Text) Then Return Nothing
            Dim Result As SkinSiteAccount = ParseSkinAccount(JToken.Parse(Text))
            If Result.Profiles.Count = 0 AndAlso Result.CurrentPid = "" Then Return Nothing
            Return Result
        Catch ex As Exception
            Logger.Warn(ex, "读取皮肤站缓存失败")
            Return Nothing
        End Try
    End Function

    ''' <summary>退出登录，并清除与游戏登录相关的缓存。</summary>
    Public Sub SkinLogout()
        SkinToken = ""
        Settings.Set("SkinSiteCache", "")
        Settings.Set("CacheAuthAccess", "")
        Settings.Set("CacheAuthClient", "")
        Settings.Set("CacheAuthUuid", "")
        Settings.Set("CacheAuthName", "")
        Try
            McLoginAuthLoader.Input = Nothing
        Catch ex As Exception
            Logger.Warn(ex, "重置登录加载器输入失败")
        End Try
    End Sub

    ''' <summary>打开皮肤站页面。</summary>
    Public Sub SkinOpenPage()
        RunInUi(
        Sub()
            FrmMain.PageChange(FormMain.PageType.SkinSite)
        End Sub)
    End Sub

#End Region

#Region "HTTP"

    Private SkinClientLock As New Object
    Private SkinClient As HttpClient

    Private Function GetSkinClient() As HttpClient
        SyncLock SkinClientLock
            If SkinClient Is Nothing Then
                Dim Handler As New HttpClientHandler With {
                    .AutomaticDecompression = DecompressionMethods.GZip Or DecompressionMethods.Deflate,
                    .UseCookies = False,
                    .AllowAutoRedirect = True
                }
                SkinClient = New HttpClient(Handler) With {.Timeout = TimeSpan.FromSeconds(60)}
                SkinClient.DefaultRequestHeaders.UserAgent.ParseAdd("PCL2/SkinSite")
            End If
            Return SkinClient
        End SyncLock
    End Function

    ''' <summary>发送请求并解析 JSON。失败时抛出 <see cref="SkinSiteException"/>。</summary>
    Private Function SkinSend(Url As String, Method As HttpMethod, Optional Body As Object = Nothing, Optional UseToken As Boolean = False) As JToken
        If RunInUi() Then Throw New Exception("皮肤站请求不能在 UI 线程中执行")
        Dim Request As New HttpRequestMessage(Method, Url)
        Try
            If Body IsNot Nothing Then
                If TypeOf Body Is HttpContent Then
                    Request.Content = DirectCast(Body, HttpContent)
                Else
                    Request.Content = New StringContent(DirectCast(Body, JToken).ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json")
                End If
            End If
            If UseToken AndAlso SkinToken <> "" Then Request.Headers.TryAddWithoutValidation("Authorization", "Bearer " & SkinToken)
            Dim Response As HttpResponseMessage = Nothing
            Try
                Response = GetSkinClient().SendAsync(Request, HttpCompletionOption.ResponseContentRead).GetAwaiter().GetResult()
            Catch ex As TaskCanceledException
                Throw New SkinSiteException("连接皮肤站超时，请检查网络后重试。")
            Catch ex As HttpRequestException
                Throw New SkinSiteException("无法连接到皮肤站，请检查网络后重试。")
            End Try
            Using Response
                Dim Text As String = Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                If Not Response.IsSuccessStatusCode Then
                    If CInt(Response.StatusCode) = 401 AndAlso UseToken Then SkinToken = ""
                    Throw New SkinSiteException(GetSkinErrorMessage(Response.StatusCode, Text), CInt(Response.StatusCode))
                End If
                If String.IsNullOrWhiteSpace(Text) Then Return Nothing
                Try
                    Return JToken.Parse(Text)
                Catch ex As Exception
                    Throw New SkinSiteException("皮肤站返回了无法解析的数据，请稍后再试。")
                End Try
            End Using
        Finally
            Request.Dispose()
        End Try
    End Function

    ''' <summary>与网页版 describeError 一致的错误文案。</summary>
    Private Function GetSkinErrorMessage(Code As HttpStatusCode, Body As String) As String
        Dim Detail As String = ""
        Try
            Dim Json As JToken = JToken.Parse(Body)
            If Json.Type = JTokenType.String Then
                Detail = Json.ToString
            ElseIf Json.Type = JTokenType.Object Then
                Detail = SkinTokenText(Json, "message")
                If Detail = "" Then Detail = SkinTokenText(Json, "errorMessage")
                If Detail = "" Then Detail = SkinTokenText(Json, "error")
                If Detail = "" Then Detail = SkinTokenText(Json, "detail")
            End If
        Catch
            Detail = If(Body, "").Trim()
        End Try
        If Detail.Contains("Invalid credentials") OrElse Detail.Contains("ForbiddenOperationException") Then Return "邮箱或密码不正确。"
        If Detail <> "" AndAlso Detail.Length <= 160 Then Return Detail
        Select Case CInt(Code)
            Case 400
                Return "提交的信息有误，请检查后再试。"
            Case 401
                Return "登录已过期，请重新登录。"
            Case 403
                Return "当前账号没有执行此操作的权限。"
            Case 404
                Return "没有找到对应的数据，请刷新后重试。"
            Case 413
                Return "文件太大，请压缩到 2MB 以内。"
            Case 429
                Return "操作有点快，请稍等片刻再试。"
            Case >= 500
                Return "皮肤站暂时开小差了，请稍后再试。"
        End Select
        Return "请求没有完成，请检查网络后重试。"
    End Function

    Private Function SkinTokenText(Json As JToken, Name As String) As String
        Dim Value As JToken = Json(Name)
        If Value Is Nothing OrElse Value.Type = JTokenType.Null Then Return ""
        Return Value.ToString
    End Function

#End Region

#Region "账号接口"

    ''' <summary>检查皮肤站是否已收到该验证码对应的邮件。</summary>
    Public Function SkinVerifyMail(Mail As String, Code As String) As Boolean
        Dim Result As JToken = SkinSend(SkinApiRoot & "/register/verify", HttpMethod.Post,
            New JObject(New JProperty("mail", Mail), New JProperty("verify_code", Code)))
        Return Result IsNot Nothing AndAlso Result("valid") IsNot Nothing AndAlso CBool(Result("valid"))
    End Function

    ''' <summary>注册账号，返回登录令牌。</summary>
    Public Function SkinRegister(Mail As String, Password As String, Code As String) As String
        Dim Result As JToken = SkinSend(SkinApiRoot & "/register/mail", HttpMethod.Post,
            New JObject(New JProperty("mail", Mail), New JProperty("password", Password), New JProperty("verify_code", Code)))
        If Result Is Nothing OrElse Result("token") Is Nothing Then Throw New SkinSiteException("注册账户失败，请稍后再试。")
        Return Result("token").ToString
    End Function

    ''' <summary>使用邮箱（或角色名）与密码登录，返回登录令牌。</summary>
    Public Function SkinLogin(Mail As String, Password As String) As String
        Dim Result As JToken = SkinSend(SkinApiRoot & "/login", HttpMethod.Post,
            New JObject(New JProperty("mail", Mail), New JProperty("password", Password)))
        If Result Is Nothing OrElse Result("token") Is Nothing Then Throw New SkinSiteException("登录失败，请检查邮箱与密码。")
        Return Result("token").ToString
    End Function

    ''' <summary>修改密码（忘记密码时同样使用）。</summary>
    Public Sub SkinModifyPassword(Mail As String, NewPassword As String, Code As String)
        SkinSend(SkinApiRoot & "/user/modify/password", HttpMethod.Post,
            New JObject(New JProperty("mail", Mail), New JProperty("new_password", NewPassword), New JProperty("verify_code", Code)))
    End Sub

#End Region

#Region "角色与材质接口"

    ''' <summary>获取账号下的所有角色与材质。</summary>
    Public Function SkinFetchProfiles() As SkinSiteAccount
        Return ParseSkinAccount(SkinSend(SkinApiRoot & "/user/profile/fetch", HttpMethod.Get, UseToken:=True))
    End Function

    ''' <summary>检查角色名是否可用。</summary>
    Public Function SkinCheckName(GameName As String) As Boolean
        Dim Result As JToken = SkinSend(SkinApiRoot & "/user/profile/check/name?gamename=" & Uri.EscapeDataString(GameName), HttpMethod.Get, UseToken:=True)
        Return Result IsNot Nothing AndAlso Result("valid") IsNot Nothing AndAlso CBool(Result("valid"))
    End Function

    ''' <summary>创建角色，返回角色编号。</summary>
    Public Function SkinCreateProfile(GameName As String) As String
        Dim Result As JToken = SkinSend(SkinApiRoot & "/user/profile/create", HttpMethod.Post,
            New JObject(New JProperty("gamename", GameName)), UseToken:=True)
        If Result Is Nothing OrElse Result("pid") Is Nothing Then Throw New SkinSiteException("新建角色失败，请稍后再试。")
        Return Result("pid").ToString
    End Function

    ''' <summary>把角色设为默认角色。</summary>
    Public Sub SkinSetDefaultProfile(Pid As String)
        SkinSend(SkinApiRoot & "/user/profile/update/current", HttpMethod.Post,
            New JObject(New JProperty("pid", Pid)), UseToken:=True)
    End Sub

    ''' <summary>删除角色。</summary>
    Public Sub SkinDeleteProfile(Pid As String)
        SkinSend(SkinApiRoot & "/user/profile/" & Uri.EscapeDataString(Pid), HttpMethod.Delete, UseToken:=True)
    End Sub

    ''' <summary>上传皮肤并创建材质，返回材质编号。</summary>
    Public Function SkinUploadSkin(Pid As String, FilePath As String, Model As String) As Integer
        Using Content As New MultipartFormDataContent
            Content.Add(New StringContent(Model), "model")
            Content.Add(SkinPngContent(FilePath), "file", IO.Path.GetFileName(FilePath))
            Dim Result As JToken = SkinSend(SkinApiRoot & "/user/profile/" & Uri.EscapeDataString(Pid) & "/skin", HttpMethod.Put, Content, UseToken:=True)
            If Result Is Nothing OrElse Result("tid") Is Nothing Then Throw New SkinSiteException("皮肤站没有返回材质编号。")
            Return CInt(Result("tid"))
        End Using
    End Function

    ''' <summary>为已有材质上传披风。</summary>
    Public Sub SkinUploadCape(Pid As String, Tid As Integer, FilePath As String)
        Using Content As New MultipartFormDataContent
            Content.Add(New StringContent(Tid.ToString), "tid")
            Content.Add(SkinPngContent(FilePath), "file", IO.Path.GetFileName(FilePath))
            SkinSend(SkinApiRoot & "/user/profile/" & Uri.EscapeDataString(Pid) & "/cape", HttpMethod.Put, Content, UseToken:=True)
        End Using
    End Sub

    ''' <summary>把材质设为角色当前使用的材质。</summary>
    Public Sub SkinSetDefaultTexture(Pid As String, Tid As Integer)
        SkinSend(SkinApiRoot & "/user/profile/update/texture", HttpMethod.Post,
            New JObject(New JProperty("pid", Pid), New JProperty("tid", Tid)), UseToken:=True)
    End Sub

    ''' <summary>删除材质。</summary>
    Public Sub SkinDeleteTexture(Tid As Integer)
        SkinSend(SkinApiRoot & "/user/texture/" & Tid, HttpMethod.Delete, UseToken:=True)
    End Sub

    ''' <summary>获取服务器状态。</summary>
    Public Function SkinFetchServerStatus() As SkinSiteServerStatus
        Dim Result As JToken = SkinSend(SkinApiRoot & "/server/status", HttpMethod.Get)
        Dim Status As New SkinSiteServerStatus
        If Result Is Nothing Then Return Status
        Status.Online = Result("online") IsNot Nothing AndAlso CBool(Result("online"))
        Status.Host = If(SkinTokenText(Result, "host"), SkinGameServer)
        Status.Port = If(Result("port") Is Nothing, 25565, CInt(Result("port")))
        Status.Version = SkinTokenText(Result, "version")
        Status.PlayersOnline = If(Result("playersOnline") Is Nothing, 0, CInt(Result("playersOnline")))
        Status.PlayersMax = If(Result("playersMax") Is Nothing, 0, CInt(Result("playersMax")))
        Status.Motd = SkinTokenText(Result, "motd")
        '服务器图标是 data:image/png;base64 形式，直接解码显示
        Dim Favicon As String = SkinTokenText(Result, "favicon")
        If Favicon.StartsWithF("data:image/png;base64,") Then
            Try
                Dim Bytes As Byte() = Convert.FromBase64String(Favicon.Substring("data:image/png;base64,".Length))
                Status.Favicon = SkinBytesToImage(Bytes)
            Catch ex As Exception
                Logger.Warn(ex, "解析服务器图标失败")
            End Try
        End If
        Return Status
    End Function

#End Region

#Region "解析"

    ''' <summary>把接口返回的 JSON 解析为账号数据。</summary>
    Public Function ParseSkinAccount(Json As JToken) As SkinSiteAccount
        Dim Result As New SkinSiteAccount
        If Json Is Nothing OrElse Json.Type <> JTokenType.Object Then Return Result
        Result.CurrentPid = SkinTokenText(Json, "currentPid")
        Dim Profiles As JToken = Json("profiles")
        If Profiles Is Nothing Then Return Result
        For Each ProfileJson As JToken In Profiles
            Dim Profile As New SkinSiteProfile With {
                .Pid = SkinTokenText(ProfileJson, "pid"),
                .Name = SkinTokenText(ProfileJson, "pname")
            }
            Dim CurrentTid As JToken = ProfileJson("currentTid")
            If CurrentTid IsNot Nothing AndAlso CurrentTid.Type <> JTokenType.Null Then Profile.CurrentTid = CInt(CurrentTid)
            Dim Textures As JToken = ProfileJson("textures")
            If Textures IsNot Nothing Then
                For Each TextureJson As JToken In Textures
                    Profile.Textures.Add(New SkinSiteTexture With {
                        .Tid = CInt(TextureJson("tid")),
                        .SkinHash = SkinTokenText(TextureJson, "skin"),
                        .Model = If(SkinTokenText(TextureJson, "model") = "slim", "slim", "default"),
                        .CapeHash = SkinTokenText(TextureJson, "cape")
                    })
                Next
            End If
            Result.Profiles.Add(Profile)
        Next
        Return Result
    End Function

#End Region

#Region "校验"

    ''' <summary>邮箱格式校验，与网页版一致；返回空字符串表示通过。</summary>
    Public Function SkinValidateMail(Mail As String) As String
        If String.IsNullOrWhiteSpace(Mail) Then Return "请输入正确的邮箱地址。"
        If Mail.Length > 80 Then Return "邮箱地址太长了。"
        If Not SkinMailPattern.IsMatch(Mail.Trim) Then Return "请输入正确的邮箱地址。"
        Return ""
    End Function

    ''' <summary>密码校验，与网页版一致：6～32 位且至少包含两类字符。</summary>
    Public Function SkinValidatePassword(Password As String) As String
        If Password Is Nothing OrElse Password.Length < 6 OrElse Password.Length > 32 Then Return "密码需要为 6–32 位。"
        Dim Types As Integer = 0
        If Text.RegularExpressions.Regex.IsMatch(Password, "[a-z]") Then Types += 1
        If Text.RegularExpressions.Regex.IsMatch(Password, "[A-Z]") Then Types += 1
        If Text.RegularExpressions.Regex.IsMatch(Password, "\d") Then Types += 1
        If Text.RegularExpressions.Regex.IsMatch(Password, "[^a-zA-Z0-9]") Then Types += 1
        If Types < 2 Then Return "请至少混合字母、数字或符号中的两类。"
        Return ""
    End Function

    ''' <summary>角色名校验，与网页版一致；返回空字符串表示通过。</summary>
    Public Function SkinValidateGameName(GameName As String) As String
        If String.IsNullOrWhiteSpace(GameName) Then Return "请输入角色名。"
        If GameName.Trim.StartsWithF("bot_", True) Then Return "角色名不能以 bot_ 开头。"
        If Not SkinNamePattern.IsMatch(GameName.Trim) Then Return "请输入 3–16 位字母、数字或下划线，且不能有连续下划线。"
        Return ""
    End Function

    ''' <summary>读取 PNG 文件头中的尺寸。</summary>
    Public Function SkinReadPngSize(FilePath As String) As (Width As Integer, Height As Integer)
        Using Stream As New IO.FileStream(FilePath, IO.FileMode.Open, IO.FileAccess.Read)
            Dim Header(23) As Byte
            If Stream.Read(Header, 0, 24) < 24 Then Throw New SkinSiteException("图片无法读取，请换一张 PNG。")
            If Not (Header(0) = &H89 AndAlso Header(1) = &H50 AndAlso Header(2) = &H4E AndAlso Header(3) = &H47) Then
                Throw New SkinSiteException("仅支持 PNG 图片。")
            End If
            Dim W As Integer = (CInt(Header(16)) << 24) Or (CInt(Header(17)) << 16) Or (CInt(Header(18)) << 8) Or CInt(Header(19))
            Dim H As Integer = (CInt(Header(20)) << 24) Or (CInt(Header(21)) << 16) Or (CInt(Header(22)) << 8) Or CInt(Header(23))
            Return (W, H)
        End Using
    End Function

    ''' <summary>上传前的本地校验，规则与网页版 validatePng 一致。</summary>
    Public Sub SkinValidatePng(FilePath As String)
        Dim Info As New IO.FileInfo(FilePath)
        If Not Info.Exists Then Throw New SkinSiteException("找不到所选择的图片文件。")
        If Info.Extension.ToLower <> ".png" Then Throw New SkinSiteException("仅支持 PNG 图片。")
        If Info.Length > SkinMaxImageMb * 1024 * 1024 Then Throw New SkinSiteException($"图片不能超过 {SkinMaxImageMb:0}MB，请压缩后再上传。")
        Dim Size = SkinReadPngSize(FilePath)
        If Size.Width > SkinMaxImageSize OrElse Size.Height > SkinMaxImageSize Then
            Throw New SkinSiteException($"图片尺寸不能超过 {SkinMaxImageSize} × {SkinMaxImageSize}。")
        End If
    End Sub

    Private Function SkinPngContent(FilePath As String) As HttpContent
        Dim Content As New StreamContent(IO.File.OpenRead(FilePath))
        Content.Headers.ContentType = New Net.Http.Headers.MediaTypeHeaderValue("image/png")
        Return Content
    End Function

    Private SkinRandom As New Random
    ''' <summary>生成并保存一个新的四位验证码。</summary>
    Public Function SkinNewCode() As String
        Dim Code As String
        SyncLock SkinRandom
            Code = SkinRandom.Next(1000, 10000).ToString
        End SyncLock
        SkinVerifyCode = Code
        Return Code
    End Function

    ''' <summary>取当前验证码，没有时生成一个。</summary>
    Public Function SkinCurrentCode() As String
        If SkinVerifyCode = "" Then Return SkinNewCode()
        Return SkinVerifyCode
    End Function

#End Region

#Region "版本与邮箱校验"

    ''' <summary>服务器版本号的缓存，避免每次切换版本都请求一次。</summary>
    Private SkinServerVersionCache As String = ""
    ''' <summary>本运行期内已经提示过的版本组合，避免反复打扰。</summary>
    Private ReadOnly SkinVersionWarned As New HashSet(Of String)

    ''' <summary>
    ''' 校验所选游戏版本与服务器版本是否一致，不一致时给出提示。
    ''' 会在后台获取一次服务器状态，之后使用缓存。
    ''' </summary>
    Public Sub SkinCheckServerVersion(Instance As McInstance)
        Try
            If Instance Is Nothing OrElse Instance.Version Is Nothing Then Return
            Dim ClientVersion As String = Instance.Version.VanillaName
            If String.IsNullOrWhiteSpace(ClientVersion) Then Return
            '已经拿到过服务器版本，直接比对
            If SkinServerVersionCache <> "" Then
                SkinShowVersionHint(ClientVersion, SkinServerVersionCache)
                Return
            End If
            '后台获取服务器版本后再比对
            RunInNewThread(
            Sub()
                Try
                    Dim Status As SkinSiteServerStatus = SkinFetchServerStatus()
                    If Status Is Nothing OrElse Not Status.Online OrElse String.IsNullOrWhiteSpace(Status.Version) Then Return
                    SkinServerVersionCache = Status.Version
                    RunInUi(Sub() SkinShowVersionHint(ClientVersion, Status.Version))
                Catch ex As Exception
                    Logger.Warn(ex, "获取服务器版本失败，跳过版本校验")
                End Try
            End Sub, "Skin Site Version Check")
        Catch ex As Exception
            Logger.Warn(ex, "校验游戏版本与服务器版本失败")
        End Try
    End Sub

    ''' <summary>版本不一致时给出提示。相同的版本组合只提示一次。</summary>
    Private Sub SkinShowVersionHint(ClientVersion As String, ServerVersion As String)
        Dim Key As String = ClientVersion & "|" & ServerVersion
        If SkinVersionWarned.Contains(Key) Then Return
        SkinVersionWarned.Add(Key)
        If SkinVersionEquals(ClientVersion, ServerVersion) Then Return
        Hint($"你选择的版本是 {ClientVersion}，服务器当前版本为 {ServerVersion}，版本不一致可能无法进入服务器。", HintType.Red, False)
    End Sub

    ''' <summary>比较客户端与服务器的版本号，忽略大小写、v 前缀与形如 “Paper ” 的前缀。</summary>
    Public Function SkinVersionEquals(ClientVersion As String, ServerVersion As String) As Boolean
        Return NormalizeVersion(ClientVersion) = NormalizeVersion(ServerVersion)
    End Function
    Private Function NormalizeVersion(Value As String) As String
        Dim Result As String = If(Value, "").Trim.ToLower
        If Result.StartsWithF("v") Then Result = Result.Substring(1)
        '部分服务端会返回 “Paper 1.20.1” 这类带前缀的版本名，取最后一段
        If Result.Contains(" ") Then Result = Result.Split(" "c).Last.Trim()
        Return Result
    End Function

    ''' <summary>
    ''' 是否为 Outlook / Hotmail / Live 邮箱。
    ''' 这些邮箱经常收不到本站的验证邮件，注册时应当建议玩家更换。
    ''' </summary>
    Public Function SkinIsOutlookMail(Mail As String) As Boolean
        Dim Value As String = If(Mail, "").Trim.ToLower
        Dim AtIndex As Integer = Value.LastIndexOf("@"c)
        If AtIndex < 0 Then Return False
        Dim Domain As String = Value.Substring(AtIndex + 1).Trim()
        If Domain = "" Then Return False
        '只看邮箱域名的主体部分，涵盖 outlook.com、outlook.jp、hotmail.com、live.cn 等
        Dim MainDomain As String = Domain.Split("."c)(0)
        Return MainDomain = "outlook" OrElse MainDomain = "hotmail" OrElse MainDomain = "live"
    End Function

#End Region

#Region "图片处理"

    Private SkinImageCache As New ConcurrentDictionary(Of String, BitmapSource)

    ''' <summary>把字节数组解码为已冻结的图片。</summary>
    Public Function SkinBytesToImage(Bytes As Byte()) As BitmapSource
        If Bytes Is Nothing OrElse Bytes.Length = 0 Then Return Nothing
        Using Stream As New IO.MemoryStream(Bytes)
            Dim Bitmap As New BitmapImage
            Bitmap.BeginInit()
            Bitmap.CacheOption = BitmapCacheOption.OnLoad
            Bitmap.StreamSource = Stream
            Bitmap.EndInit()
            Bitmap.Freeze()
            Return Bitmap
        End Using
    End Function

    ''' <summary>下载材质图片并缓存。必须在非 UI 线程调用。</summary>
    Public Function SkinLoadTexture(Url As String) As BitmapSource
        If String.IsNullOrWhiteSpace(Url) Then Return Nothing
        Dim Cached As BitmapSource = Nothing
        If SkinImageCache.TryGetValue(Url, Cached) Then Return Cached
        Try
            Dim Bytes As Byte() = GetSkinClient().GetByteArrayAsync(Url).GetAwaiter().GetResult()
            Dim Image As BitmapSource = SkinBytesToImage(Bytes)
            If Image IsNot Nothing Then SkinImageCache(Url) = Image
            Return Image
        Catch ex As Exception
            Logger.Warn(ex, $"下载材质图片失败（{Url}）")
            Return Nothing
        End Try
    End Function

    ''' <summary>读取已缓存的材质图片，未下载时返回 Nothing。可以在 UI 线程调用。</summary>
    Public Function SkinGetTextureCached(Url As String) As BitmapSource
        If String.IsNullOrWhiteSpace(Url) Then Return Nothing
        Dim Cached As BitmapSource = Nothing
        If SkinImageCache.TryGetValue(Url, Cached) Then Return Cached
        Return Nothing
    End Function

    ''' <summary>按像素区域裁剪材质图片，超出范围时返回 Nothing。</summary>
    Public Function SkinCrop(Image As BitmapSource, X As Integer, Y As Integer, Width As Integer, Height As Integer) As BitmapSource
        If Image Is Nothing Then Return Nothing
        If X < 0 OrElse Y < 0 OrElse X + Width > Image.PixelWidth OrElse Y + Height > Image.PixelHeight Then Return Nothing
        Dim Cropped As New CroppedBitmap(Image, New Int32Rect(X, Y, Width, Height))
        Cropped.Freeze()
        Return Cropped
    End Function

    ''' <summary>
    ''' 与网页版 SkinFigure 相同的部位坐标（64×64 皮肤）。
    ''' 返回 Nothing 表示该部位在 64×32 的旧皮肤中不存在。
    ''' </summary>
    Public Function SkinFigureParts(Image As BitmapSource) As List(Of SkinSiteFigurePart)
        Dim Result As New List(Of SkinSiteFigurePart)
        If Image Is Nothing Then Return Result
        '头、身体、左臂、右臂、左腿、右腿
        Dim Head = SkinCrop(Image, 8, 8, 8, 8)
        Dim Body = SkinCrop(Image, 20, 20, 8, 12)
        Dim ArmLeft = SkinCrop(Image, 44, 20, 4, 12)
        Dim ArmRight = SkinCrop(Image, 36, 52, 4, 12)
        Dim LegLeft = SkinCrop(Image, 4, 20, 4, 12)
        Dim LegRight = SkinCrop(Image, 20, 52, 4, 12)
        '64×32 的旧皮肤没有背面手臂与腿部，镜像使用正面部位
        If ArmRight Is Nothing Then ArmRight = ArmLeft
        If LegRight Is Nothing Then LegRight = LegLeft
        If Head IsNot Nothing Then Result.Add(New SkinSiteFigurePart("head", Head))
        If Body IsNot Nothing Then Result.Add(New SkinSiteFigurePart("body", Body))
        If ArmLeft IsNot Nothing Then Result.Add(New SkinSiteFigurePart("armLeft", ArmLeft))
        If ArmRight IsNot Nothing Then Result.Add(New SkinSiteFigurePart("armRight", ArmRight))
        If LegLeft IsNot Nothing Then Result.Add(New SkinSiteFigurePart("legLeft", LegLeft))
        If LegRight IsNot Nothing Then Result.Add(New SkinSiteFigurePart("legRight", LegRight))
        Return Result
    End Function

    Public Class SkinSiteFigurePart
        Public Sub New(Name As String, Image As BitmapSource)
            Me.Name = Name
            Me.Image = Image
        End Sub
        Public ReadOnly Name As String
        Public ReadOnly Image As BitmapSource
    End Class

    ''' <summary>裁剪披风贴图中正反两面所在的区域（与网页版 CapeFigure 一致）。</summary>
    Public Function SkinCropCape(Image As BitmapSource) As BitmapSource
        If Image Is Nothing Then Return Nothing
        Dim Scale As Integer = If(Image.PixelHeight >= 32, Math.Max(1, CInt(Math.Round(Image.PixelHeight / 32.0))), 1)
        Return SkinCrop(Image, 1 * Scale, 1 * Scale, 21 * Scale, 16 * Scale)
    End Function

#End Region

#Region "游戏登录"

    ''' <summary>用皮肤站账号进行 Yggdrasil 认证，角色由皮肤站上的默认角色决定。</summary>
    Public Function SkinYggdrasilLogin(Mail As String, Password As String) As SkinSiteLoginResult
        Dim Body As New JObject(
            New JProperty("agent", New JObject(New JProperty("name", "Minecraft"), New JProperty("version", 1))),
            New JProperty("username", Mail),
            New JProperty("password", Password),
            New JProperty("clientToken", Guid.NewGuid().ToString("N")),
            New JProperty("requestUser", False))
        Dim Result As JToken = SkinSend(SkinAuthServer & "/authserver/authenticate", HttpMethod.Post, Body)
        If Result Is Nothing Then Throw New SkinSiteException("登录游戏失败，请稍后再试。")
        Dim Profiles As JToken = Result("availableProfiles")
        If Profiles Is Nothing OrElse Profiles.Count = 0 Then Throw New SkinSiteException("这个账号还没有角色，请先创建角色。")
        Dim Selected As JToken = Result("selectedProfile")
        If Selected Is Nothing Then Throw New SkinSiteException("无法确定要使用的角色，请先在皮肤站里设置一个默认角色。")
        Return New SkinSiteLoginResult With {
            .AccessToken = SkinTokenText(Result, "accessToken"),
            .ClientToken = SkinTokenText(Result, "clientToken"),
            .Uuid = SkinTokenText(Selected, "id"),
            .Name = SkinTokenText(Selected, "name")
        }
    End Function

    ''' <summary>把皮肤站账号设为启动器使用的游戏账号，并配置自动进入服务器。</summary>
    Public Sub SkinApplyLoginAccount(Instance As McInstance, Mail As String, Password As String, Login As SkinSiteLoginResult)
        Settings.Set("CacheAuthUsername", Mail)
        Settings.Set("CacheAuthPass", Password)
        If Login IsNot Nothing Then
            Settings.Set("CacheAuthAccess", Login.AccessToken)
            Settings.Set("CacheAuthClient", Login.ClientToken)
            Settings.Set("CacheAuthUuid", Login.Uuid)
            Settings.Set("CacheAuthName", Login.Name)
        End If
        Settings.Set("CacheAuthServerServer", SkinAuthServer)
        Settings.Set("CacheAuthServerName", SkinServerName)
        Settings.Set("CacheAuthServerRegister", SkinSiteHome)
        Settings.Set("LoginType", McLoginType.Auth)
        SkinRememberCredential(Mail, Password)
        Try
            McLoginAuthLoader.Input = Nothing
        Catch ex As Exception
            Logger.Warn(ex, "重置登录加载器输入失败")
        End Try
        If Instance IsNot Nothing Then
            Settings.Set("VersionServerLogin", 4, Instance:=Instance)
            Settings.Set("VersionServerAuthServer", SkinAuthServer, Instance:=Instance)
            Settings.Set("VersionServerAuthRegister", SkinSiteHome, Instance:=Instance)
            Settings.Set("VersionServerAuthName", SkinServerName, Instance:=Instance)
            Settings.Set("VersionServerEnter", SkinGameServer, Instance:=Instance)
        End If
    End Sub

    ''' <summary>把账号记入 Authlib 登录页的“记住密码”列表。</summary>
    Private Sub SkinRememberCredential(Mail As String, Password As String)
        If Mail = "" Then Return
        Try
            Dim Mails As New List(Of String), Passwords As New List(Of String)
            Dim RawMails As String = Settings.Get(Of String)("LoginAuthEmail")
            Dim RawPasswords As String = Settings.Get(Of String)("LoginAuthPass")
            If RawMails <> "" Then Mails.AddRange(RawMails.Split("¨"))
            If RawPasswords <> "" Then Passwords.AddRange(RawPasswords.Split("¨"))
            While Passwords.Count < Mails.Count
                Passwords.Add("")
            End While
            Dim Index As Integer = Mails.IndexOf(Mail)
            If Index >= 0 Then
                Passwords(Index) = Password
            Else
                Mails.Insert(0, Mail)
                Passwords.Insert(0, Password)
            End If
            If Mails.Count > 10 Then
                Mails = Mails.Take(10).ToList()
                Passwords = Passwords.Take(10).ToList()
            End If
            Settings.Set("LoginAuthEmail", Mails.Join("¨"c))
            Settings.Set("LoginAuthPass", Passwords.Join("¨"c))
        Catch ex As Exception
            Logger.Warn(ex, "保存皮肤站登录记录失败")
        End Try
    End Sub

#End Region

End Module

Imports System.Windows.Media.Effects
Imports System.Windows.Shapes
Imports System.Windows.Threading

''' <summary>
''' 皮肤站页面：管理 mcpixelart 皮肤站的角色与材质。
''' 布局按桌面应用组织：左侧角色列表、右侧材质列表，两个列表各自滚动，页面本身不整体滚动。
''' </summary>
Public Class PageSkinSite

#Region "状态"

    Private Shadows IsLoaded As Boolean = False
    ''' <summary>所有皮肤站请求串行执行，避免并发与重复提交。</summary>
    Private Shared ReadOnly SkinLock As New Object
    ''' <summary>角色与材质数据。</summary>
    Private Account As SkinSiteAccount
    ''' <summary>当前选中的角色。</summary>
    Private SelectedPid As String = ""
    ''' <summary>是否正在加载角色数据。</summary>
    Private Loading As Boolean = False
    ''' <summary>角色名查重定时器（对应网页版 800ms 防抖）。</summary>
    Private NameCheck As DispatcherTimer
    Private NameCheckToken As Integer = 0
    ''' <summary>已经确认可用的角色名。</summary>
    Private NameAvailable As String = ""

    '弹窗状态
    Private ModalPrimary As ModSkinSiteUi.SkinButtonRef
    Private ModalError As Border
    Private ModalErrorText As TextBlock
    Private ModalButtons As New List(Of ModSkinSiteUi.SkinButtonRef)
    Private InpNewName As ModSkinSiteUi.SkinInputRef
    Private LabNewNameHint As TextBlock
    Private BrdNewNameStatus As Border
    Private PendingSkinFile As String = ""
    Private PendingSkinModel As String = "default"
    Private PendingCapeTarget As SkinSiteTexture
    Private PendingCapeFile As String = ""

    '账号安全表单状态
    Private SecStep As Integer = 1
    Private SecVerified As Boolean = False
    Private SecCode As String = ""
    Private SecError As String = ""
    Private InpSecMail As ModSkinSiteUi.SkinInputRef
    Private InpSecPass As ModSkinSiteUi.SkinInputRef
    Private LabSecError As TextBlock

#End Region

#Region "加载与刷新"

    Private Sub PageSkinSite_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If IsLoaded Then Return
        IsLoaded = True
        InitModal()
        Rebuild()
        If SkinLoggedIn Then RefreshAccount()
    End Sub

    Public Sub Reload()
        Rebuild()
    End Sub

    ''' <summary>拉取角色与材质，并预下载材质贴图。</summary>
    Public Sub RefreshAccount(Optional ShowHintOnSuccess As Boolean = False)
        If Not SkinLoggedIn Then Return
        Loading = True
        RunTask(
        Sub()
            Dim Data = SkinFetchProfiles()
            SkinSaveCache(Data)
            '提前下载所有材质贴图与披风，之后可以直接用缓存渲染
            For Each Profile In Data.Profiles
                For Each Texture In Profile.Textures
                    SkinLoadTexture(Texture.SkinUrl)
                    If Texture.CapeHash <> "" Then SkinLoadTexture(Texture.CapeUrl)
                Next
            Next
            Account = Data
        End Sub,
        Sub()
            Loading = False
            If SelectedPid <> "" AndAlso Account IsNot Nothing AndAlso Account.FindProfile(SelectedPid) Is Nothing Then SelectedPid = ""
            Rebuild()
            If ShowHintOnSuccess Then Hint("角色与材质已刷新", HintType.Green, False)
        End Sub)
    End Sub

    ''' <summary>皮肤站令牌失效时回到登录界面。</summary>
    Private Sub OnTokenExpired()
        SkinLogout()
        Account = Nothing
        SelectedPid = ""
        Rebuild()
    End Sub

#End Region

#Region "网络任务"

    ''' <summary>在后台串行执行皮肤站操作，完成后回到 UI 线程。</summary>
    Private Sub RunTask(Action As Action, Optional OnSuccess As Action = Nothing)
        RunInThread(
        Sub()
            SyncLock SkinLock
                Try
                    Action.Invoke()
                    RunInUi(Sub() If OnSuccess IsNot Nothing Then OnSuccess.Invoke())
                Catch ex As Exception
                    RunInUi(Sub() ShowTaskError(ex))
                End Try
            End SyncLock
        End Sub)
    End Sub

    ''' <summary>把异常转换成界面提示；令牌失效时自动退出登录。</summary>
    Private Sub ShowTaskError(ex As Exception)
        Loading = False
        Dim Message As String
        If TypeOf ex Is SkinSiteException Then
            Message = ex.Message
            If CType(ex, SkinSiteException).StatusCode = 401 Then
                Logger.Info("皮肤站令牌已失效，要求重新登录")
                OnTokenExpired()
                Hint("登录已过期，请重新登录。", HintType.Red, False)
                Return
            End If
        Else
            Logger.Error(ex, "皮肤站操作失败")
            Message = "操作没有完成，请稍后再试。"
        End If
        SetModalError(Message)
        Hint(Message, HintType.Red, False)
    End Sub

    Private Sub SetModalError(Message As String)
        If ModalError IsNot Nothing Then
            If ModalErrorText IsNot Nothing Then ModalErrorText.Text = Message
            ModalError.Visibility = If(Message = "", Visibility.Collapsed, Visibility.Visible)
        End If
        If LabSecError IsNot Nothing Then LabSecError.Text = Message
    End Sub

#End Region
#Region "界面搭建"

    ''' <summary>重绘整个页面。</summary>
    Private Sub Rebuild()
        If Not IsLoaded Then Return
        PanMain.Children.Clear()
        If Not SkinLoggedIn Then
            PanMain.Children.Add(BuildNotLoggedIn())
            Return
        End If
        If Account Is Nothing Then
            '数据还没加载完成
            Dim LoadingPanel As New StackPanel With {.HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center}
            LoadingPanel.Children.Add(SkinIcon(ModSkinSiteIcons.IconLoaderCircle, 26, "#3D7564"))
            Dim LoadingText = SkinText("正在加载角色与材质……", 12.5, SkinColorMuted)
            LoadingText.HorizontalAlignment = HorizontalAlignment.Center
            LoadingText.Margin = New Thickness(0, 10, 0, 0)
            LoadingPanel.Children.Add(LoadingText)
            PanMain.Children.Add(LoadingPanel)
            Return
        End If
        '默认选中默认角色，没有默认角色时选中第一个
        If SelectedPid = "" OrElse Account.FindProfile(SelectedPid) Is Nothing Then
            If Account.DefaultProfile IsNot Nothing Then
                SelectedPid = Account.DefaultProfile.Pid
            ElseIf Account.Profiles.Count > 0 Then
                SelectedPid = Account.Profiles(0).Pid
            Else
                SelectedPid = ""
            End If
        End If
        Dim Root As New Grid
        Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
        Root.Children.Add(BuildAccountBar())
        Dim Content As FrameworkElement = BuildMainView()
        Grid.SetRow(Content, 1)
        Root.Children.Add(Content)
        PanMain.Children.Add(Root)
    End Sub

    ''' <summary>顶部账号条。</summary>
    Private Function BuildAccountBar() As FrameworkElement
        Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 16)}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim Left As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Left.Children.Add(SkinText("皮肤站", 17, SkinColorInk, True))
        Dim Mail As New TextBlock With {
            .Text = SkinMail,
            .FontSize = 12,
            .FontFamily = New FontFamily(SkinFontMono),
            .Foreground = SkinBrush(SkinColorMuted),
            .Margin = New Thickness(12, 2, 0, 0),
            .VerticalAlignment = VerticalAlignment.Center
        }
        Left.Children.Add(Mail)
        Row.Children.Add(Left)
        Dim Right As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Dim RefreshButton = SkinButton("刷新", "ghost", Sub() RefreshAccount(True), ModSkinSiteIcons.IconRefreshCw, True)
        Right.Children.Add(RefreshButton.Root)
        Dim Security = SkinButton("修改密码", "ghost", AddressOf ShowSecurityModal, ModSkinSiteIcons.IconKeyRound, True)
        Security.Root.Margin = New Thickness(8, 0, 0, 0)
        Right.Children.Add(Security.Root)
        Dim Logout = SkinButton("退出登录", "ghost", AddressOf ConfirmLogout, ModSkinSiteIcons.IconLogOut, True)
        Logout.Root.Margin = New Thickness(8, 0, 0, 0)
        Right.Children.Add(Logout.Root)
        Grid.SetColumn(Right, 1)
        Row.Children.Add(Right)
        Return Row
    End Function

    ''' <summary>主体：左侧角色列表，右侧材质列表。</summary>
    Private Function BuildMainView() As FrameworkElement
        Dim Grid As New Grid
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(248)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid.Children.Add(BuildProfilePanel())
        Dim TexturePanel As FrameworkElement = BuildTexturePanel()
        Grid.SetColumn(TexturePanel, 1)
        Grid.Children.Add(TexturePanel)
        Return Grid
    End Function

    ''' <summary>左侧角色列表。</summary>
    Private Function BuildProfilePanel() As FrameworkElement
        Dim Card As Border = SkinCard(15, New Thickness(12), "#FFFFFF", SkinColorCardLine, True)
        Card.Margin = New Thickness(0, 0, 14, 0)
        Dim Panel As New Grid
        Panel.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Panel.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
        Panel.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Dim Head As New Grid With {.Margin = New Thickness(4, 2, 4, 10)}
        Head.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Head.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Head.Children.Add(SkinText("我的角色", 13.5, SkinColorInk, True))
        Dim Count As New TextBlock With {
            .Text = $"{Account.Profiles.Count} / {SkinMaxProfile}",
            .FontSize = 11.5,
            .Foreground = SkinBrush("#87918D"),
            .VerticalAlignment = VerticalAlignment.Center
        }
        Grid.SetColumn(Count, 1)
        Head.Children.Add(Count)
        Panel.Children.Add(Head)
        Dim Scroll As New MyScrollViewer With {
            .VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        }
        Dim List As New StackPanel
        For Each Profile In Account.Profiles
            List.Children.Add(BuildProfileRow(Profile))
        Next
        Scroll.Content = List
        Grid.SetRow(Scroll, 1)
        Panel.Children.Add(Scroll)
        Dim AddButton = SkinButton("新建角色", "secondary", AddressOf ShowCreateProfileModal, ModSkinSiteIcons.IconPlus, True, True)
        AddButton.SetEnabled(Account.Profiles.Count < SkinMaxProfile)
        AddButton.Root.Margin = New Thickness(0, 10, 0, 0)
        AddButton.Root.HorizontalAlignment = HorizontalAlignment.Stretch
        Grid.SetRow(AddButton.Root, 2)
        Panel.Children.Add(AddButton.Root)
        Card.Child = Panel
        Return Card
    End Function

    ''' <summary>角色列表中的一行。</summary>
    Private Function BuildProfileRow(Profile As SkinSiteProfile) As FrameworkElement
        Dim IsDefault As Boolean = Account.CurrentPid = Profile.Pid
        Dim IsSelected As Boolean = Profile.Pid = SelectedPid
        Dim Row As New Border With {
            .CornerRadius = New CornerRadius(10),
            .Padding = New Thickness(8, 7, 8, 7),
            .Margin = New Thickness(0, 0, 0, 4),
            .Cursor = Cursors.Hand,
            .Background = SkinBrush(If(IsSelected, "#E2EEE8", "Transparent"))
        }
        Dim Grid As New Grid
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        '角色头像
        Dim Art As Border = SkinArtBackground("#E5EEE8", "#D3E2D9", 12, 8)
        Art.Width = 34
        Art.Height = 34
        Art.ClipToBounds = True
        Dim Active = Profile.ActiveTexture
        If Active IsNot Nothing Then
            Dim Skin As BitmapSource = SkinGetTextureCached(Active.SkinUrl)
            Dim FaceSource As BitmapSource = SkinCrop(Skin, 8, 8, 8, 8)
            If FaceSource IsNot Nothing Then
                Dim Face As New Image With {.Source = FaceSource, .Stretch = Stretch.Uniform, .Margin = New Thickness(2)}
                RenderOptions.SetBitmapScalingMode(Face, BitmapScalingMode.NearestNeighbor)
                Art.Child = Face
            End If
        End If
        Grid.Children.Add(Art)
        '名字与材质数
        Dim Info As New StackPanel With {.Margin = New Thickness(10, 0, 0, 0), .VerticalAlignment = VerticalAlignment.Center}
        Info.Children.Add(SkinText(Profile.Name, 12.5, SkinColorInk, True, True))
        Dim SubText = SkinText($"{Profile.Textures.Count} 套材质", 10.5, "#87918D")
        SubText.Margin = New Thickness(0, 3, 0, 0)
        Info.Children.Add(SubText)
        Grid.SetColumn(Info, 1)
        Grid.Children.Add(Info)
        If IsDefault Then
            Dim Crown = SkinIcon(ModSkinSiteIcons.IconCrown, 14, "#B58A2E")
            Crown.VerticalAlignment = VerticalAlignment.Center
            Grid.SetColumn(Crown, 2)
            Grid.Children.Add(Crown)
        End If
        Row.Child = Grid
        AddHandler Row.MouseEnter, Sub() If Not IsSelected Then Row.Background = SkinBrush("#F1F5F0")
        AddHandler Row.MouseLeave, Sub() Row.Background = SkinBrush(If(IsSelected, "#E2EEE8", "Transparent"))
        AddHandler Row.MouseLeftButtonUp,
            Sub()
                SelectedPid = Profile.Pid
                Rebuild()
            End Sub
        Return Row
    End Function

    ''' <summary>右侧材质区。</summary>
    Private Function BuildTexturePanel() As FrameworkElement
        Dim Profile As SkinSiteProfile = CurrentProfile()
        If Profile Is Nothing Then
            Dim EmptyCard As Border = SkinCard(15, New Thickness(24), "#FFFFFF", SkinColorCardLine, True)
            EmptyCard.VerticalAlignment = VerticalAlignment.Top
            Dim EmptyPanel As New StackPanel
            Dim EmptyTitle = SkinText("这个账号还没有角色", 14, SkinColorInk, True)
            EmptyPanel.Children.Add(EmptyTitle)
            Dim EmptyDesc = SkinText($"点左侧的{vbLQ}新建角色{vbRQ}创建第一个角色，然后就能上传皮肤与披风了。", 12, SkinColorMuted, False, False, True)
            EmptyDesc.Margin = New Thickness(0, 8, 0, 0)
            EmptyPanel.Children.Add(EmptyDesc)
            EmptyCard.Child = EmptyPanel
            Return EmptyCard
        End If
        Dim Panel As New Grid
        Panel.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Panel.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
        '标题与操作
        Dim Head As New Grid With {.Margin = New Thickness(0, 0, 0, 12)}
        Head.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Head.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim TitleRow As New StackPanel With {.Orientation = Orientation.Horizontal}
        TitleRow.Children.Add(SkinText(Profile.Name, 18, SkinColorInk, True, True))
        Dim Tags As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(12, 0, 0, 0), .VerticalAlignment = VerticalAlignment.Center}
        If Account.CurrentPid = Profile.Pid Then
            Tags.Children.Add(SkinTag("默认角色", "primary", ModSkinSiteIcons.IconCrown))
        End If
        Dim Active = Profile.ActiveTexture
        Dim CountTag = SkinTag($"{Profile.Textures.Count} 套材质 · {If(Active?.ModelText, "未设置")}", "default")
        CountTag.Margin = New Thickness(7, 0, 0, 0)
        Tags.Children.Add(CountTag)
        TitleRow.Children.Add(Tags)
        Head.Children.Add(TitleRow)
        Dim Actions As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Dim Upload = SkinButton("上传新皮肤", "primary", Sub() ShowUploadSkinModal(Profile), ModSkinSiteIcons.IconUpload)
        Upload.SetEnabled(Account.TotalTextures < SkinMaxTexture)
        Actions.Children.Add(Upload.Root)
        If Account.CurrentPid <> Profile.Pid Then
            Dim MakeDefault = SkinButton("设为默认角色", "secondary", Sub() SetDefaultProfile(Profile), ModSkinSiteIcons.IconCrown)
            MakeDefault.Root.Margin = New Thickness(8, 0, 0, 0)
            Actions.Children.Add(MakeDefault.Root)
        End If
        Dim DeleteButton = SkinButton("删除角色", "ghost", Sub() ConfirmDeleteProfile(Profile), ModSkinSiteIcons.IconTrash2)
        DeleteButton.Root.Margin = New Thickness(8, 0, 0, 0)
        Actions.Children.Add(DeleteButton.Root)
        Grid.SetColumn(Actions, 1)
        Head.Children.Add(Actions)
        Panel.Children.Add(Head)
        '材质列表：只有这里需要滚动
        Dim Scroll As New MyScrollViewer With {
            .VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        }
        If Profile.Textures.Count = 0 Then
            Scroll.Content = BuildTextureEmpty()
        Else
            Dim Wrap As New WrapPanel
            For Each Texture In Profile.Textures
                Wrap.Children.Add(BuildTextureCard(Profile, Texture))
            Next
            Scroll.Content = Wrap
        End If
        Grid.SetRow(Scroll, 1)
        Panel.Children.Add(Scroll)
        If Account.TotalTextures >= SkinMaxTexture Then
            Dim Limit = SkinInfoHint(ModSkinSiteIcons.IconAlertTriangle, $"已达到每个账号 {SkinMaxTexture} 套材质的上限",
                                     "删除不再使用的材质后可以继续上传。", "warn")
            Limit.Margin = New Thickness(0, 12, 0, 0)
            Grid.SetRow(Limit, 1)
            Limit.VerticalAlignment = VerticalAlignment.Bottom
            Panel.Children.Add(Limit)
        End If
        Return Panel
    End Function

    ''' <summary>还没有登录皮肤站账号时的提示。</summary>
    Private Function BuildNotLoggedIn() As FrameworkElement
        Dim Card As Border = SkinCard(16, New Thickness(28), "#FFFFFF", SkinColorCardLine, True)
        Card.Width = 430
        Card.HorizontalAlignment = HorizontalAlignment.Center
        Card.VerticalAlignment = VerticalAlignment.Center
        Dim Panel As New StackPanel
        Panel.Children.Add(New Border With {
            .Width = 50, .Height = 50,
            .CornerRadius = New CornerRadius(14),
            .Background = SkinBrush("#E6F1EB"),
            .HorizontalAlignment = HorizontalAlignment.Left,
            .Child = SkinIcon(ModSkinSiteIcons.IconCircleUserRound, 23, "#3D7564")
        })
        Dim Title = SkinText("还没有登录皮肤站账号", 15, SkinColorInk, True)
        Title.Margin = New Thickness(0, 14, 0, 0)
        Panel.Children.Add(Title)
        Dim Desc = SkinText($"在启动页用 mcpixelart 皮肤站账号登录，或直接点{vbLQ}登录并进入游戏{vbRQ}，就能在这里管理角色与材质。", 12, SkinColorMuted, False, False, True)
        Desc.Margin = New Thickness(0, 8, 0, 0)
        Desc.LineHeight = 19
        Panel.Children.Add(Desc)
        Dim Go = SkinButton("去启动页登录", "primary", Sub() FrmMain.PageChange(FormMain.PageType.Launch), ModSkinSiteIcons.IconArrowRight)
        Go.Root.Margin = New Thickness(0, 16, 0, 0)
        Panel.Children.Add(Go.Root)
        Card.Child = Panel
        Return Card
    End Function

    ''' <summary>提示条，Kind：warn / info / danger。</summary>
    Private Function SkinInfoHint(IconData As String, Title As String, Text As String, Optional Kind As String = "info") As FrameworkElement
        Dim Background As String = "#FFF7E9", Line As String = "#ECD9B9", Foreground As String = "#6C4A20", IconColor As String = "#D18B36"
        If Kind = "info" Then
            Background = "#EDF1EB" : Line = "#DDE4DA" : Foreground = "#53665F" : IconColor = "#548271"
        ElseIf Kind = "danger" Then
            Background = "#FFF3F2" : Line = "#EDCCCC" : Foreground = "#A84747" : IconColor = SkinColorDanger
        End If
        Dim Row As New Grid With {.VerticalAlignment = VerticalAlignment.Center}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim Icon = SkinIcon(IconData, 19, IconColor)
        Icon.Margin = New Thickness(0, 0, 12, 0)
        Row.Children.Add(Icon)
        Dim TextPanel As New StackPanel
        TextPanel.Children.Add(SkinText(Title, 12, Foreground, True))
        Dim Body = SkinText(Text, 11, Foreground, False, False, True)
        Body.Margin = New Thickness(0, 4, 0, 0)
        Body.Opacity = 0.85
        TextPanel.Children.Add(Body)
        Grid.SetColumn(TextPanel, 1)
        Row.Children.Add(TextPanel)
        Return New Border With {
            .Background = SkinBrush(Background),
            .BorderBrush = SkinBrush(Line),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(12),
            .Padding = New Thickness(16, 14, 16, 14),
            .Child = Row
        }
    End Function

#End Region
#Region "弹窗"

    Private Sub InitModal()
        AddHandler BtnModalClose.Click, Sub() CloseModal()
        AddHandler BorModalBack.MouseLeftButtonDown, Sub() CloseModal()
    End Sub

    ''' <summary>显示弹窗。Actions 为 (文字, 样式, 点击事件, 图标)。</summary>
    Private Sub ShowModal(Title As String, Desc As String, Body As UIElement, ParamArray Actions() As (Text As String, Kind As String, Handler As Action, Icon As String))
        LabModalTitle.Text = Title
        LabModalDesc.Text = Desc
        LabModalDesc.Visibility = If(Desc = "", Visibility.Collapsed, Visibility.Visible)
        PanModalBody.Children.Clear()
        PanModalBody.Children.Add(Body)
        PanModalActions.Children.Clear()
        ModalButtons.Clear()
        For Each Item In Actions
            Dim Button = SkinButton(Item.Text, If(Item.Kind = "", "ghost", Item.Kind), Item.Handler, Item.Icon)
            ModalButtons.Add(Button)
            PanModalActions.Children.Add(Button.Root)
        Next
        PanModal.Visibility = Visibility.Visible
    End Sub

    Private Sub CloseModal()
        PanModal.Visibility = Visibility.Collapsed
        PanModalBody.Children.Clear()
        PanModalActions.Children.Clear()
        ModalButtons.Clear()
        ModalError = Nothing
        ModalErrorText = Nothing
        ModalPrimary = Nothing
        InpNewName = Nothing
        NameCheckToken += 1
    End Sub

    ''' <summary>弹窗内的错误提示条。</summary>
    Private Function BuildModalError() As FrameworkElement
        ModalErrorText = SkinText("", 11.5, SkinColorDangerDark, False, False, True)
        ModalError = New Border With {
            .Background = SkinBrush("#FFF3F2"),
            .BorderBrush = SkinBrush("#EDCCCC"),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(9),
            .Padding = New Thickness(13, 10, 13, 10),
            .Margin = New Thickness(26, 12, 26, 0),
            .Child = ModalErrorText,
            .Visibility = Visibility.Collapsed
        }
        Return ModalError
    End Function

    ''' <summary>弹窗内的提示块。</summary>
    Private Function BuildModalNote(IconData As String, Title As String, Text As String) As FrameworkElement
        Dim Panel As New Grid With {.Margin = New Thickness(26, 16, 26, 0)}
        Panel.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Panel.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim Icon = SkinIcon(IconData, 17, "#548271")
        Icon.Margin = New Thickness(0, 0, 11, 0)
        Icon.VerticalAlignment = VerticalAlignment.Top
        Panel.Children.Add(Icon)
        Dim TextPanel As New StackPanel
        If Title <> "" Then
            TextPanel.Children.Add(SkinText(Title, 11.5, "#31433D", True))
        End If
        Dim Body = SkinText(Text, 11, "#53665F", False, False, True)
        If Title <> "" Then Body.Margin = New Thickness(0, 4, 0, 0)
        Body.LineHeight = 18
        TextPanel.Children.Add(Body)
        Grid.SetColumn(TextPanel, 1)
        Panel.Children.Add(TextPanel)
        Return New Border With {
            .Background = SkinBrush("#EDF1EB"),
            .CornerRadius = New CornerRadius(10),
            .Padding = New Thickness(14, 12, 14, 12),
            .Margin = New Thickness(26, 16, 26, 0),
            .Child = Panel
        }
    End Function

#End Region

#Region "创建角色"

    Private Sub ShowCreateProfileModal()
        If Account IsNot Nothing AndAlso Account.Profiles.Count >= SkinMaxProfile Then
            Hint($"每个账号最多只能创建 {SkinMaxProfile} 个角色。", HintType.Red, False)
            Return
        End If
        NameAvailable = ""
        NameCheckToken += 1
        Dim Body As New StackPanel With {.Margin = New Thickness(26, 22, 26, 10)}
        InpNewName = ModSkinSiteUi.SkinInput("例如 Steve_2026", Nothing, False, True)
        InpNewName.Box.MaxLength = 16
        LabNewNameHint = SkinText("3–16 位字母、数字或下划线，不能以 bot_ 开头。", 11, "#8A938F", False, False, True)
        LabNewNameHint.Margin = New Thickness(0, 7, 0, 0)
        '状态图标放在输入框右侧
        BrdNewNameStatus = New Border With {
            .Width = 22, .Height = 22,
            .VerticalAlignment = VerticalAlignment.Center,
            .Margin = New Thickness(8, 0, 0, 0)
        }
        Grid.SetColumn(BrdNewNameStatus, 2)
        InpNewName.Inner.Children.Add(BrdNewNameStatus)
        AddHandler InpNewName.Box.TextChanged, Sub() OnNameChanged()
        Dim Label As New StackPanel
        Label.Children.Add(SkinText("角色名", 11.5, "#3C4945", True))
        InpNewName.Root.Margin = New Thickness(0, 8, 0, 0)
        Label.Children.Add(InpNewName.Root)
        Label.Children.Add(LabNewNameHint)
        Body.Children.Add(Label)
        Body.Children.Add(BuildModalNote(ModSkinSiteIcons.IconShieldCheck, "角色名创建后不能修改",
                                         "请在提交前确认拼写。若想换名字，需要创建新角色。"))
        Dim ErrorBox = BuildModalError()
        Body.Children.Add(ErrorBox)
        ShowModal("创建新角色", $"角色名会显示在游戏中。当前 {If(Account?.Profiles.Count, 0)} / {SkinMaxProfile} 个。",
                  Body, ("取消", "ghost", Sub() CloseModal(), ""), ("创建角色", "primary", Sub() DoCreateProfile(), ModSkinSiteIcons.IconArrowRight))
        '记录主按钮，便于按查重结果切换可用状态
        If ModalButtons.Count = 2 Then
            ModalPrimary = ModalButtons.Last()
            ModalPrimary.SetEnabled(False)
        End If
        InpNewName.Box.Focus()
    End Sub

    Private Sub OnNameChanged()
        NameAvailable = ""
        If ModalPrimary IsNot Nothing Then ModalPrimary.SetEnabled(False)
        If InpNewName Is Nothing Then Return
        Dim Value As String = InpNewName.Box.Text.Trim
        Dim RuleError As String = SkinValidateGameName(Value)
        If RuleError <> "" OrElse Value = "" Then
            SetNameStatus("idle", If(Value = "", "3–16 位字母、数字或下划线，不能以 bot_ 开头。", RuleError), Value <> "")
            Return
        End If
        SetNameStatus("checking", "正在确认角色名，请稍等。", False)
        '与网页版一致：输入停止 800ms 后再查重
        NameCheckToken += 1
        If NameCheck Is Nothing Then
            NameCheck = New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(800)}
            AddHandler NameCheck.Tick,
                Sub()
                    NameCheck.Stop()
                    '在触发时再取当前的序号，否则会一直用手上第一份过期结果
                    RunNameCheck(NameCheckToken)
                End Sub
        End If
        NameCheck.Stop()
        NameCheck.Start()
    End Sub

    Private Sub RunNameCheck(Token As Integer)
        If InpNewName Is Nothing Then Return
        Dim Value As String = InpNewName.Box.Text.Trim
        If Value = "" Then Return
        RunTask(
        Sub()
            Dim Valid As Boolean = SkinCheckName(Value)
            RunInUi(
            Sub()
                If Token <> NameCheckToken OrElse InpNewName Is Nothing Then Return
                If Valid Then
                    NameAvailable = Value
                    SetNameStatus("available", "这个名字可以使用。", False)
                    If ModalPrimary IsNot Nothing Then ModalPrimary.SetEnabled(True)
                Else
                    SetNameStatus("taken", "这个名字已经有人使用了。", False)
                End If
            End Sub)
        End Sub)
    End Sub

    Private Sub SetNameStatus(State As String, Message As String, Optional Invalid As Boolean = False)
        If LabNewNameHint Is Nothing Then Return
        LabNewNameHint.Text = Message
        LabNewNameHint.Foreground = SkinBrush(If(Invalid OrElse State = "taken", "#B84D4D", If(State = "available", "#3F8B66", "#8A938F")))
        If BrdNewNameStatus Is Nothing Then Return
        BrdNewNameStatus.Child = Nothing
        Select Case State
            Case "checking"
                BrdNewNameStatus.Child = SkinIcon(ModSkinSiteIcons.IconLoaderCircle, 17, "#87918D")
            Case "available"
                BrdNewNameStatus.Child = SkinIcon(ModSkinSiteIcons.IconCheck, 17, "#4A966F")
            Case "taken"
                BrdNewNameStatus.Child = SkinIcon(ModSkinSiteIcons.IconX, 17, SkinColorDanger)
        End Select
        If InpNewName IsNot Nothing Then
            InpNewName.Root.BorderBrush = SkinBrush(If(Invalid OrElse State = "taken", "#D96969", "#D6DCD4"))
        End If
    End Sub

    Private Sub DoCreateProfile()
        If InpNewName Is Nothing Then Return
        Dim GameName As String = InpNewName.Box.Text.Trim
        If GameName <> NameAvailable Then
            SetModalError("请先使用一个可用的角色名。")
            Return
        End If
        Dim FirstProfile As Boolean = Account Is Nothing OrElse Account.Profiles.Count = 0
        RunTask(
        Sub()
            Dim Pid As String = SkinCreateProfile(GameName)
            If FirstProfile Then SkinSetDefaultProfile(Pid)
        End Sub,
        Sub()
            CloseModal()
            Hint("角色创建成功", HintType.Green, False)
            RefreshAccount()
        End Sub)
    End Sub

#End Region

#Region "材质展示"

    Private Function BuildTextureEmpty() As FrameworkElement
        Dim Card As New Border With {
            .MinHeight = 240,
            .CornerRadius = New CornerRadius(18),
            .BorderBrush = SkinBrush("#C7D1CA"),
            .BorderThickness = New Thickness(1.5),
            .Background = SkinBrush("#80FFFFFF")
        }
        Dim Panel As New StackPanel With {.HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center}
        Panel.Children.Add(New Border With {
            .Width = 58, .Height = 58,
            .CornerRadius = New CornerRadius(16),
            .Background = SkinBrush("#E2EEE8"),
            .HorizontalAlignment = HorizontalAlignment.Center,
            .Child = SkinIcon(ModSkinSiteIcons.IconImagePlus, 26, "#4C806F")
        })
        Dim Title = SkinText("角色还没有皮肤", 16, SkinColorInk, True)
        Title.HorizontalAlignment = HorizontalAlignment.Center
        Title.Margin = New Thickness(0, 14, 0, 0)
        Panel.Children.Add(Title)
        Dim Desc = SkinText("上传一张 Minecraft PNG 皮肤。上传成功后会自动设为当前材质。", 11.5, "#7C8883")
        Desc.HorizontalAlignment = HorizontalAlignment.Center
        Desc.Margin = New Thickness(0, 6, 0, 0)
        Panel.Children.Add(Desc)
        Dim Profile = Account.FindProfile(SelectedPid)
        Dim Upload = SkinButton("上传第一张皮肤", "primary", Sub() ShowUploadSkinModal(Profile), ModSkinSiteIcons.IconUpload)
        Upload.Root.HorizontalAlignment = HorizontalAlignment.Center
        Upload.Root.Margin = New Thickness(0, 16, 0, 0)
        Panel.Children.Add(Upload.Root)
        Card.Child = Panel
        Return Card
    End Function

    ''' <summary>一张材质卡片，对应网页版 .texture-card。</summary>
    Private Function BuildTextureCard(Profile As SkinSiteProfile, Texture As SkinSiteTexture) As FrameworkElement
        Dim IsActive As Boolean = Profile.CurrentTid = Texture.Tid
        Dim Card As Border = SkinCard(17, New Thickness(0), "#FFFFFF", If(IsActive, "#80AA9A", "#DCE2DB"), True)
        Card.Width = 282
        Card.Margin = New Thickness(0, 0, 14, 14)
        Dim Stack As New StackPanel
        '预览区
        Dim Preview As Border = SkinArtBackground("#E8EEE9", "#DBE5DE", 22, 0)
        Preview.Height = 208
        Preview.ClipToBounds = True
        Preview.CornerRadius = New CornerRadius(17, 17, 0, 0)
        Dim Figure = SkinFigure(SkinGetTextureCached(Texture.SkinUrl), 158)
        Figure.VerticalAlignment = VerticalAlignment.Bottom
        Figure.HorizontalAlignment = HorizontalAlignment.Center
        Figure.Margin = New Thickness(0, 0, 0, 14)
        Preview.Child = Figure
        '预览区上的元素
        Dim Overlay As New Grid
        Overlay.Children.Add(Preview)
        If IsActive Then
            Dim Badge As New Border With {
                .Background = SkinBrush("#397561"),
                .CornerRadius = New CornerRadius(7),
                .Padding = New Thickness(7, 4, 7, 4),
                .Margin = New Thickness(11, 11, 0, 0),
                .HorizontalAlignment = HorizontalAlignment.Left,
                .VerticalAlignment = VerticalAlignment.Top,
                .Child = SkinIconText(ModSkinSiteIcons.IconBadgeCheck, "当前使用", 13, "#FFFFFF", "#FFFFFF", 11, True, 4)
            }
            Overlay.Children.Add(Badge)
        End If
        If Texture.CapeHash <> "" Then
            Dim CapeThumb As New Border With {
                .Width = 78,
                .CornerRadius = New CornerRadius(10),
                .Background = SkinBrush("#E0FFFFFF"),
                .BorderBrush = SkinBrush("#2E4C695E"),
                .BorderThickness = New Thickness(1),
                .Padding = New Thickness(6),
                .Margin = New Thickness(0, 0, 12, 12),
                .HorizontalAlignment = HorizontalAlignment.Right,
                .VerticalAlignment = VerticalAlignment.Bottom,
                .Cursor = Cursors.Hand
            }
            Dim CapeStack As New StackPanel
            CapeStack.Children.Add(SkinText("披风", 10, "#7C8883"))
            Dim CapeImage = SkinCape(SkinGetTextureCached(Texture.CapeUrl), 64)
            CapeImage.Margin = New Thickness(0, 3, 0, 0)
            CapeStack.Children.Add(CapeImage)
            CapeThumb.Child = CapeStack
            CapeThumb.RenderTransformOrigin = New Point(0.5, 0.5)
            CapeThumb.RenderTransform = New RotateTransform(3)
            AddHandler CapeThumb.MouseEnter, Sub() CapeThumb.RenderTransform = New RotateTransform(0)
            AddHandler CapeThumb.MouseLeave, Sub() CapeThumb.RenderTransform = New RotateTransform(3)
            AddHandler CapeThumb.MouseLeftButtonUp, Sub()
                                                         ShowCapePreview(Profile, Texture)
                                                     End Sub
            Overlay.Children.Add(CapeThumb)
        End If
        Dim ModelLabel As New Border With {
            .Background = SkinBrush("#C0FFFFFF"),
            .CornerRadius = New CornerRadius(5),
            .Padding = New Thickness(6, 4, 6, 4),
            .Margin = New Thickness(0, 0, 10, 9),
            .HorizontalAlignment = HorizontalAlignment.Right,
            .VerticalAlignment = VerticalAlignment.Bottom,
            .Child = SkinText(Texture.ModelTag, 10, "#51645D", True, True)
        }
        Overlay.Children.Add(ModelLabel)
        Stack.Children.Add(Overlay)
        '说明
        Dim Body As New StackPanel With {.Margin = New Thickness(14, 13, 14, 12)}
        Body.Children.Add(SkinText($"材质 #{Texture.Tid}", 12.5, SkinColorInk, True))
        Dim State = SkinText(If(Texture.CapeHash = "", "皮肤已配置 · 暂无披风", "已配置皮肤与披风"), 11, "#89938F")
        State.Margin = New Thickness(0, 4, 0, 0)
        Body.Children.Add(State)
        Stack.Children.Add(Body)
        '操作
        Dim Actions As New Grid With {
            .Margin = New Thickness(10, 0, 10, 10)
        }
        Actions.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Actions.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Actions.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        If IsActive Then
            Dim Selected As New Border With {
                .Background = SkinBrush("#E5F2EC"),
                .CornerRadius = New CornerRadius(7),
                .Padding = New Thickness(0, 8, 0, 8),
                .Child = SkinIconText(ModSkinSiteIcons.IconCheck, "已选中", 13, "#33745D", "#33745D", 11.5, True, 5)
            }
            Grid.SetColumnSpan(Selected, 1)
            Actions.Children.Add(Selected)
        Else
            Dim UseButton = SkinButton("使用此材质", "soft", Sub() SetDefaultTexture(Profile, Texture), ModSkinSiteIcons.IconPalette, True, True)
            Actions.Children.Add(UseButton.Root)
        End If
        Dim CapeButton = SkinButton(If(Texture.CapeHash = "", "添加披风", "更换披风"), "soft",
                                    Sub() ShowUploadCapeModal(Profile, Texture), ModSkinSiteIcons.IconFileImage, True)
        CapeButton.Root.Margin = New Thickness(6, 0, 0, 0)
        CapeButton.Root.Padding = New Thickness(9, 8, 9, 8)
        Grid.SetColumn(CapeButton.Root, 1)
        Actions.Children.Add(CapeButton.Root)
        Dim DeleteButton = SkinButton("", "soft", Sub() ConfirmDeleteTexture(Profile, Texture), ModSkinSiteIcons.IconTrash2, True)
        DeleteButton.Root.Margin = New Thickness(6, 0, 0, 0)
        DeleteButton.Root.Padding = New Thickness(8, 8, 8, 8)
        Grid.SetColumn(DeleteButton.Root, 2)
        Actions.Children.Add(DeleteButton.Root)
        Stack.Children.Add(Actions)
        Card.Child = Stack
        Return Card
    End Function

#End Region

#Region "材质上传"

    Private Function CurrentProfile() As SkinSiteProfile
        If Account Is Nothing Then Return Nothing
        Return Account.FindProfile(SelectedPid)
    End Function

    ''' <summary>拖放 / 点击选择文件的区域，对应网页版 FileDrop。</summary>
    Private Function BuildDropZone(HintText As String, IsSkin As Boolean) As FrameworkElement
        Dim FilePath As String = If(IsSkin, PendingSkinFile, PendingCapeFile)
        Dim Zone As New Border With {
            .MinHeight = 208,
            .CornerRadius = New CornerRadius(15),
            .BorderBrush = SkinBrush("#BBC8BF"),
            .BorderThickness = New Thickness(1.5),
            .Background = SkinBrush("#F2F5F0"),
            .Cursor = Cursors.Hand
        }
        If FilePath = "" Then
            Dim Panel As New StackPanel With {.HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center}
            Panel.Children.Add(New Border With {
                .Width = 50, .Height = 50,
                .CornerRadius = New CornerRadius(14),
                .Background = SkinBrush("#DFECE6"),
                .HorizontalAlignment = HorizontalAlignment.Center,
                .Child = SkinIcon(ModSkinSiteIcons.IconUpload, 23, "#3C7565")
            })
            Dim Title = SkinText("拖放 PNG 到这里，或点击选择", 12.5, SkinColorInk, True)
            Title.HorizontalAlignment = HorizontalAlignment.Center
            Title.Margin = New Thickness(0, 13, 0, 0)
            Panel.Children.Add(Title)
            Dim SubText = SkinText(HintText, 11, "#7B8983")
            SubText.HorizontalAlignment = HorizontalAlignment.Center
            SubText.Margin = New Thickness(0, 6, 0, 0)
            Panel.Children.Add(SubText)
            Zone.Child = Panel
            AddHandler Zone.MouseLeftButtonUp, Sub() PickTextureFile(IsSkin)
        Else
            '已选择文件：显示预览与文件信息
            Dim Preview As New Grid
            Dim Picture As New Image With {.Stretch = Stretch.Uniform, .Margin = New Thickness(0, 0, 0, 42)}
            Try
                Picture.Source = SkinBytesToImage(IO.File.ReadAllBytes(FilePath))
            Catch ex As Exception
                Logger.Warn(ex, "读取待上传图片失败")
            End Try
            Preview.Children.Add(Picture)
            Dim Info As New IO.FileInfo(FilePath)
            Dim Meta As New Border With {
                .Background = SkinBrush("#EBFFFFFF"),
                .CornerRadius = New CornerRadius(10),
                .Padding = New Thickness(12, 10, 12, 10),
                .Margin = New Thickness(10, 0, 10, 10),
                .VerticalAlignment = VerticalAlignment.Bottom
            }
            Dim MetaPanel As New StackPanel With {.Orientation = Orientation.Horizontal}
            Dim MetaIcon = SkinIcon(ModSkinSiteIcons.IconImagePlus, 19, "#497E6E")
            MetaIcon.Margin = New Thickness(0, 0, 10, 0)
            MetaPanel.Children.Add(MetaIcon)
            Dim MetaText As New StackPanel
            MetaText.Children.Add(SkinText(Info.Name, 11.5, SkinColorInk, True))
            MetaText.Children.Add(SkinText($"{Math.Round(Info.Length / 1024)} KB · PNG", 11, "#818C88"))
            MetaPanel.Children.Add(MetaText)
            Meta.Child = MetaPanel
            Preview.Children.Add(Meta)
            Dim Remove As New Border With {
                .Width = 32, .Height = 32,
                .CornerRadius = New CornerRadius(9),
                .Background = SkinBrush("#EBFFFFFF"),
                .HorizontalAlignment = HorizontalAlignment.Right,
                .VerticalAlignment = VerticalAlignment.Top,
                .Margin = New Thickness(0, 9, 9, 0),
                .Cursor = Cursors.Hand,
                .Child = SkinIcon(ModSkinSiteIcons.IconX, 15, "#6B7A74")
            }
            AddHandler Remove.MouseLeftButtonUp,
                Sub()
                    If IsSkin Then PendingSkinFile = "" Else PendingCapeFile = ""
                    If IsSkin Then ShowUploadSkinModal(CurrentProfile()) Else ShowUploadCapeModal(CurrentProfile(), PendingCapeTarget)
                End Sub
            Preview.Children.Add(Remove)
            Zone.Child = Preview
            Zone.BorderThickness = New Thickness(1.5)
        End If
        '拖放支持
        Zone.AllowDrop = True
        AddHandler Zone.DragOver,
            Sub(Sender As Object, E As DragEventArgs)
                If Not E.Data.GetDataPresent(DataFormats.FileDrop) Then Return
                E.Effects = DragDropEffects.Copy
                E.Handled = True
                Zone.BorderBrush = SkinBrush("#4F8F79")
                Zone.Background = SkinBrush("#E7F3ED")
            End Sub
        AddHandler Zone.DragLeave,
            Sub()
                Zone.BorderBrush = SkinBrush("#BBC8BF")
                Zone.Background = SkinBrush("#F2F5F0")
            End Sub
        AddHandler Zone.Drop,
            Sub(Sender As Object, E As DragEventArgs)
                E.Handled = True
                If Not E.Data.GetDataPresent(DataFormats.FileDrop) Then Return
                Dim Files As String() = E.Data.GetData(DataFormats.FileDrop)
                If Files Is Nothing OrElse Files.Length = 0 Then Return
                PickedTextureFile(Files(0), IsSkin)
            End Sub
        Return Zone
    End Function

    ''' <summary>打开文件选择框。</summary>
    Private Sub PickTextureFile(IsSkin As Boolean)
        Dim FilePath As String = Dialogs.SelectFile(If(IsSkin, "选择皮肤图片", "选择披风图片"), False, filter:={({"png"}, "PNG 图片")}).FirstOrDefault()
        If String.IsNullOrEmpty(FilePath) Then Return
        PickedTextureFile(FilePath, IsSkin)
    End Sub

    ''' <summary>选中文件后的校验与预览刷新。</summary>
    Private Sub PickedTextureFile(FilePath As String, IsSkin As Boolean)
        Try
            SkinValidatePng(FilePath)
        Catch ex As Exception
            Hint(ex.Message, HintType.Red, False)
            Return
        End Try
        If IsSkin Then
            PendingSkinFile = FilePath
            ShowUploadSkinModal(CurrentProfile())
        Else
            PendingCapeFile = FilePath
            ShowUploadCapeModal(CurrentProfile(), PendingCapeTarget)
        End If
    End Sub

    ''' <summary>上传皮肤弹窗。</summary>
    Private Sub ShowUploadSkinModal(Profile As SkinSiteProfile)
        If Profile Is Nothing Then Return
        Dim Body As New Grid With {.Margin = New Thickness(26, 22, 26, 10)}
        Body.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Body.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim Left As New StackPanel
        Left.Children.Add(BuildDropZone($"小于 {SkinMaxImageMb:0}MB，尺寸不超过 {SkinMaxImageSize} × {SkinMaxImageSize}", True))
        Left.Children.Add(BuildInlineNote(ModSkinSiteIcons.IconFileImage, "建议使用标准 Minecraft 64 × 64 皮肤", "必须是 PNG 格式；透明区域会按游戏规则显示。"))
        Body.Children.Add(Left)
        Dim Right As New StackPanel With {.Width = 214, .Margin = New Thickness(22, 0, 0, 0)}
        Right.Children.Add(SkinText("选择手臂模型", 11.5, "#3C4945", True))
        Right.Children.Add(BuildModelCard("default", "Steve", "标准手臂 · 4 像素宽"))
        Right.Children.Add(BuildModelCard("slim", "Alex", "纤细手臂 · 3 像素宽"))
        Dim ModelNote = SkinText("选错模型会让手臂出现黑线或错位。不确定时通常选择 Steve。", 11, "#7B8681", False, False, True)
        ModelNote.Margin = New Thickness(2, 8, 0, 0)
        ModelNote.LineHeight = 17
        Right.Children.Add(ModelNote)
        Grid.SetColumn(Right, 1)
        Body.Children.Add(Right)
        Dim Box As New StackPanel
        Box.Children.Add(Body)
        Box.Children.Add(BuildModalError())
        ShowModal($"为 {Profile.Name} 上传皮肤", $"每次上传会创建一套新材质。账号已使用 {Account.TotalTextures} / {SkinMaxTexture} 套。",
                  Box, ("取消", "ghost", Sub() CloseModal(), ""),
                  ("上传并创建材质", "primary", Sub() DoUploadSkin(Profile), ModSkinSiteIcons.IconUpload))
    End Sub

    ''' <summary>Steve / Alex 模型选择卡片。</summary>
    Private Function BuildModelCard(Value As String, Title As String, Desc As String) As FrameworkElement
        Dim Selected As Boolean = PendingSkinModel = Value
        Dim Card As New Border With {
            .CornerRadius = New CornerRadius(12),
            .BorderBrush = SkinBrush(If(Selected, "#6B9E8C", "#DBE1DA")),
            .BorderThickness = New Thickness(1),
            .Background = SkinBrush("#FFFFFF"),
            .Padding = New Thickness(10),
            .Margin = New Thickness(0, 8, 0, 0),
            .Cursor = Cursors.Hand
        }
        Dim Grid As New Grid
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(42)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        '小人物图示
        Dim Figure As New Canvas With {.Width = 34, .Height = 46, .VerticalAlignment = VerticalAlignment.Center}
        Dim Head As New Border With {.Width = 14, .Height = 14, .CornerRadius = New CornerRadius(3), .Background = SkinBrush("#75BFA7")}
        Canvas.SetLeft(Head, 10) : Canvas.SetTop(Head, 1) : Figure.Children.Add(Head)
        Dim Torso As New Border With {.Width = 14, .Height = 16, .CornerRadius = New CornerRadius(2), .Background = SkinBrush("#3C7462")}
        Canvas.SetLeft(Torso, 10) : Canvas.SetTop(Torso, 17) : Figure.Children.Add(Torso)
        Dim ArmWidth As Double = If(Value = "slim", 5, 6)
        Dim ArmLeft As New Border With {.Width = ArmWidth, .Height = 19, .CornerRadius = New CornerRadius(2), .Background = SkinBrush("#6DAF9A")}
        Canvas.SetLeft(ArmLeft, If(Value = "slim", 3, 2)) : Canvas.SetTop(ArmLeft, 18) : Figure.Children.Add(ArmLeft)
        Dim ArmRight As New Border With {.Width = ArmWidth, .Height = 19, .CornerRadius = New CornerRadius(2), .Background = SkinBrush("#6DAF9A")}
        Canvas.SetLeft(ArmRight, 34 - If(Value = "slim", 8, 8)) : Canvas.SetTop(ArmRight, 18) : Figure.Children.Add(ArmRight)
        Grid.Children.Add(Figure)
        Dim TextPanel As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        TextPanel.Children.Add(SkinText(Title, 12, SkinColorInk, True))
        Dim DescText = SkinText(Desc, 11, "#8A948F")
        DescText.Margin = New Thickness(0, 3, 0, 0)
        TextPanel.Children.Add(DescText)
        Grid.SetColumn(TextPanel, 1)
        Grid.Children.Add(TextPanel)
        If Selected Then
            Dim Check = SkinIcon(ModSkinSiteIcons.IconCheck, 16, "#4E9474")
            Grid.SetColumn(Check, 2)
            Grid.Children.Add(Check)
        End If
        Card.Child = Grid
        AddHandler Card.MouseLeftButtonUp, Sub()
                                               PendingSkinModel = Value
                                               ShowUploadSkinModal(CurrentProfile())
                                           End Sub
        Return Card
    End Function

    ''' <summary>弹窗内的说明块（不带外边距）。</summary>
    Private Function BuildInlineNote(IconData As String, Title As String, Text As String) As FrameworkElement
        Dim Panel As New Grid With {.Margin = New Thickness(0, 12, 0, 0)}
        Panel.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Panel.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Dim Icon = SkinIcon(IconData, 17, "#548271")
        Icon.Margin = New Thickness(0, 0, 10, 0)
        Icon.VerticalAlignment = VerticalAlignment.Top
        Panel.Children.Add(Icon)
        Dim TextPanel As New StackPanel
        TextPanel.Children.Add(SkinText(Title, 11.5, "#43534D", True))
        Dim Body = SkinText(Text, 11, "#65736E", False, False, True)
        Body.Margin = New Thickness(0, 3, 0, 0)
        Body.LineHeight = 17
        TextPanel.Children.Add(Body)
        Grid.SetColumn(TextPanel, 1)
        Panel.Children.Add(TextPanel)
        Return Panel
    End Function

    Private Sub DoUploadSkin(Profile As SkinSiteProfile)
        If PendingSkinFile = "" Then
            SetModalError("请先选择一张皮肤 PNG。")
            Return
        End If
        Dim FilePath As String = PendingSkinFile
        Dim Model As String = PendingSkinModel
        Dim FirstTexture As Boolean = Profile.CurrentTid Is Nothing
        RunTask(
        Sub()
            SkinValidatePng(FilePath)
            Dim Tid As Integer = SkinUploadSkin(Profile.Pid, FilePath, Model)
            If FirstTexture Then SkinSetDefaultTexture(Profile.Pid, Tid)
        End Sub,
        Sub()
            CloseModal()
            Hint("皮肤上传成功", HintType.Green, False)
            RefreshAccount()
        End Sub)
    End Sub

    ''' <summary>上传披风弹窗。</summary>
    Private Sub ShowUploadCapeModal(Profile As SkinSiteProfile, Texture As SkinSiteTexture)
        If Profile Is Nothing OrElse Texture Is Nothing Then Return
        PendingCapeTarget = Texture
        Dim Box As New StackPanel With {.Margin = New Thickness(26, 22, 26, 10)}
        Box.Children.Add(BuildDropZone($"仅 PNG，小于 {SkinMaxImageMb:0}MB", False))
        Box.Children.Add(BuildInlineNote(ModSkinSiteIcons.IconFileImage, "建议使用标准披风比例",
                                         "披风会绑定到该材质，上传后可能需要重新进入游戏才会刷新。"))
        Box.Children.Add(BuildModalError())
        ShowModal(If(Texture.CapeHash = "", "添加披风", "更换披风"), $"披风会绑定到 {Profile.Name} 的材质 #{Texture.Tid}。",
                  Box, ("取消", "ghost", Sub() CloseModal(), ""),
                  ("上传披风", "primary", Sub() DoUploadCape(Profile), ModSkinSiteIcons.IconUpload))
    End Sub

    Private Sub DoUploadCape(Profile As SkinSiteProfile)
        If PendingCapeFile = "" OrElse PendingCapeTarget Is Nothing Then
            SetModalError("请先选择披风 PNG。")
            Return
        End If
        Dim FilePath As String = PendingCapeFile
        Dim Target = PendingCapeTarget
        RunTask(
        Sub()
            SkinValidatePng(FilePath)
            SkinUploadCape(Profile.Pid, Target.Tid, FilePath)
        End Sub,
        Sub()
            CloseModal()
            Hint(If(Target.CapeHash = "", "披风已添加", "披风已更换"), HintType.Green, False)
            RefreshAccount()
        End Sub)
    End Sub

    ''' <summary>披风预览弹窗。</summary>
    Private Sub ShowCapePreview(Profile As SkinSiteProfile, Texture As SkinSiteTexture)
        Dim Stage As New Border With {
            .CornerRadius = New CornerRadius(16),
            .Background = New LinearGradientBrush(
                CType(ColorConverter.ConvertFromString("#EEF3EE"), Color),
                CType(ColorConverter.ConvertFromString("#DFE8E1"), Color), New Point(0, 0), New Point(1, 1)),
            .Padding = New Thickness(26),
            .Margin = New Thickness(26, 22, 26, 10)
        }
        Dim Cape = SkinCape(SkinGetTextureCached(Texture.CapeUrl), 260)
        Cape.HorizontalAlignment = HorizontalAlignment.Center
        Stage.Child = Cape
        ShowModal($"材质 #{Texture.Tid} 的披风", "", Stage,
                  ("关闭", "ghost", Sub() CloseModal(), ""),
                  ("更换披风", "primary", Sub()
                                              CloseModal()
                                              ShowUploadCapeModal(Profile, Texture)
                                          End Sub, ModSkinSiteIcons.IconFileImage))
    End Sub

#End Region

#Region "角色与材质操作"

    Private Sub SetDefaultProfile(Profile As SkinSiteProfile)
        RunTask(
        Sub() SkinSetDefaultProfile(Profile.Pid),
        Sub()
            If Account IsNot Nothing Then Account.CurrentPid = Profile.Pid
            Hint("默认角色已更新", HintType.Green, False)
            Rebuild()
        End Sub)
    End Sub

    Private Sub ConfirmDeleteProfile(Profile As SkinSiteProfile)
        ShowConfirm("删除这个角色？", $"角色 {Profile.Name} 及其关联的材质将无法恢复，已有材质不会自动转移到其他角色。",
                    "确认删除", True, Sub() DoDeleteProfile(Profile))
    End Sub

    Private Sub DoDeleteProfile(Profile As SkinSiteProfile)
        RunTask(
        Sub() SkinDeleteProfile(Profile.Pid),
        Sub()
            Hint("角色已删除", HintType.Green, False)
            SelectedPid = ""
            RefreshAccount()
        End Sub)
    End Sub

    Private Sub SetDefaultTexture(Profile As SkinSiteProfile, Texture As SkinSiteTexture)
        RunTask(
        Sub() SkinSetDefaultTexture(Profile.Pid, Texture.Tid),
        Sub()
            Profile.CurrentTid = Texture.Tid
            Hint("材质已换上，重新进入游戏后即可看到新皮肤。", HintType.Green, False)
            Rebuild()
        End Sub)
    End Sub

    Private Sub ConfirmDeleteTexture(Profile As SkinSiteProfile, Texture As SkinSiteTexture)
        ShowConfirm("删除这个材质？", $"材质 #{Texture.Tid} 将永久删除。如果它正在使用，请先切换到其他材质。",
                    "确认删除", True, Sub() DoDeleteTexture(Profile, Texture))
    End Sub

    Private Sub DoDeleteTexture(Profile As SkinSiteProfile, Texture As SkinSiteTexture)
        RunTask(
        Sub() SkinDeleteTexture(Texture.Tid),
        Sub()
            Hint("材质已删除", HintType.Green, False)
            Profile.Textures.RemoveAll(Function(t) t.Tid = Texture.Tid)
            If Profile.CurrentTid = Texture.Tid Then Profile.CurrentTid = Nothing
            RefreshAccount()
        End Sub)
    End Sub

    ''' <summary>通用确认弹窗。</summary>
    Private Sub ShowConfirm(Title As String, Desc As String, ConfirmText As String, Danger As Boolean, OnConfirm As Action)
        Dim Panel As New StackPanel With {.HorizontalAlignment = HorizontalAlignment.Center, .Margin = New Thickness(26, 24, 26, 6)}
        Panel.Children.Add(New Border With {
            .Width = 55, .Height = 55,
            .CornerRadius = New CornerRadius(16),
            .Background = SkinBrush(If(Danger, "#FEEAEA", "#E6F1EB")),
            .HorizontalAlignment = HorizontalAlignment.Center,
            .Child = SkinIcon(If(Danger, ModSkinSiteIcons.IconAlertTriangle, ModSkinSiteIcons.IconLogOut), 24,
                              If(Danger, SkinColorDanger, "#2F6A58"))
        })
        Dim Text = SkinText(Desc, 12, "#5F6D68", False, False, True)
        Text.TextAlignment = TextAlignment.Center
        Text.HorizontalAlignment = HorizontalAlignment.Center
        Text.Margin = New Thickness(0, 18, 0, 0)
        Text.LineHeight = 21
        Text.MaxWidth = 340
        Panel.Children.Add(Text)
        ShowModal(Title, "", Panel,
                  ("取消", "ghost", Sub() CloseModal(), ""),
                  (ConfirmText, If(Danger, "danger", "dark"), Sub()
                                                                  CloseModal()
                                                                  OnConfirm.Invoke()
                                                              End Sub, If(Danger, ModSkinSiteIcons.IconTrash2, ModSkinSiteIcons.IconLogOut)))
    End Sub

    Private Sub ConfirmLogout()
        ShowConfirm("确认退出登录？", "退出后需要重新输入邮箱和密码才能管理角色，已经保存的皮肤和披风不受影响。",
                    "确认退出", False, AddressOf DoLogout)
    End Sub

    Private Sub DoLogout()
        SkinLogout()
        Account = Nothing
        SelectedPid = ""
        Rebuild()
        Hint("已安全退出", HintType.Blue, False)
    End Sub

#End Region
#Region "账号安全"

    ''' <summary>修改密码（需要邮箱验证）。</summary>
    Private Sub ShowSecurityModal()
        If SecCode = "" Then SecCode = SkinNewCode()
        Dim Body As New StackPanel With {.Margin = New Thickness(26, 22, 26, 10)}
        Body.Children.Add(BuildSecurityStep())
        Body.Children.Add(BuildModalError())
        Dim Description As String = If(SecStep = 1, "需要验证注册邮箱后才能设置新密码。",
                                       $"把验证码发送到 {SkinMailAddress} 后点{vbLQ}验证是否发送成功{vbRQ}。")
        ShowModal("修改登录密码", Description, Body, ("关闭", "ghost", Sub() CloseModal(), ""))
    End Sub

    ''' <summary>带标题与说明的输入区。</summary>
    Private Function FieldBlock(Label As String, Control As FrameworkElement, Optional Hint As String = "") As FrameworkElement
        Dim Panel As New StackPanel With {.Margin = New Thickness(0, 0, 0, 16)}
        Panel.Children.Add(SkinText(Label, 11.5, "#3C4945", True))
        Control.Margin = New Thickness(0, 8, 0, 0)
        Panel.Children.Add(Control)
        If Hint <> "" Then
            Dim HintText = SkinText(Hint, 11, "#8A938F", False, False, True)
            HintText.Margin = New Thickness(0, 7, 0, 0)
            Panel.Children.Add(HintText)
        End If
        Return Panel
    End Function

    ''' <summary>邮箱验证说明卡片。</summary>
    Private Function BuildVerifyNotice() As FrameworkElement
        Dim Card As New Border With {
            .Background = SkinBrush("#FFFBF0"),
            .BorderBrush = SkinBrush("#E7DDC2"),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(12),
            .Padding = New Thickness(14, 13, 14, 13),
            .Margin = New Thickness(0, 16, 0, 0)
        }
        Dim Grid As New Grid
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim Left As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        Dim TitleRow As New StackPanel With {.Orientation = Orientation.Horizontal}
        TitleRow.Children.Add(SkinIcon(ModSkinSiteIcons.IconTimer, 14, "#8B6A26"))
        Dim TitleText = SkinText($"验证码从发出起 {SkinCodeValidMinutes} 分钟内有效", 11.5, "#8B6A26", True)
        TitleText.Margin = New Thickness(6, 0, 0, 0)
        TitleRow.Children.Add(TitleText)
        Left.Children.Add(TitleRow)
        Dim Body = SkinText($"发送出错或者一直收不到邮件时，加入 QQ 群 {SkinQQGroup} 联系服主。", 11, "#7D7563", False, False, True)
        Body.Margin = New Thickness(0, 7, 12, 0)
        Body.LineHeight = 17
        Left.Children.Add(Body)
        Grid.Children.Add(Left)
        Dim Qr As New Border With {
            .Width = 92, .Height = 92,
            .CornerRadius = New CornerRadius(10),
            .Background = SkinBrush("#FFFFFF"),
            .BorderBrush = SkinBrush("#E5DCC8"),
            .BorderThickness = New Thickness(1),
            .Padding = New Thickness(5)
        }
        Try
            Qr.Child = New Image With {
                .Source = New BitmapImage(New Uri("pack://application:,,,/Plain Craft Launcher 2;component/Images/SkinSite/qq-group.png")),
                .Stretch = Stretch.Uniform
            }
        Catch ex As Exception
            Logger.Warn(ex, "加载 QQ 群二维码失败")
        End Try
        Grid.SetColumn(Qr, 1)
        Grid.Children.Add(Qr)
        Card.Child = Grid
        Return Card
    End Function
    Private Function BuildSecurityStep() As FrameworkElement
        Dim Panel As New StackPanel With {.MaxWidth = 460, .HorizontalAlignment = HorizontalAlignment.Left}
        If SecStep = 1 Then
            InpSecMail = ModSkinSiteUi.SkinInput("name@example.com", ModSkinSiteIcons.IconMail)
            InpSecMail.SetText(SkinMail)
            InpSecPass = ModSkinSiteUi.SkinInput("6–32 位安全密码", ModSkinSiteIcons.IconKeyRound, True)
            Panel.Children.Add(FieldBlock("注册邮箱", InpSecMail.Root))
            Panel.Children.Add(FieldBlock("新密码", InpSecPass.Root))
            Panel.Children.Add(BuildSecurityError())
            Dim Next_ = SkinButton("验证邮箱", "primary", Sub()
                                                                    Dim Mail As String = InpSecMail.Text.Trim
                                                                    Dim MailError As String = SkinValidateMail(Mail)
                                                                    If MailError <> "" Then
                                                                        SecError = MailError
                                                                        ShowSecurityModal()
                                                                        Return
                                                                    End If
                                                                    Dim PassError As String = SkinValidatePassword(InpSecPass.Text)
                                                                    If PassError <> "" Then
                                                                        SecError = PassError
                                                                        ShowSecurityModal()
                                                                        Return
                                                                    End If
                                                                    SecError = ""
                                                                    SecVerified = False
                                                                    SecStep = 2
                                                                    ShowSecurityModal()
                                                                End Sub, ModSkinSiteIcons.IconArrowRight)
            Next_.Root.Margin = New Thickness(0, 6, 0, 0)
            Panel.Children.Add(Next_.Root)
        Else
            Panel.Children.Add(SkinText("向 " & SkinMailAddress & " 发送邮件，只需标题填写：", 11.5, "#64716C", False, False, True))
            Dim CodeBox As New Border With {
                .Background = SkinBrush("#F1F4EF"),
                .BorderBrush = SkinBrush("#9DB2A9"),
                .BorderThickness = New Thickness(1),
                .CornerRadius = New CornerRadius(13),
                .Margin = New Thickness(0, 12, 0, 0),
                .Cursor = Cursors.Hand,
                .Child = SkinText(SecCode, 26, "#214B3E", True, True)
            }
            Dim CodePanel As New StackPanel With {.Orientation = Orientation.Horizontal, .HorizontalAlignment = HorizontalAlignment.Center, .Margin = New Thickness(0, 18, 0, 18)}
            CodePanel.Children.Add(SkinText(SecCode, 26, "#214B3E", True, True))
            Dim CodeCopy = SkinIcon(ModSkinSiteIcons.IconCopy, 15, "#214B3E")
            CodeCopy.Margin = New Thickness(14, 0, 0, 0)
            CodePanel.Children.Add(CodeCopy)
            CodeBox.Child = CodePanel
            AddHandler CodeBox.MouseLeftButtonUp, Sub() ClipboardSet(SecCode, True, "验证码已复制")
            Panel.Children.Add(CodeBox)
            Dim CopyMail = SkinButton("复制验证邮箱", "secondary", Sub() ClipboardSet(SkinMailAddress, True, "验证邮箱已复制"), ModSkinSiteIcons.IconCopy, True)
            Panel.Children.Add(CopyMail.Root)
            Dim Verify = SkinButton(If(SecVerified, "验证邮件已收到", "验证是否发送成功"), If(SecVerified, "soft", "secondary"),
                                    Sub()
                                        RunTask(
                                        Sub()
                                            If Not SkinVerifyMail(InpSecMail.Text.Trim, SecCode) Then
                                                Throw New SkinSiteException("暂未收到验证邮件。请确认标题只有这 4 位验证码，稍等十几秒再检查。")
                                            End If
                                        End Sub,
                                        Sub()
                                            SecVerified = True
                                            SecError = ""
                                            Hint("验证邮件已收到，现在可以更新密码了。", HintType.Green, False)
                                            ShowSecurityModal()
                                        End Sub)
                                    End Sub, ModSkinSiteIcons.IconMailCheck)
            Verify.Root.Margin = New Thickness(0, 12, 0, 0)
            Verify.Root.HorizontalAlignment = HorizontalAlignment.Left
            Panel.Children.Add(Verify.Root)
            Panel.Children.Add(BuildVerifyNotice())
            Panel.Children.Add(BuildSecurityError())
            Dim Buttons As New StackPanel With {.Orientation = Orientation.Horizontal, .HorizontalAlignment = HorizontalAlignment.Right, .Margin = New Thickness(0, 8, 0, 0)}
            Dim Back = SkinButton("返回修改", "ghost", Sub()
                                                          SecStep = 1
                                                          SecVerified = False
                                                          SecError = ""
                                                          ShowSecurityModal()
                                                      End Sub)
            Buttons.Children.Add(Back.Root)
            Dim Update = SkinButton("更新密码", "primary", Sub()
                                                              If Not SecVerified Then
                                                                  SecError = "请先检查验证邮件是否已经收到。"
                                                                  ShowSecurityModal()
                                                                  Return
                                                              End If
                                                              Dim Mail As String = InpSecMail.Text.Trim
                                                              Dim Password As String = InpSecPass.Text
                                                              RunTask(
                                                              Sub() SkinModifyPassword(Mail, Password, SecCode),
                                                              Sub()
                                                                  SecCode = SkinNewCode()
                                                                  SkinPassword = If(SkinRemember, Password, "")
                                                                  SecStep = 1
                                                                  SecVerified = False
                                                                  SecError = ""
                                                                  Hint("密码已更新，下次登录请使用新密码。", HintType.Green, False)
                                                                  ShowSecurityModal()
                                                              End Sub)
                                                          End Sub)
            Update.Root.Margin = New Thickness(10, 0, 0, 0)
            Buttons.Children.Add(Update.Root)
            Panel.Children.Add(Buttons)
        End If
        Return Panel
    End Function

    ''' <summary>账号安全表单的错误提示框。</summary>
    Private Function BuildSecurityError() As FrameworkElement
        LabSecError = SkinText(SecError, 11.5, SkinColorDangerDark, False, False, True)
        Return New Border With {
            .Background = SkinBrush("#FFF3F2"),
            .BorderBrush = SkinBrush("#EDCCCC"),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(9),
            .Padding = New Thickness(13, 10, 13, 10),
            .Margin = New Thickness(0, 14, 0, 0),
            .Child = LabSecError,
            .Visibility = If(SecError = "", Visibility.Collapsed, Visibility.Visible)
        }
    End Function

#End Region

End Class

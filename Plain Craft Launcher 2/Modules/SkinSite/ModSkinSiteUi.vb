Imports System.Windows.Media.Effects
Imports System.Windows.Shapes

''' <summary>
''' 皮肤站页面所使用的界面元素工厂。
''' 配色、圆角、阴影与交互效果都对齐网页版（C:\codes\mcpixelart-skin-site-front\src\styles.css）。
''' 所有成员都必须在 UI 线程调用。
''' </summary>
Public Module ModSkinSiteUi

#Region "基础元素"

    ''' <summary>创建一个文本块。</summary>
    Public Function SkinText(Text As String, Optional Size As Double = 13, Optional Color As String = SkinColorInk,
                             Optional Bold As Boolean = False, Optional Mono As Boolean = False, Optional Wrap As Boolean = False) As TextBlock
        Dim Block As New TextBlock With {
            .Text = Text,
            .FontSize = Size,
            .Foreground = SkinBrush(Color),
            .TextWrapping = If(Wrap, TextWrapping.Wrap, TextWrapping.NoWrap),
            .VerticalAlignment = VerticalAlignment.Center
        }
        If Bold Then Block.FontWeight = FontWeights.Bold
        If Mono Then Block.FontFamily = New FontFamily(SkinFontMono)
        Return Block
    End Function

    ''' <summary>创建一个图标（使用与网页版相同的 lucide 路径，描边绘制）。</summary>
    Public Function SkinIcon(Data As String, Optional Size As Double = 18, Optional Color As String = SkinColorMuted,
                             Optional Thickness As Double = 2) As Viewbox
        Dim Path As New Shapes.Path With {
            .Data = Geometry.Parse(Data),
            .Stroke = SkinBrush(Color),
            .StrokeThickness = Thickness,
            .StrokeStartLineCap = PenLineCap.Round,
            .StrokeEndLineCap = PenLineCap.Round,
            .StrokeLineJoin = PenLineJoin.Round,
            .Fill = Brushes.Transparent,
            .SnapsToDevicePixels = True
        }
        '图标数据基于 24 × 24 视图区，用 Viewbox 缩放到目标尺寸
        Dim Box As New Viewbox With {
            .Width = Size, .Height = Size,
            .Stretch = Stretch.Uniform,
            .Child = Path,
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center
        }
        Return Box
    End Function

    ''' <summary>创建“图标 + 文字”的水平组合。</summary>
    Public Function SkinIconText(Data As String, Text As String, Optional Size As Double = 18, Optional Color As String = SkinColorMuted,
                                 Optional TextColor As String = SkinColorInk, Optional TextSize As Double = 13,
                                 Optional Bold As Boolean = False, Optional Gap As Double = 9) As StackPanel
        Dim Panel As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        If Data <> "" Then
            Dim Icon = SkinIcon(Data, Size, Color)
            Icon.Margin = New Thickness(0, 0, Gap, 0)
            Panel.Children.Add(Icon)
        End If
        Panel.Children.Add(SkinText(Text, TextSize, TextColor, Bold))
        Return Panel
    End Function

    ''' <summary>创建一个卡片容器。</summary>
    Public Function SkinCard(Optional Radius As Double = 17, Optional Padding As Thickness? = Nothing,
                             Optional Background As String = "#FFFFFF", Optional Line As String = SkinColorCardLine,
                             Optional Shadow As Boolean = True) As Border
        Dim Card As New Border With {
            .Background = SkinBrush(Background),
            .BorderBrush = SkinBrush(Line),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(Radius)
        }
        If Padding IsNot Nothing Then Card.Padding = Padding.Value
        If Shadow Then
            Card.Effect = New DropShadowEffect With {
                .Color = CType(ColorConverter.ConvertFromString("#1A332B"), Color),
                .BlurRadius = 30, .ShadowDepth = 6, .Direction = 270, .Opacity = 0.08
            }
        End If
        Return Card
    End Function

    ''' <summary>创建一个标签（与网页版 .tag 一致）。</summary>
    Public Function SkinTag(Text As String, Optional Kind As String = "default", Optional IconData As String = Nothing) As Border
        Dim Background As String = "#F5F7F3", Foreground As String = "#69756F", Line As String = "#DDE2DD"
        Select Case Kind
            Case "primary"
                Background = "#FFF4D9" : Foreground = "#674C1D" : Line = "#EBD49C"
            Case "success"
                Background = "#EDF8F3" : Foreground = "#39725F" : Line = "#CBE1D7"
            Case "danger"
                Background = "#FFF0F0" : Foreground = "#A84747" : Line = "#EDCCCC"
        End Select
        Dim Panel As New StackPanel With {.Orientation = Orientation.Horizontal}
        If IconData <> "" Then
            Dim Icon = SkinIcon(IconData, 12, Foreground, 2)
            Icon.Margin = New Thickness(0, 0, 4, 0)
            Panel.Children.Add(Icon)
        End If
        Panel.Children.Add(SkinText(Text, 11, Foreground, True))
        Return New Border With {
            .Background = SkinBrush(Background),
            .BorderBrush = SkinBrush(Line),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(99),
            .Padding = New Thickness(8, 4, 8, 4),
            .Child = Panel,
            .HorizontalAlignment = HorizontalAlignment.Left
        }
    End Function

    ''' <summary>创建与网页版一致的网格纹理画刷。</summary>
    Public Function SkinGridBrush(CellSize As Double, LineColor As String, Opacity As Double) As Brush
        Dim Group As New GeometryGroup
        Group.Children.Add(New LineGeometry(New Point(0, 0), New Point(CellSize, 0)))
        Group.Children.Add(New LineGeometry(New Point(0, 0), New Point(0, CellSize)))
        Dim Drawing As New GeometryDrawing(Nothing, New Pen(SkinBrush(LineColor), 1), Group)
        Return New DrawingBrush(Drawing) With {
            .TileMode = TileMode.Tile,
            .Viewport = New Rect(0, 0, CellSize, CellSize),
            .ViewportUnits = BrushMappingMode.Absolute,
            .Opacity = Opacity
        }
    End Function

    ''' <summary>创建带网格纹理的渐变底。</summary>
    Public Function SkinArtBackground(FromColor As String, ToColor As String, Optional GridCell As Double = 20, Optional Radius As Double = 12) As Border
        Dim Grid As New Rectangle With {.Fill = SkinGridBrush(GridCell, "#346150", 0.22)}
        Return New Border With {
            .CornerRadius = New CornerRadius(Radius),
            .Background = New LinearGradientBrush(
                CType(ColorConverter.ConvertFromString(FromColor), Color),
                CType(ColorConverter.ConvertFromString(ToColor), Color), New Point(0, 0), New Point(1, 1)),
            .Child = Grid,
            .ClipToBounds = True
        }
    End Function

#End Region

#Region "按钮与输入框"

    ''' <summary>按钮引用，便于后续切换文字与可用状态。</summary>
    Public Class SkinButtonRef
        Public Root As Border
        Public Label As TextBlock
        Public Icon As Viewbox
        Public OnClick As Action
        Friend Enabled As Boolean = True
        Friend NormalBackground As Brush
        Friend HoverBackground As Brush
        Public Sub SetEnabled(Value As Boolean)
            Enabled = Value
            Root.Opacity = If(Value, 1, 0.58)
            Root.Cursor = If(Value, Cursors.Hand, Cursors.Arrow)
        End Sub
        Public Sub SetText(Text As String)
            Label.Text = Text
        End Sub
    End Class

    ''' <summary>
    ''' 创建一个网页版风格的按钮。Kind：primary / secondary / dark / ghost / danger。
    ''' </summary>
    Public Function SkinButton(Text As String, Kind As String, OnClick As Action,
                               Optional IconData As String = Nothing, Optional Small As Boolean = False,
                               Optional Stretch As Boolean = False) As SkinButtonRef
        Dim Background As String = SkinColorLime, Foreground As String = "#13221E"
        Dim Hover As String = SkinColorLimeHover, Line As String = "Transparent"
        Select Case Kind
            Case "secondary"
                Background = "#FFFFFF" : Foreground = SkinColorForest : Hover = "#F6F9F5" : Line = "#CDD8CF"
            Case "dark"
                Background = SkinColorForestDark : Foreground = "#FFFFFF" : Hover = "#153D33"
            Case "ghost"
                Background = "Transparent" : Foreground = SkinColorForest : Hover = "#EDF1EB" : Line = SkinColorLine
            Case "danger"
                Background = SkinColorDanger : Foreground = "#FFFFFF" : Hover = "#C24848"
            Case "soft"
                Background = SkinColorSoftFill : Foreground = SkinColorSoftText : Hover = "#E4ECE7"
        End Select
        Dim Ref As New SkinButtonRef With {
            .OnClick = OnClick,
            .NormalBackground = SkinBrush(Background),
            .HoverBackground = SkinBrush(Hover)
        }
        Dim Panel As New StackPanel With {.Orientation = Orientation.Horizontal, .HorizontalAlignment = HorizontalAlignment.Center}
        If IconData <> "" Then
            Ref.Icon = SkinIcon(IconData, If(Small, 15, 17), Foreground)
            Ref.Icon.Margin = New Thickness(0, 0, 7, 0)
            Panel.Children.Add(Ref.Icon)
        End If
        Ref.Label = SkinText(Text, If(Small, 12.5, 13), Foreground, True)
        Panel.Children.Add(Ref.Label)
        Ref.Root = New Border With {
            .Background = Ref.NormalBackground,
            .BorderBrush = SkinBrush(Line),
            .BorderThickness = New Thickness(If(Line = "Transparent", 0, 1)),
            .CornerRadius = New CornerRadius(If(Small, 10, 12)),
            .Padding = New Thickness(If(Small, 14, 19), If(Small, 9, 11), If(Small, 14, 19), If(Small, 9, 11)),
            .Child = Panel,
            .Cursor = Cursors.Hand,
            .HorizontalAlignment = If(Stretch, HorizontalAlignment.Stretch, HorizontalAlignment.Left)
        }
        AddHandler Ref.Root.MouseEnter, Sub() If Ref.Enabled Then Ref.Root.Background = Ref.HoverBackground
        AddHandler Ref.Root.MouseLeave, Sub() If Ref.Enabled Then Ref.Root.Background = Ref.NormalBackground
        AddHandler Ref.Root.MouseLeftButtonUp,
            Sub(Sender As Object, E As MouseButtonEventArgs)
                If Not Ref.Enabled Then Return
                E.Handled = True
                Ref.OnClick?.Invoke()
            End Sub
        Return Ref
    End Function

    ''' <summary>输入框引用。</summary>
    Public Class SkinInputRef
        Public Root As Border
        ''' <summary>内部网格：第 0 列图标、第 1 列输入框、第 2 列附加元素。</summary>
        Public Inner As Grid
        Public Box As MyTextBox
        Public Password As PasswordBox
        ''' <summary>密码明文显示时使用的文本框。</summary>
        Public Reveal As MyTextBox
        Friend Revealed As Boolean = False
        Public ReadOnly Property Text As String
            Get
                If Password Is Nothing Then Return Box.Text
                Return If(Revealed, Reveal.Text, Password.Password)
            End Get
        End Property
        Public Sub SetText(Value As String)
            If Password Is Nothing Then
                Box.Text = Value
            Else
                Password.Password = Value
                If Reveal IsNot Nothing Then Reveal.Text = Value
            End If
        End Sub
        ''' <summary>切换密码的显示与隐藏。</summary>
        Public Sub ToggleReveal()
            If Password Is Nothing Then Return
            Revealed = Not Revealed
            If Revealed Then
                Reveal.Text = Password.Password
                Reveal.Visibility = Visibility.Visible
                Password.Visibility = Visibility.Collapsed
                Reveal.Focus()
            Else
                Password.Password = Reveal.Text
                Password.Visibility = Visibility.Visible
                Reveal.Visibility = Visibility.Collapsed
                Password.Focus()
            End If
        End Sub
    End Class

    ''' <summary>创建网页版风格的输入框。</summary>
    Public Function SkinInput(Hint As String, Optional IconData As String = Nothing, Optional IsPassword As Boolean = False,
                              Optional Mono As Boolean = False, Optional Height As Double = 49) As SkinInputRef
        Dim Ref As New SkinInputRef
        Dim Inner As New Grid With {.VerticalAlignment = VerticalAlignment.Center}
        Inner.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Inner.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Inner.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Ref.Inner = Inner
        Dim Font As New FontFamily(If(Mono, SkinFontMono, "Microsoft YaHei UI, Microsoft YaHei, sans-serif"))
        If IconData <> "" Then
            Dim Icon = SkinIcon(IconData, 17, "#87918D")
            Icon.Margin = New Thickness(0, 0, 10, 0)
            Grid.SetColumn(Icon, 0)
            Inner.Children.Add(Icon)
        End If
        If IsPassword Then
            Ref.Password = New PasswordBox With {
                .Background = Brushes.Transparent,
                .BorderThickness = New Thickness(0),
                .Foreground = SkinBrush(SkinColorInk),
                .FontSize = 13,
                .Padding = New Thickness(0),
                .VerticalContentAlignment = VerticalAlignment.Center,
                .FontFamily = Font,
                .Height = Height - 18
            }
            Grid.SetColumn(Ref.Password, 1)
            Inner.Children.Add(Ref.Password)
            Ref.Reveal = New MyTextBox With {
                .Background = Brushes.Transparent,
                .BorderThickness = New Thickness(0),
                .Foreground = SkinBrush(SkinColorInk),
                .FontSize = 13,
                .Padding = New Thickness(0),
                .VerticalContentAlignment = VerticalAlignment.Center,
                .FontFamily = Font,
                .Height = Height - 18,
                .ShowValidateResult = False,
                .Visibility = Visibility.Collapsed
            }
            Grid.SetColumn(Ref.Reveal, 1)
            Inner.Children.Add(Ref.Reveal)
            '显示 / 隐藏密码
            Dim Eye As New Border With {
                .Width = 26, .Height = 26,
                .Margin = New Thickness(6, 0, 0, 0),
                .Cursor = Cursors.Hand,
                .Child = SkinIcon(ModSkinSiteIcons.IconEye, 16, "#87918D")
            }
            AddHandler Eye.MouseLeftButtonUp,
                Sub(Sender As Object, E As MouseButtonEventArgs)
                    E.Handled = True
                    Ref.ToggleReveal()
                    Eye.Child = SkinIcon(If(Ref.Revealed, ModSkinSiteIcons.IconEyeOff, ModSkinSiteIcons.IconEye), 16, "#87918D")
                End Sub
            Grid.SetColumn(Eye, 2)
            Inner.Children.Add(Eye)
        Else
            Ref.Box = New MyTextBox With {
                .Background = Brushes.Transparent,
                .BorderThickness = New Thickness(0),
                .Foreground = SkinBrush(SkinColorInk),
                .FontSize = 13,
                .Padding = New Thickness(0),
                .HintText = Hint,
                .VerticalContentAlignment = VerticalAlignment.Center,
                .FontFamily = Font,
                .Height = Height - 18,
                .ShowValidateResult = False
            }
            Grid.SetColumn(Ref.Box, 1)
            Inner.Children.Add(Ref.Box)
        End If
        Ref.Root = New Border With {
            .Background = SkinBrush("#FFFFFF"),
            .BorderBrush = SkinBrush("#D6DCD4"),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(11),
            .Padding = New Thickness(14, 0, 14, 0),
            .Height = Height,
            .Child = Inner
        }
        Dim FocusIn As RoutedEventHandler =
            Sub() Ref.Root.BorderBrush = SkinBrush("#6E9B8C")
        Dim FocusOut As RoutedEventHandler =
            Sub() Ref.Root.BorderBrush = SkinBrush("#D6DCD4")
        If IsPassword Then
            AddHandler Ref.Password.GotFocus, FocusIn
            AddHandler Ref.Password.LostFocus, FocusOut
        Else
            AddHandler Ref.Box.GotFocus, FocusIn
            AddHandler Ref.Box.LostFocus, FocusOut
        End If
        Return Ref
    End Function

#End Region

#Region "皮肤人物"

    ''' <summary>
    ''' 与网页版 SkinFigure 一致的人物预览：用皮肤贴图裁剪出头部、身体与四肢。
    ''' Image 为完整皮肤贴图，Height 为人物高度（网页版完整尺寸为 170 × 310）。
    ''' </summary>
    Public Function SkinFigure(Image As BitmapSource, Optional Height As Double = 310) As FrameworkElement
        Const W As Double = 170, H As Double = 310
        Dim Area As New Canvas With {.Width = W, .Height = H}
        If Image Is Nothing Then Return SkinFigurePlaceholder(Height)
        '地面阴影
        Dim Shadow As New Ellipse With {
            .Width = W - 32, .Height = 24,
            .Fill = SkinBrush("#2E0E241D")
        }
        Shadow.Effect = New BlurEffect With {.Radius = 7}
        Canvas.SetLeft(Shadow, 16)
        Canvas.SetTop(Shadow, H - 24)
        Area.Children.Add(Shadow)
        For Each Part In SkinFigureParts(Image)
            Dim Target As New FrameworkElement
            Dim Left As Double = 0, Top As Double = 0, Width As Double = 0, Rotate As Double = 0
            Select Case Part.Name
                Case "head"
                    Left = 46 : Top = 5 : Width = 80 : Rotate = -2
                Case "body"
                    Left = 46 : Top = 86 : Width = 80
                Case "armLeft"
                    Left = 7 : Top = 88 : Width = 40 : Rotate = 5
                Case "armRight"
                    Left = 127 : Top = 88 : Width = 40 : Rotate = -7
                Case "legLeft"
                    Left = 46 : Top = 205 : Width = 40 : Rotate = 2
                Case "legRight"
                    Left = 86 : Top = 205 : Width = 40 : Rotate = -2
                Case Else
                    Continue For
            End Select
            Dim PartHeight As Double = Width * (Part.Image.PixelHeight / Part.Image.PixelWidth)
            Dim Picture As New Image With {
                .Source = Part.Image,
                .Stretch = Stretch.Fill,
                .Width = Width,
                .Height = PartHeight,
                .SnapsToDevicePixels = True
            }
            RenderOptions.SetBitmapScalingMode(Picture, BitmapScalingMode.NearestNeighbor)
            '与网页版一致：像素背景 + 内侧描边
            Dim Wrapper As New Border With {
                .Width = Width,
                .Height = PartHeight,
                .Background = SkinBrush("#D8DDD9"),
                .Child = Picture,
                .ClipToBounds = True
            }
            If Rotate <> 0 Then
                Wrapper.RenderTransformOrigin = New Point(0.5, If(Part.Name.StartsWithF("arm") OrElse Part.Name.StartsWithF("leg"), 0, 0.5))
                Wrapper.RenderTransform = New RotateTransform(Rotate)
            End If
            Canvas.SetLeft(Wrapper, Left)
            Canvas.SetTop(Wrapper, Top)
            Area.Children.Add(Wrapper)
        Next
        Dim Box As New Viewbox With {
            .Height = Height,
            .Stretch = Stretch.Uniform,
            .Child = Area
        }
        Box.Effect = New DropShadowEffect With {
            .Color = CType(ColorConverter.ConvertFromString("#14241F"), Color),
            .BlurRadius = 18, .ShadowDepth = 9, .Direction = 270, .Opacity = 0.18
        }
        Return Box
    End Function

    ''' <summary>没有皮肤时的占位人物（与网页版 skin-placeholder 一致的配色）。</summary>
    Public Function SkinFigurePlaceholder(Optional Height As Double = 310) As FrameworkElement
        Const W As Double = 170, H As Double = 310
        Dim Area As New Canvas With {.Width = W, .Height = H}
        Dim Make As Func(Of Double, Double, Double, Double, String, String, FrameworkElement) =
            Function(Left, Top, Width, PartHeight, FromColor, ToColor)
                Return New Border With {
                    .Width = Width, .Height = PartHeight,
                    .CornerRadius = New CornerRadius(4),
                    .Background = New LinearGradientBrush(
                        CType(ColorConverter.ConvertFromString(FromColor), Color),
                        CType(ColorConverter.ConvertFromString(ToColor), Color), New Point(0, 0), New Point(1, 1)),
                    .Opacity = 0.95
                }
            End Function
        '头
        Dim Head = Make(46, 5, 80, 80, "#D3E5DD", "#A6C9BA")
        Canvas.SetLeft(Head, 46) : Canvas.SetTop(Head, 5) : Area.Children.Add(Head)
        '身体、双臂、双腿
        Dim Body = Make(46, 86, 80, 120, "#8CBAA8", "#5C9280")
        Canvas.SetLeft(Body, 46) : Canvas.SetTop(Body, 86) : Area.Children.Add(Body)
        Dim ArmLeft = Make(6, 88, 40, 120, "#8CBAA8", "#79AA98")
        Canvas.SetLeft(ArmLeft, 6) : Canvas.SetTop(ArmLeft, 88) : Area.Children.Add(ArmLeft)
        Dim ArmRight = Make(124, 88, 40, 120, "#8CBAA8", "#79AA98")
        Canvas.SetLeft(ArmRight, 124) : Canvas.SetTop(ArmRight, 88) : Area.Children.Add(ArmRight)
        Dim LegLeft = Make(46, 205, 40, 120, "#5C9280", "#406F60")
        Canvas.SetLeft(LegLeft, 46) : Canvas.SetTop(LegLeft, 205) : Area.Children.Add(LegLeft)
        Dim LegRight = Make(86, 205, 40, 120, "#5C9280", "#406F60")
        Canvas.SetLeft(LegRight, 86) : Canvas.SetTop(LegRight, 205) : Area.Children.Add(LegRight)
        '图标
        Dim Icon = SkinIcon(ModSkinSiteIcons.IconImageOff, 26, "#7AA694")
        Canvas.SetLeft(Icon, 72) : Canvas.SetTop(Icon, 34) : Area.Children.Add(Icon)
        Return New Viewbox With {.Height = Height, .Stretch = Stretch.Uniform, .Child = Area}
    End Function

    ''' <summary>披风预览：裁出贴图中正反两面所在的区域。</summary>
    Public Function SkinCape(Image As BitmapSource, Optional Width As Double = 78) As FrameworkElement
        If Image Is Nothing Then
            Dim Placeholder As New Border With {
                .Width = Width, .Height = Width * 16 / 21,
                .CornerRadius = New CornerRadius(6),
                .Background = SkinBrush("#E7ECE7")
            }
            Return Placeholder
        End If
        Dim Picture As New Image With {
            .Source = SkinCropCape(Image),
            .Stretch = Stretch.Fill,
            .Width = Width,
            .Height = Width * 16 / 21
        }
        RenderOptions.SetBitmapScalingMode(Picture, BitmapScalingMode.NearestNeighbor)
        Return Picture
    End Function

#End Region

#Region "服务器名称"

    ''' <summary>
    ''' 服务器名称，与网页版 .minecraft-motd 完全一致的配色与字号（For·ever·Life·time）。
    ''' </summary>
    Public Function SkinBrandName(Optional Size As Double = 15) As StackPanel
        Dim Panel As New StackPanel With {.Orientation = Orientation.Horizontal}
        Dim Pieces() As (Text As String, Color As String) = {
            ("For", SkinColorName1), ("ever", SkinColorName2), (" · ", SkinColorName3),
            ("Life", SkinColorName4), ("time", SkinColorName5)}
        For Each Piece In Pieces
            Panel.Children.Add(New TextBlock With {
                .Text = Piece.Text,
                .FontSize = Size,
                .FontWeight = FontWeights.Bold,
                .FontFamily = New FontFamily(SkinFontMono),
                .Foreground = SkinBrush(Piece.Color)
            })
        Next
        Return Panel
    End Function

#End Region

End Module

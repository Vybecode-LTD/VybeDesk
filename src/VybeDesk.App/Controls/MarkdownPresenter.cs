using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace VybeDesk.App.Controls;

/// <summary>
/// Renders Markdown text as a stack of native Avalonia controls. Built on
/// Markdig for parsing; the AST walker is custom so we have full control
/// over the layout (no opaque third-party templates, no zero-height
/// surprises). Bind <see cref="Markdown"/> to a string — on every change
/// the body re-renders into the <see cref="ContentControl.Content"/>
/// slot.
/// </summary>
public sealed class MarkdownPresenter : ContentControl
{
    private static IBrush CodeBackground   => ResolveBrush("StratumSurface0", "#1B1B22");
    private static IBrush InlineCodeBg     => ResolveBrush("StratumSurface2", "#33333E");
    private static IBrush QuoteBarBrush    => ResolveBrush("StratumInfo",     "#5E8FE0");
    private static IBrush TableBorderBrush => ResolveBrush("StratumBorder1",  "#3A3A45");
    private static IBrush LinkBrush        => ResolveBrush("StratumInfo",     "#9ABEE0");

    private static readonly FontFamily Mono =
        new("Cascadia Code,Cascadia Mono,Consolas,monospace");

    private static IBrush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var v) == true
            && v is IBrush b)
            return b;
        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .Build();

    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownPresenter, string?>(nameof(Markdown));

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    static MarkdownPresenter()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownPresenter>((s, _) => s.Render());
    }

    private void Render()
    {
        var text = Markdown ?? "";
        if (string.IsNullOrEmpty(text))
        {
            Content = null;
            return;
        }

        try
        {
            var doc = Markdig.Markdown.Parse(text, Pipeline);
            var stack = new StackPanel { Spacing = 8 };
            foreach (var block in doc)
            {
                var control = RenderBlock(block);
                if (control is not null) stack.Children.Add(control);
            }
            Content = stack;
        }
        catch
        {
            // Anything the parser chokes on falls back to plain text rather
            // than blanking the bubble.
            Content = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        }
    }

    // ── block rendering ──────────────────────────────────────────────

    private Control? RenderBlock(Block block) => block switch
    {
        HeadingBlock h         => RenderHeading(h),
        ParagraphBlock p       => RenderParagraph(p),
        FencedCodeBlock f      => RenderCodeBlock(f.Lines.ToString(), f.Info ?? ""),
        CodeBlock c            => RenderCodeBlock(c.Lines.ToString(), ""),
        ListBlock l            => RenderList(l),
        QuoteBlock q           => RenderQuote(q),
        Table t                => RenderTable(t),
        ThematicBreakBlock _   => new Border
        {
            Height = 1,
            Background = TableBorderBrush,
            Margin = new Thickness(0, 4),
        },
        _ => null,
    };

    private TextBlock RenderHeading(HeadingBlock h)
    {
        var size = h.Level switch
        {
            1 => 22.0,
            2 => 18.0,
            3 => 16.0,
            _ => 14.0,
        };
        var tb = new SelectableTextBlock
        {
            FontSize = size,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, h.Level == 1 ? 6 : 4, 0, 2),
        };
        if (h.Inline is not null) PopulateInlines(tb.Inlines!, h.Inline);
        return tb;
    }

    private TextBlock RenderParagraph(ParagraphBlock p)
    {
        var tb = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        if (p.Inline is not null) PopulateInlines(tb.Inlines!, p.Inline);
        return tb;
    }

    private Border RenderCodeBlock(string code, string language)
    {
        var body = new SelectableTextBlock
        {
            Text = code.TrimEnd('\n', '\r'),
            FontFamily = Mono,
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
        };
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = body,
        };
        return new Border
        {
            Background = CodeBackground,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10),
            Child = scroll,
        };
    }

    private StackPanel RenderList(ListBlock list)
    {
        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(8, 0, 0, 0) };
        int index = 1;
        foreach (var item in list)
        {
            if (item is not ListItemBlock li) continue;
            var bullet = list.IsOrdered ? (index++ + ".") : "•";
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Margin = new Thickness(0, 2, 0, 0),
            };
            var bulletTb = new TextBlock
            {
                Text = bullet,
                Margin = new Thickness(0, 0, 8, 0),
                Opacity = 0.7,
            };
            Grid.SetColumn(bulletTb, 0);
            grid.Children.Add(bulletTb);

            var contentStack = new StackPanel { Spacing = 4 };
            foreach (var child in li)
            {
                var rendered = RenderBlock(child);
                if (rendered is not null) contentStack.Children.Add(rendered);
            }
            Grid.SetColumn(contentStack, 1);
            grid.Children.Add(contentStack);

            stack.Children.Add(grid);
        }
        return stack;
    }

    private Border RenderQuote(QuoteBlock quote)
    {
        var inner = new StackPanel { Spacing = 4 };
        foreach (var child in quote)
        {
            var rendered = RenderBlock(child);
            if (rendered is not null) inner.Children.Add(rendered);
        }
        return new Border
        {
            BorderBrush = QuoteBarBrush,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 2, 0, 2),
            Margin = new Thickness(0, 2),
            Child = inner,
        };
    }

    private Border RenderTable(Table table)
    {
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        var maxCols = 0;
        foreach (var row in table)
            if (row is TableRow tr && tr.Count > maxCols) maxCols = tr.Count;

        // Measure the longest text in each column, then use sqrt-dampened
        // star weights so a 400-char column doesn't take 20x the space of
        // a 20-char column. The sqrt compresses ratios to roughly 4.5:1
        // in that example — enough to give wider columns more room without
        // starving narrow ones.
        var maxLengths = new int[maxCols];
        var headerLengths = new int[maxCols];
        foreach (var rowObj in table)
        {
            if (rowObj is not TableRow row) continue;
            for (int i = 0; i < row.Count && i < maxCols; i++)
            {
                if (row[i] is not TableCell cell) continue;
                var len = MeasureCellTextLength(cell);
                if (len > maxLengths[i]) maxLengths[i] = len;
                if (row.IsHeader) headerLengths[i] = Math.Max(headerLengths[i], len);
            }
        }

        const double headerPxPerChar = 8.5;
        const double cellPaddingX    = 16;
        for (int c = 0; c < maxCols; c++)
        {
            var raw = Math.Max(1, maxLengths[c]);
            var weight = Math.Max(1.0, Math.Sqrt(raw));
            var minW = (headerLengths[c] * headerPxPerChar) + cellPaddingX;
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width    = new GridLength(weight, GridUnitType.Star),
                MinWidth = minW,
            });
        }

        int rowIndex = 0;
        foreach (var rowObj in table)
        {
            if (rowObj is not TableRow row) continue;
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            int colIndex = 0;
            foreach (var cellObj in row)
            {
                if (cellObj is not TableCell cell) continue;
                var cellStack = new StackPanel { Spacing = 2 };
                foreach (var child in cell)
                {
                    var rendered = RenderBlock(child);
                    if (rendered is null) continue;
                    // Headers stay on a single line so e.g. "Audience"
                    // doesn't wrap its trailing 'e'. NoWrap on a star
                    // column also makes the column grow to fit the
                    // header, then body cells wrap inside whatever
                    // remains. Bumping FontWeight to make the header
                    // read as a header even before the bg cue lands.
                    if (row.IsHeader && rendered is TextBlock tb)
                    {
                        tb.TextWrapping = TextWrapping.NoWrap;
                        tb.FontWeight = FontWeight.SemiBold;
                    }
                    cellStack.Children.Add(rendered);
                }
                var border = new Border
                {
                    BorderBrush = TableBorderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(8, 4),
                    Background = row.IsHeader
                        ? ResolveBrush("StratumSurface1", "#2A2A33")
                        : null,
                    Child = cellStack,
                };
                Grid.SetRow(border, rowIndex);
                Grid.SetColumn(border, colIndex);
                grid.Children.Add(border);
                colIndex++;
            }
            rowIndex++;
        }

        return new Border
        {
            BorderBrush = TableBorderBrush,
            BorderThickness = new Thickness(1, 1, 0, 0),
            Margin = new Thickness(0, 2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = grid,
        };
    }

    /// <summary>
    /// Sum of literal text + inline-code characters inside a table cell.
    /// Used to weight column widths so longer columns get more space.
    /// Cheap walk — only LiteralInline / CodeInline contribute.
    /// </summary>
    private static int MeasureCellTextLength(TableCell cell)
    {
        int total = 0;
        foreach (var block in cell)
        {
            if (block is ParagraphBlock p && p.Inline is not null)
                total += MeasureInlineTextLength(p.Inline);
        }
        return total;
    }

    private static int MeasureInlineTextLength(ContainerInline container)
    {
        int total = 0;
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    total += lit.Content.Length;
                    break;
                case CodeInline code:
                    total += code.Content.Length;
                    break;
                case ContainerInline sub:
                    total += MeasureInlineTextLength(sub);
                    break;
            }
        }
        return total;
    }

    // ── inline rendering ─────────────────────────────────────────────

    private void PopulateInlines(InlineCollection target, ContainerInline container)
    {
        foreach (var inline in container)
            AppendInline(target, inline);
    }

    private void AppendInline(InlineCollection target, Markdig.Syntax.Inlines.Inline inline)
    {
        switch (inline)
        {
            case LiteralInline lit:
                target.Add(new Run(lit.Content.ToString()));
                break;

            case CodeInline code:
                target.Add(new Run(code.Content)
                {
                    FontFamily = Mono,
                    FontSize = 12,
                    Background = InlineCodeBg,
                });
                break;

            case EmphasisInline em:
            {
                // ** or __ = strong, * or _ = emphasis (italic)
                var isStrong = em.DelimiterCount >= 2;
                var span = new Span
                {
                    FontWeight = isStrong ? FontWeight.SemiBold : FontWeight.Normal,
                    FontStyle = isStrong ? FontStyle.Normal : FontStyle.Italic,
                };
                PopulateInlines(span.Inlines!, em);
                target.Add(span);
                break;
            }

            case LineBreakInline lb when lb.IsHard:
                target.Add(new LineBreak());
                break;

            case LinkInline link:
            {
                // Render as styled run with [text](url) layout. Click-handling
                // can come later.
                var span = new Span { Foreground = LinkBrush };
                PopulateInlines(span.Inlines!, link);
                target.Add(span);
                if (!string.IsNullOrEmpty(link.Url))
                {
                    target.Add(new Run(" (" + link.Url + ")")
                    {
                        FontSize = 11,
                        Foreground = LinkBrush,
                    });
                }
                break;
            }

            case AutolinkInline auto:
                target.Add(new Run(auto.Url) { Foreground = LinkBrush });
                break;

            case ContainerInline container:
                PopulateInlines(target, container);
                break;
        }
    }
}

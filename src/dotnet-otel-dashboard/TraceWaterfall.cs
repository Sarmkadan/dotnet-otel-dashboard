namespace DotnetOtelDashboard;

/// <summary>
/// One row of a trace waterfall: a span together with its position on the trace timeline.
/// </summary>
/// <param name="Span">The span this row represents.</param>
/// <param name="Depth">Nesting depth in the span tree. Root spans are at depth 0.</param>
/// <param name="Offset">Start of the span relative to the start of the trace.</param>
/// <param name="OffsetPercent"><paramref name="Offset"/> as a percentage of the total trace duration.</param>
/// <param name="WidthPercent">Span duration as a percentage of the total trace duration, at least 0.1.</param>
public sealed record WaterfallRow(SpanRecord Span, int Depth, TimeSpan Offset, double OffsetPercent, double WidthPercent);

/// <summary>
/// Lays out the spans of one trace as a depth-first waterfall for the UI.
/// </summary>
public static class TraceWaterfall
{
    /// <summary>Minimum rendered width in percent, so that zero-length spans remain visible.</summary>
    private const double MinWidthPercent = 0.1;

    /// <summary>
    /// Builds waterfall rows for the spans of one trace.
    /// </summary>
    /// <remarks>
    /// Spans whose <see cref="SpanRecord.ParentSpanId"/> is missing from <paramref name="spans"/> become roots.
    /// Roots and siblings are ordered by start time, then by span id. Spans that are only reachable through a
    /// cycle, with no root above them, are not emitted.
    /// </remarks>
    /// <param name="spans">Spans of a single trace, in any order.</param>
    /// <returns>Rows in depth-first pre-order, or an empty list when <paramref name="spans"/> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spans"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<WaterfallRow> Build(IReadOnlyList<SpanRecord> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);

        if (spans.Count == 0)
        {
            return [];
        }

        var traceStart = spans.Min(s => s.StartTime);
        var traceEnd = spans.Max(s => s.StartTime + s.Duration);
        var total = traceEnd - traceStart;
        if (total <= TimeSpan.Zero)
        {
            total = TimeSpan.FromTicks(1);
        }

        var totalTicks = (double)total.Ticks;

        var ordered = spans
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.SpanId, StringComparer.Ordinal)
            .ToList();

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var span in ordered)
        {
            ids.Add(span.SpanId);
        }

        var children = new Dictionary<string, List<SpanRecord>>(StringComparer.Ordinal);
        var roots = new List<SpanRecord>();
        foreach (var span in ordered)
        {
            if (span.ParentSpanId is { } parentId && ids.Contains(parentId))
            {
                if (!children.TryGetValue(parentId, out var siblings))
                {
                    siblings = [];
                    children[parentId] = siblings;
                }

                siblings.Add(span);
            }
            else
            {
                roots.Add(span);
            }
        }

        var rows = new List<WaterfallRow>(ordered.Count);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        // Explicit stack instead of recursion, so deep span trees cannot overflow the call stack.
        // Children are pushed in reverse so they pop in start-time order.
        var stack = new Stack<(SpanRecord Span, int Depth)>();
        for (var i = roots.Count - 1; i >= 0; i--)
        {
            stack.Push((roots[i], 0));
        }

        while (stack.Count > 0)
        {
            var (span, depth) = stack.Pop();
            if (!visited.Add(span.SpanId))
            {
                continue;
            }

            var offset = span.StartTime - traceStart;
            rows.Add(new WaterfallRow(
                span,
                depth,
                offset,
                offset.Ticks / totalTicks * 100,
                Math.Max(span.Duration.Ticks / totalTicks * 100, MinWidthPercent)));

            if (children.TryGetValue(span.SpanId, out var kids))
            {
                for (var i = kids.Count - 1; i >= 0; i--)
                {
                    stack.Push((kids[i], depth + 1));
                }
            }
        }

        return rows;
    }
}

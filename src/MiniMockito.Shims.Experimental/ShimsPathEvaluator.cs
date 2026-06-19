namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Internal evaluator for the Phase 24 property-path syntax used by the inspection API.
/// </summary>
/// <remarks>
/// Supported syntax:
/// <list type="bullet">
///   <item><description>Property / field access: <c>Items</c>, <c>Items.Count</c>, <c>SelectedUser.Name</c></description></item>
///   <item><description>Indexer access: <c>Items[0]</c>, <c>Items[0].Name</c>, <c>Rows[1].Cells[2].Text</c></description></item>
/// </list>
/// A <see langword="null"/> encountered <b>mid-path</b> raises a <see cref="ShimsInspectionException"/>;
/// a <see langword="null"/> at the final segment is returned as-is.
/// </remarks>
internal static class ShimsPathEvaluator
{
    internal static object? Evaluate(object root, string path)
    {
        ThrowHelper.ThrowIfNull(root);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ShimsInspectionException(
                "Inspection failed: the property path must be a non-empty string.");
        }

        var segments = path.Split('.');
        object? current = root;

        for (var i = 0; i < segments.Length; i++)
        {
            var rawSegment = segments[i].Trim();
            var isLastSegment = i == segments.Length - 1;

            ParseSegment(rawSegment, path, out var memberName, out var indices);

            if (memberName.Length > 0)
            {
                if (current is null)
                    throw NullEncountered(path, rawSegment);

                current = ShimsReflectionAccessor.GetMember(current, memberName, path, rawSegment);
            }

            for (var j = 0; j < indices.Count; j++)
            {
                if (current is null)
                    throw NullEncountered(path, rawSegment);

                current = ShimsReflectionAccessor.GetIndex(current, indices[j], path, rawSegment);
            }

            // A null mid-path cannot be navigated further; a null at the end is a valid leaf value.
            if (current is null && !isLastSegment)
                throw NullEncountered(path, rawSegment);
        }

        return current;
    }

    private static void ParseSegment(string segment, string path, out string memberName, out List<int> indices)
    {
        indices = new List<int>();

        var bracket = segment.IndexOf('[');
        if (bracket < 0)
        {
            memberName = segment;
            return;
        }

        memberName = segment.Substring(0, bracket).Trim();
        var rest = segment.Substring(bracket);

        while (rest.Length > 0)
        {
            if (rest[0] != '[')
                throw MalformedPath(path, segment);

            var close = rest.IndexOf(']');
            if (close < 0)
                throw MalformedPath(path, segment);

            var number = rest.Substring(1, close - 1).Trim();
            if (!int.TryParse(number, out var index))
                throw MalformedPath(path, segment);

            indices.Add(index);
            rest = rest.Substring(close + 1).Trim();
        }
    }

    private static ShimsInspectionException NullEncountered(string path, string segment)
    {
        return new ShimsInspectionException(string.Join(
            Environment.NewLine,
            "Inspection failed: null was encountered while evaluating the path.",
            "Requested path: " + path,
            "Failed segment: " + segment,
            "Reason: a value along the path was null and cannot be navigated further."));
    }

    private static ShimsInspectionException MalformedPath(string path, string segment)
    {
        return new ShimsInspectionException(string.Join(
            Environment.NewLine,
            "Inspection failed: the property path is malformed.",
            "Requested path: " + path,
            "Failed segment: " + segment,
            "Reason: expected 'Name', 'Name[index]', or chained '[index]' with integer indices."));
    }
}

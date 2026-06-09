using Mono.Cecil;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Coordinates experimental assembly rewrite operations.
/// </summary>
public static class AssemblyRewriter
{
    /// <summary>
    /// Rewrites allowlisted parameterless <c>newobj</c> call sites to <see cref="ShimDispatcher.New{T}"/>.
    /// </summary>
    /// <param name="inputAssemblyPath">The assembly to inspect and rewrite.</param>
    /// <param name="outputAssemblyPath">The output assembly path. The original assembly is never overwritten.</param>
    /// <param name="options">The rewrite options.</param>
    /// <returns>The rewrite result and diagnostics.</returns>
    public static RewriteResult RewriteNewObj(
        string inputAssemblyPath,
        string outputAssemblyPath,
        RewriteOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputAssemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputAssemblyPath);
        ArgumentNullException.ThrowIfNull(options);

        var inputFullPath = Path.GetFullPath(inputAssemblyPath);
        var outputFullPath = Path.GetFullPath(outputAssemblyPath);

        if (!File.Exists(inputFullPath))
        {
            throw new ShimRewriteException($"Input assembly was not found: {inputFullPath}");
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(inputFullPath, outputFullPath))
        {
            throw new ShimRewriteException("The rewritten assembly path must be different from the original assembly path.");
        }

        var outputDirectory = Path.GetDirectoryName(outputFullPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ShimRewriteException($"Output assembly path must include a directory: {outputFullPath}");
        }

        Directory.CreateDirectory(outputDirectory);

        var report = AssemblyRewriteScanner.Scan(inputFullPath, options.ToScanOptions());
        var diagnostics = new List<string>
        {
            $"Dry-run scan found {report.SupportedCallSites.Count} supported and {report.UnsupportedCallSites.Count} unsupported allowlisted newobj call site(s).",
        };

        var resolver = CreateAssemblyResolver(inputFullPath);
        using var module = ModuleDefinition.ReadModule(
            inputFullPath,
            new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadSymbols = false,
            });

        var rewrittenCount = NewObjRewriter.Rewrite(module, options, diagnostics);

        // Phase 14: also rewrite static call sites when StaticTargetTypes is specified.
        if (options.StaticTargetTypes.Count > 0)
        {
            var staticResult = StaticCallRewriter.Rewrite(module, options, diagnostics);
            rewrittenCount += staticResult.RewrittenCallSiteCount;
        }

        module.Write(
            outputFullPath,
            new WriterParameters
            {
                WriteSymbols = false,
            });

        if (options.CopyRuntimeFiles)
        {
            CopyRuntimeFiles(inputFullPath, outputFullPath, diagnostics);
        }

        diagnostics.Add($"Wrote rewritten assembly to {outputFullPath}.");

        return new RewriteResult(inputFullPath, outputFullPath, report, rewrittenCount, diagnostics);
    }

    private static IAssemblyResolver CreateAssemblyResolver(string inputAssemblyPath)
    {
        var resolver = new DefaultAssemblyResolver();
        var inputDirectory = Path.GetDirectoryName(inputAssemblyPath);
        if (!string.IsNullOrWhiteSpace(inputDirectory))
        {
            resolver.AddSearchDirectory(inputDirectory);
        }

        var shimDirectory = Path.GetDirectoryName(typeof(ShimDispatcher).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(shimDirectory))
        {
            resolver.AddSearchDirectory(shimDirectory);
        }

        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            resolver.AddSearchDirectory(runtimeDirectory);
        }

        return resolver;
    }

    private static void CopyRuntimeFiles(string inputAssemblyPath, string outputAssemblyPath, IList<string> diagnostics)
    {
        var outputDirectory = Path.GetDirectoryName(outputAssemblyPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        CopyIfExists(
            typeof(ShimDispatcher).Assembly.Location,
            Path.Combine(outputDirectory, Path.GetFileName(typeof(ShimDispatcher).Assembly.Location)),
            diagnostics);

        foreach (var extension in new[] { ".deps.json", ".runtimeconfig.json" })
        {
            var sourcePath = Path.ChangeExtension(inputAssemblyPath, extension);
            if (File.Exists(sourcePath))
            {
                CopyIfExists(sourcePath, Path.Combine(outputDirectory, Path.GetFileName(sourcePath)), diagnostics);
            }
        }
    }

    private static void CopyIfExists(string sourcePath, string destinationPath, IList<string> diagnostics)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath)))
        {
            return;
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
        diagnostics.Add($"Copied dependency {Path.GetFileName(sourcePath)} to rewritten assembly output directory.");
    }
}

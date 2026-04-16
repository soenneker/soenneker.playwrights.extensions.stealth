using System;
using System.Collections.Generic;
using System.Linq;
using Soenneker.Playwrights.Extensions.Stealth.Options;
using Soenneker.Extensions.String;

namespace Soenneker.Playwrights.Extensions.Stealth;

internal static class StealthLaunchArgumentNormalizer
{
    public static readonly string[] DetectableDefaultArgumentsToIgnore =
    [
        "--enable-automation",
        "--disable-popup-blocking",
        "--disable-component-update",
        "--disable-default-apps",
        "--disable-extensions",
        "--disable-client-side-phishing-detection",
        "--disable-component-extensions-with-background-pages",
        "--allow-pre-commit-input",
        "--disable-ipc-flooding-protection",
        "--metrics-recording-only",
        "--unsafely-disable-devtools-self-xss-warnings",
        "--disable-back-forward-cache",
        "--disable-features=ImprovedCookieControls,LazyFrameLoading,GlobalMediaControls,DestroyProfileOnBrowserClose,MediaRouter,DialMediaRouteProvider,AcceptCHFrame,AutoExpandDetailsElement,CertificateTransparencyComponentUpdater,AvoidUnnecessaryBeforeUnloadCheckSync,Translate,HttpsUpgrades,PaintHolding,ThirdPartyStoragePartitioning,LensOverlay,PlzDedicatedWorker",
        "--enable-unsafe-swiftshader"
    ];

    private static readonly string[] _defaultArguments =
    [
        "--disable-blink-features=AutomationControlled",
        "--enable-quic",
        "--use-gl=desktop"
    ];

    private static readonly HashSet<string> _detectableArguments = new(DetectableDefaultArgumentsToIgnore, StringComparer.OrdinalIgnoreCase);

    public static string[] Normalize(IEnumerable<string>? existingArguments, bool isHeadlessLaunch = false, StealthLaunchOptions? options = null)
    {
        options ??= new StealthLaunchOptions();

        var result = new List<string>();

        if (existingArguments is not null)
        {
            foreach (string argument in existingArguments)
            {
                if (argument.IsNullOrWhiteSpace())
                    continue;

                if (options.RemoveDetectableArguments && ShouldRemove(argument))
                    continue;

                AddOrMerge(result, argument);
            }
        }

        foreach (string argument in _defaultArguments)
        {
            AddOrMerge(result, argument);
        }

        if (options.IncludeNoSandboxArgument)
            AddOrMerge(result, "--no-sandbox");

        if (options.AdditionalArguments is not null)
        {
            foreach (string argument in options.AdditionalArguments.Where(static arg => !string.IsNullOrWhiteSpace(arg)))
            {
                AddOrMerge(result, argument);
            }
        }

        return [.. result];
    }

    private static bool ShouldRemove(string argument)
    {
        string key = GetArgumentKey(argument);
        return _detectableArguments.Contains(key);
    }

    private static void AddOrMerge(List<string> arguments, string argument)
    {
        if (argument.StartsWith("--disable-blink-features=", StringComparison.OrdinalIgnoreCase))
        {
            MergeBlinkFeatures(arguments, argument);
            return;
        }

        string key = GetArgumentKey(argument);
        int existingIndex = arguments.FindIndex(existing => string.Equals(GetArgumentKey(existing), key, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
            arguments[existingIndex] = argument;
        else
            arguments.Add(argument);
    }

    private static void MergeBlinkFeatures(List<string> arguments, string argument)
    {
        const string prefix = "--disable-blink-features=";
        string[] incoming = argument[prefix.Length..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        int index = arguments.FindIndex(static existing => existing.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            arguments.Add(argument);
            return;
        }

        string[] current = arguments[index][prefix.Length..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var merged = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);

        foreach (string feature in incoming)
        {
            merged.Add(feature);
        }

        arguments[index] = $"{prefix}{string.Join(',', merged)}";
    }

    private static string GetArgumentKey(string argument)
    {
        int separatorIndex = argument.IndexOf('=');
        return separatorIndex >= 0 ? argument[..separatorIndex] : argument;
    }
}

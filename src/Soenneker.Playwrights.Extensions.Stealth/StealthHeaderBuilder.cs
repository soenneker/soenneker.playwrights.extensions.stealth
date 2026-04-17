using System;
using System.Collections.Generic;
using Soenneker.Extensions.String;
using Soenneker.Playwrights.Extensions.Stealth.Options;

namespace Soenneker.Playwrights.Extensions.Stealth;

public static class StealthHeaderBuilder
{
    public static string BuildUserAgent(HardwareProfile profile)
    {
        if (profile.UserAgentOverride.HasContent())
            return profile.UserAgentOverride;

        return $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) " + $"AppleWebKit/537.36 (KHTML, like Gecko) " +
               $"Chrome/{BuildReducedChromiumVersion(profile)} Safari/537.36";
    }

    public static Dictionary<string, string> BuildContextHeaders(HardwareProfile profile, StealthContextOptions? options = null)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["accept-language"] = BuildAcceptLanguage(profile),
            ["user-agent"] = BuildUserAgent(profile)
        };

        if (options?.InjectClientHintHeaders == true)
            ApplyClientHintHeaders(headers, profile);

        if (options?.AdditionalHttpHeaders is not null)
        {
            foreach ((string key, string value) in options.AdditionalHttpHeaders)
            {
                headers[key] = value;
            }
        }

        return headers;
    }

    public static Dictionary<string, object> BuildUserAgentOverrideParameters(HardwareProfile profile)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["userAgent"] = BuildUserAgent(profile),
            ["acceptLanguage"] = BuildAcceptLanguage(profile),
            ["platform"] = profile.Platform,
            ["userAgentMetadata"] = BuildUserAgentMetadata(profile)
        };
    }

    public static Dictionary<string, string> BuildDocumentHeaders(HardwareProfile profile, IReadOnlyDictionary<string, string> requestHeaders,
        string requestUrl, StealthContextOptions? options = null)
    {
        var headers = new Dictionary<string, string>(requestHeaders, StringComparer.OrdinalIgnoreCase)
        {
            ["accept"] =
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
            ["accept-language"] = BuildAcceptLanguage(profile),
            ["user-agent"] = BuildUserAgent(profile),
            ["upgrade-insecure-requests"] = "1",
            ["sec-fetch-dest"] = "document",
            ["sec-fetch-mode"] = "navigate",
            ["sec-fetch-user"] = "?1"
        };

        if (options?.InjectClientHintHeaders == true)
            ApplyClientHintHeaders(headers, profile);

        headers["sec-fetch-site"] = DetermineFetchSite(headers, requestUrl);

        return headers;
    }

    private static string BuildAcceptLanguage(HardwareProfile profile)
    {
        if (profile.Languages.Length > 1)
            return $"{profile.Languages[0]},{profile.Languages[1]};q=0.9";

        return profile.Languages.Length == 1 ? profile.Languages[0] : profile.Locale;
    }

    private static string BuildBrandsHeader(HardwareProfile profile)
    {
        return $"\"Google Chrome\";v=\"{profile.ChromeMajorVersion}\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"{profile.ChromeMajorVersion}\"";
    }

    private static string BuildFullVersionBrandsHeader(HardwareProfile profile)
    {
        return $"\"Google Chrome\";v=\"{profile.ChromeVersion}\", " +
               $"\"Not.A/Brand\";v=\"8.0.0.0\", " +
               $"\"Chromium\";v=\"{profile.ChromeVersion}\"";
    }

    private static string BuildReducedChromiumVersion(HardwareProfile profile)
    {
        return $"{profile.ChromeMajorVersion}.0.0.0";
    }

    private static Dictionary<string, object> BuildUserAgentMetadata(HardwareProfile profile)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["brands"] = BuildBrandVersionList(profile, fullVersion: false),
            ["fullVersionList"] = BuildBrandVersionList(profile, fullVersion: true),
            ["fullVersion"] = profile.ChromeVersion,
            ["platform"] = profile.OsPlatform,
            ["platformVersion"] = profile.OsPlatformVersion,
            ["architecture"] = profile.Architecture,
            ["model"] = profile.DeviceModel,
            ["mobile"] = profile.IsMobile,
            ["bitness"] = profile.Bitness,
            ["wow64"] = false
        };
    }

    private static List<Dictionary<string, string>> BuildBrandVersionList(HardwareProfile profile, bool fullVersion)
    {
        string chromeVersion = fullVersion ? profile.ChromeVersion : profile.ChromeMajorVersion.ToString();
        string notABrandVersion = fullVersion ? "8.0.0.0" : "8";

        return
        [
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["brand"] = "Google Chrome",
                ["version"] = chromeVersion
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["brand"] = "Not.A/Brand",
                ["version"] = notABrandVersion
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["brand"] = "Chromium",
                ["version"] = chromeVersion
            }
        ];
    }

    private static void ApplyClientHintHeaders(IDictionary<string, string> headers, HardwareProfile profile)
    {
        headers["sec-ch-ua"] = BuildBrandsHeader(profile);
        headers["sec-ch-ua-full-version"] = $"\"{profile.ChromeVersion}\"";
        headers["sec-ch-ua-full-version-list"] = BuildFullVersionBrandsHeader(profile);
        headers["sec-ch-ua-mobile"] = profile.IsMobile ? "?1" : "?0";
        headers["sec-ch-ua-platform"] = $"\"{profile.OsPlatform}\"";
        headers["sec-ch-ua-platform-version"] = $"\"{profile.OsPlatformVersion}\"";
        headers["sec-ch-ua-arch"] = $"\"{profile.Architecture}\"";
        headers["sec-ch-ua-bitness"] = $"\"{profile.Bitness}\"";
        headers["sec-ch-ua-wow64"] = "?0";
        headers["sec-ch-ua-model"] = $"\"{profile.DeviceModel}\"";
        headers["sec-ch-prefers-color-scheme"] = profile.PrefersDarkMode ? "dark" : "light";
    }

    private static string DetermineFetchSite(IReadOnlyDictionary<string, string> headers, string requestUrl)
    {
        if (!headers.TryGetValue("referer", out string? referer) || string.IsNullOrWhiteSpace(referer))
            return "none";

        if (!Uri.TryCreate(referer, UriKind.Absolute, out Uri? refererUri))
            return "cross-site";

        if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri? requestUri))
            return "cross-site";

        if (string.Equals(refererUri.Host, requestUri.Host, StringComparison.OrdinalIgnoreCase))
            return string.Equals(refererUri.Scheme, requestUri.Scheme, StringComparison.OrdinalIgnoreCase) ? "same-origin" : "same-site";

        return string.Equals(refererUri.GetLeftPart(UriPartial.Authority), requestUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)
            ? "same-origin"
            : "cross-site";
    }
}
using System;
using System.Collections.Generic;
using Soenneker.Playwrights.Extensions.Stealth.Options;

namespace Soenneker.Playwrights.Extensions.Stealth;

internal static class StealthHeaderBuilder
{
    public static string BuildUserAgent(HardwareProfile profile)
    {
        return
            $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            $"AppleWebKit/537.36 (KHTML, like Gecko) " +
            $"Chrome/{profile.ChromeVersion} Safari/537.36";
    }

    public static Dictionary<string, string> BuildContextHeaders(HardwareProfile profile, StealthContextOptions? options = null)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Accept-Language"] = BuildAcceptLanguage(profile),
            ["User-Agent"] = BuildUserAgent(profile)
        };

        if (options?.InjectClientHintHeaders == true)
        {
            headers["Sec-CH-UA"] = BuildBrandsHeader(profile);
            headers["Sec-CH-UA-Full-Version"] = $"\"{profile.ChromeVersion}\"";
            headers["Sec-CH-UA-Full-Version-List"] = BuildFullVersionBrandsHeader(profile);
            headers["Sec-CH-UA-Mobile"] = "?0";
            headers["Sec-CH-UA-Platform"] = $"\"{profile.OsPlatform}\"";
            headers["Sec-CH-UA-Platform-Version"] = $"\"{profile.OsPlatformVersion}\"";
            headers["Sec-CH-UA-Arch"] = $"\"{profile.Architecture}\"";
            headers["Sec-CH-UA-Bitness"] = $"\"{profile.Bitness}\"";
            headers["Sec-CH-UA-WoW64"] = "?0";
            headers["Sec-CH-UA-Model"] = "\"\"";
            headers["Sec-CH-Prefers-Color-Scheme"] = profile.PrefersDarkMode ? "dark" : "light";
        }

        if (options?.AdditionalHttpHeaders is not null)
        {
            foreach ((string key, string value) in options.AdditionalHttpHeaders)
            {
                headers[key] = value;
            }
        }

        return headers;
    }

    public static Dictionary<string, string> BuildDocumentHeaders(HardwareProfile profile, IReadOnlyDictionary<string, string> requestHeaders, string requestUrl)
    {
        var headers = new Dictionary<string, string>(requestHeaders, StringComparer.OrdinalIgnoreCase)
        {
            ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
            ["Accept-Language"] = BuildAcceptLanguage(profile),
            ["Upgrade-Insecure-Requests"] = "1",
            ["Sec-Fetch-Dest"] = "document",
            ["Sec-Fetch-Mode"] = "navigate",
            ["Sec-Fetch-User"] = "?1"
        };

        headers["Sec-Fetch-Site"] = DetermineFetchSite(headers, requestUrl);

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
        return $"\"Not(A:Brand\";v=\"8\", \"Chromium\";v=\"{profile.ChromeMajorVersion}\", \"Google Chrome\";v=\"{profile.ChromeMajorVersion}\"";
    }

    private static string BuildFullVersionBrandsHeader(HardwareProfile profile)
    {
        return
            $"\"Not(A:Brand\";v=\"8.0.0.0\", " +
            $"\"Chromium\";v=\"{profile.ChromeVersion}\", " +
            $"\"Google Chrome\";v=\"{profile.ChromeVersion}\"";
    }

    private static string DetermineFetchSite(IReadOnlyDictionary<string, string> headers, string requestUrl)
    {
        if (!headers.TryGetValue("Referer", out string? referer) || string.IsNullOrWhiteSpace(referer))
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

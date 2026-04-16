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
        {
            headers["sec-ch-ua"] = BuildBrandsHeader(profile);
            headers["sec-ch-ua-full-version"] = $"\"{profile.ChromeVersion}\"";
            headers["sec-ch-ua-full-version-list"] = BuildFullVersionBrandsHeader(profile);
            headers["sec-ch-ua-mobile"] = "?0";
            headers["sec-ch-ua-platform"] = $"\"{profile.OsPlatform}\"";
            headers["sec-ch-ua-platform-version"] = $"\"{profile.OsPlatformVersion}\"";
            headers["sec-ch-ua-arch"] = $"\"{profile.Architecture}\"";
            headers["sec-ch-ua-bitness"] = $"\"{profile.Bitness}\"";
            headers["sec-ch-ua-wow64"] = "?0";
            headers["sec-ch-ua-model"] = "\"\"";
            headers["sec-ch-prefers-color-scheme"] = profile.PrefersDarkMode ? "dark" : "light";
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

    public static Dictionary<string, string> BuildDocumentHeaders(HardwareProfile profile, IReadOnlyDictionary<string, string> requestHeaders,
        string requestUrl)
    {
        var headers = new Dictionary<string, string>(requestHeaders, StringComparer.OrdinalIgnoreCase)
        {
            ["accept"] =
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
            ["accept-language"] = BuildAcceptLanguage(profile),
            ["upgrade-insecure-requests"] = "1",
            ["sec-fetch-dest"] = "document",
            ["sec-fetch-mode"] = "navigate",
            ["sec-fetch-user"] = "?1"
        };

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
        return $"\"Not(A:Brand\";v=\"8\", \"Chromium\";v=\"{profile.ChromeMajorVersion}\", \"Google Chrome\";v=\"{profile.ChromeMajorVersion}\"";
    }

    private static string BuildFullVersionBrandsHeader(HardwareProfile profile)
    {
        return $"\"Not(A:Brand\";v=\"8.0.0.0\", " + $"\"Chromium\";v=\"{profile.ChromeVersion}\", " + $"\"Google Chrome\";v=\"{profile.ChromeVersion}\"";
    }

    private static string BuildReducedChromiumVersion(HardwareProfile profile)
    {
        return $"{profile.ChromeMajorVersion}.0.0.0";
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
using System.Globalization;
using System.Text;
using System.Text.Json;
using Soenneker.Playwrights.Extensions.Stealth.Dtos;
using Soenneker.Playwrights.Extensions.Stealth.Options;

namespace Soenneker.Playwrights.Extensions.Stealth;

internal static class StealthScriptBuilder
{
    public static string Build(HardwareProfile profile, StealthContextOptions? options = null)
    {
        options ??= new StealthContextOptions();

        string profileJson = JsonSerializer.Serialize(new
        {
            seed = profile.Seed,
            userAgent = StealthHeaderBuilder.BuildUserAgent(profile),
            languages = profile.Languages,
            locale = profile.Locale,
            timezone = profile.TimeZone,
            platform = profile.Platform,
            browserVendor = profile.BrowserVendor,
            hardwareConcurrency = profile.Cores,
            deviceMemory = profile.MemoryGb,
            screenWidth = profile.ScreenW,
            screenHeight = profile.ScreenH,
            devicePixelRatio = profile.DevicePixelRatio,
            maxTouchPoints = profile.MaxTouchPoints,
            prefersDarkMode = profile.PrefersDarkMode,
            chromeVersion = profile.ChromeVersion,
            chromeMajorVersion = profile.ChromeMajorVersion,
            isMobile = profile.IsMobile,
            deviceModel = profile.DeviceModel,
            osPlatform = profile.OsPlatform,
            osPlatformVersion = profile.OsPlatformVersion,
            architecture = profile.Architecture,
            bitness = profile.Bitness,
            latitude = profile.Latitude.ToString("F5", CultureInfo.InvariantCulture),
            longitude = profile.Longitude.ToString("F5", CultureInfo.InvariantCulture),
            webGlVendor = profile.WebGlVendor,
            webGlRenderer = profile.WebGlRenderer,
            colorDepth = profile.ColorDepth,
            pixelDepth = profile.PixelDepth
        });

        var script = new StringBuilder();
        script.AppendLine("(() => {");
        script.Append("const profile = ");
        script.Append(profileJson);
        script.AppendLine(";");
        script.AppendLine("if (globalThis.__soennekerStealthApplied) return;");
        script.AppendLine("Object.defineProperty(globalThis, '__soennekerStealthApplied', { value: true, configurable: false, enumerable: false });");

        AppendFoundation(script);
        AppendNavigatorModule(script, options);
        AppendPluginModule(script);
        AppendChromeModule(script);
        AppendSpeechModule(script, options);
        AppendPermissionModule(script, options);
        AppendWindowAndScreenModule(script);
        AppendMediaModule(script, options);
        AppendIntlModule(script, options);
        AppendCanvasModule(script, options);
        AppendWebGlModule(script, options);
        AppendWebRtcModule(script);

        script.AppendLine("})();");

        return script.ToString();
    }

    private static void AppendFoundation(StringBuilder script)
    {
        script.AppendLine(
            """
            let seed = profile.seed;
            const rand = () => {
              const x = Math.sin(seed++) * 10000;
              return x - Math.floor(x);
            };
            const patchGetter = (target, property, getter) => {
              try {
                Object.defineProperty(target, property, {
                  configurable: true,
                  enumerable: true,
                  get: getter
                });
              } catch {}
            };
            const patchValue = (target, property, value) => {
              try {
                Object.defineProperty(target, property, {
                  configurable: true,
                  enumerable: true,
                  writable: true,
                  value
                });
              } catch {}
            };
            const createArrayLike = (items, key) => {
              const array = [];
              items.forEach((item, index) => {
                array[index] = item;
                if (item[key]) array[item[key]] = item;
              });
              patchValue(array, 'item', function item(index) {
                return this[index] ?? null;
              });
              patchValue(array, 'namedItem', function namedItem(name) {
                return this[name] ?? null;
              });
              patchValue(array, 'refresh', function refresh() {});
              return array;
            };
            """
        );
    }

    private static void AppendNavigatorModule(StringBuilder script, StealthContextOptions options)
    {
        script.AppendLine(
            """
            patchGetter(Navigator.prototype, 'webdriver', () => undefined);
            patchGetter(Navigator.prototype, 'hardwareConcurrency', () => profile.hardwareConcurrency);
            patchGetter(Navigator.prototype, 'deviceMemory', () => profile.deviceMemory);
            patchGetter(Navigator.prototype, 'platform', () => profile.platform);
            patchGetter(Navigator.prototype, 'vendor', () => profile.browserVendor);
            patchGetter(Navigator.prototype, 'language', () => profile.locale);
            patchGetter(Navigator.prototype, 'languages', () => [...profile.languages]);
            patchGetter(Navigator.prototype, 'maxTouchPoints', () => profile.maxTouchPoints);
            patchGetter(Navigator.prototype, 'pdfViewerEnabled', () => true);
            patchGetter(Navigator.prototype, 'userAgent', () => profile.userAgent);

            const brands = [
              { brand: 'Google Chrome', version: String(profile.chromeMajorVersion) },
              { brand: 'Not.A/Brand', version: '8' },
              { brand: 'Chromium', version: String(profile.chromeMajorVersion) }
            ];

            const fullVersionList = [
              { brand: 'Google Chrome', version: profile.chromeVersion },
              { brand: 'Not.A/Brand', version: '8.0.0.0' },
              { brand: 'Chromium', version: profile.chromeVersion }
            ];
            """
        );

        switch (options.Surfaces.UserAgentData)
        {
            case StealthSurfaceMode.Spoofed:
                script.AppendLine(
                    """

                    const uaData = {
                      brands,
                      mobile: profile.isMobile,
                      platform: profile.osPlatform,
                      getHighEntropyValues: async hints => {
                        const values = {
                          architecture: profile.architecture,
                          bitness: profile.bitness,
                          mobile: profile.isMobile,
                          model: profile.deviceModel,
                          platform: profile.osPlatform,
                          platformVersion: profile.osPlatformVersion,
                          uaFullVersion: profile.chromeVersion,
                          fullVersionList,
                          wow64: false
                        };

                        return Object.fromEntries(hints.map(hint => [hint, values[hint] ?? null]));
                      },
                      toJSON: () => ({
                        brands,
                        mobile: profile.isMobile,
                        platform: profile.osPlatform
                      })
                    };

                    patchGetter(Navigator.prototype, 'userAgentData', () => uaData);
                    """
                );
                break;
            case StealthSurfaceMode.Disabled:
                script.AppendLine(
                    """

                    patchGetter(Navigator.prototype, 'userAgentData', () => undefined);
                    """
                );
                break;
        }
    }

    private static void AppendPluginModule(StringBuilder script)
    {
        script.AppendLine(
            """
            const mimeTypes = createArrayLike([
              {
                type: 'application/pdf',
                suffixes: 'pdf',
                description: 'Portable Document Format'
              },
              {
                type: 'text/pdf',
                suffixes: 'pdf',
                description: 'Portable Document Format'
              }
            ], 'type');

            const plugins = createArrayLike([
              {
                name: 'Chrome PDF Viewer',
                filename: 'internal-pdf-viewer',
                description: 'Portable Document Format',
                length: mimeTypes.length,
                0: mimeTypes[0]
              },
              {
                name: 'Chromium PDF Viewer',
                filename: 'internal-pdf-viewer',
                description: 'Portable Document Format',
                length: mimeTypes.length,
                0: mimeTypes[0]
              }
            ], 'name');

            patchGetter(Navigator.prototype, 'mimeTypes', () => mimeTypes);
            patchGetter(Navigator.prototype, 'plugins', () => plugins);
            """
        );
    }

    private static void AppendChromeModule(StringBuilder script)
    {
        script.AppendLine(
            """
            if (!window.chrome) {
              patchValue(window, 'chrome', {});
            }

            if (window.chrome) {
              window.chrome.runtime ??= {};
              window.chrome.app ??= {
                InstallState: {
                  DISABLED: 'disabled',
                  INSTALLED: 'installed',
                  NOT_INSTALLED: 'not_installed'
                },
                RunningState: {
                  CANNOT_RUN: 'cannot_run',
                  READY_TO_RUN: 'ready_to_run',
                  RUNNING: 'running'
                }
              };
              window.chrome.webstore ??= {
                onInstallStageChanged: { addListener() {} },
                onDownloadProgress: { addListener() {} }
              };
              window.chrome.csi ??= () => ({
                onloadT: Date.now(),
                startE: Date.now() - Math.round(50 + rand() * 100),
                pageT: Math.round(100 + rand() * 200),
                tran: 15
              });
              window.chrome.loadTimes ??= () => ({
                requestTime: (Date.now() / 1000) - rand(),
                startLoadTime: (Date.now() / 1000) - rand(),
                commitLoadTime: (Date.now() / 1000) - rand() / 2,
                finishDocumentLoadTime: (Date.now() / 1000) - rand() / 3,
                finishLoadTime: (Date.now() / 1000),
                firstPaintTime: (Date.now() / 1000),
                firstPaintAfterLoadTime: 0,
                navigationType: 'Other',
                wasFetchedViaSpdy: true,
                wasNpnNegotiated: true,
                npnNegotiatedProtocol: 'h2',
                wasAlternateProtocolAvailable: false,
                connectionInfo: 'h2'
              });
            }
            """
        );
    }

    private static void AppendSpeechModule(StringBuilder script, StealthContextOptions options)
    {
        if (!options.WarmupSpeechVoices)
            return;

        script.AppendLine(
            """
            if (typeof SpeechSynthesis !== 'undefined' && SpeechSynthesis.prototype && typeof speechSynthesis !== 'undefined' && typeof speechSynthesis.getVoices === 'function') {
              const speechProto = SpeechSynthesis.prototype;
              const nativeGetVoices = speechProto.getVoices;
              patchValue(speechProto, 'getVoices', function() {
                const voices = nativeGetVoices.call(speechSynthesis);
                if (voices.some(v => v && /^google/i.test(v.name)))
                  return voices;
                return [...voices, {
                  voiceURI: 'Google US English',
                  name: 'Google US English',
                  lang: 'en-US',
                  localService: false,
                  default: false
                }];
              });
            }

            if (typeof speechSynthesis !== 'undefined' && typeof speechSynthesis.getVoices === 'function') {
              const synth = speechSynthesis;
              let attempts = 0;

              const warmupVoices = () => {
                try {
                  const voices = synth.getVoices();
                  if (Array.isArray(voices) && voices.length > 0) {
                    try { synth.removeEventListener?.('voiceschanged', warmupVoices); } catch {}
                    return true;
                  }
                } catch {}

                attempts += 1;
                return false;
              };

              warmupVoices();

              try {
                synth.addEventListener?.('voiceschanged', warmupVoices, { once: true });
              } catch {}

              for (let i = 1; i <= 4; i += 1) {
                setTimeout(() => {
                  if (attempts < 6) {
                    warmupVoices();
                  }
                }, i * 50);
              }
            }
            """
        );
    }

    private static void AppendPermissionModule(StringBuilder script, StealthContextOptions options)
    {
        switch (options.Surfaces.PermissionsQuery)
        {
            case StealthSurfaceMode.Spoofed:
                script.AppendLine(
                    """
                    if (navigator.permissions?.query) {
                      const originalQuery = navigator.permissions.query.bind(navigator.permissions);

                      patchValue(navigator.permissions, 'query', async parameters => {
                        const delay = 20 + Math.round(rand() * 35);
                        await new Promise(resolve => setTimeout(resolve, delay));

                        if (parameters?.name === 'notifications') {
                          return {
                            state: Notification.permission,
                            onchange: null
                          };
                        }

                        return originalQuery(parameters);
                      });
                    }
                    """
                );
                break;
            case StealthSurfaceMode.Disabled:
                script.AppendLine(
                    """
                    if (navigator.permissions) {
                      patchValue(navigator.permissions, 'query', async () => {
                        throw new DOMException('The operation is not supported.', 'NotSupportedError');
                      });
                    }
                    """
                );
                break;
        }

        script.AppendLine(
            """
            if (window.matchMedia) {
              const originalMatchMedia = window.matchMedia.bind(window);
              patchValue(window, 'matchMedia', query => {
                if (query === '(prefers-color-scheme: dark)' || query === '(prefers-color-scheme: light)') {
                  return {
                    matches: query.includes(profile.prefersDarkMode ? 'dark' : 'light'),
                    media: query,
                    onchange: null,
                    addListener() {},
                    removeListener() {},
                    addEventListener() {},
                    removeEventListener() {},
                    dispatchEvent() { return false; }
                  };
                }

                return originalMatchMedia(query);
              });
            }
            """
        );
    }

    private static void AppendWindowAndScreenModule(StringBuilder script)
    {
        script.AppendLine(
            """
            patchGetter(window, 'devicePixelRatio', () => profile.devicePixelRatio);
            patchGetter(window, 'outerWidth', () => Math.round(profile.screenWidth + (80 * profile.devicePixelRatio)));
            patchGetter(window, 'outerHeight', () => Math.round(profile.screenHeight + (88 * profile.devicePixelRatio)));

            if (window.screen) {
              patchGetter(window.screen, 'width', () => profile.screenWidth);
              patchGetter(window.screen, 'height', () => profile.screenHeight);
              patchGetter(window.screen, 'availWidth', () => profile.screenWidth);
              patchGetter(window.screen, 'availHeight', () => profile.screenHeight - 40);
              patchGetter(window.screen, 'colorDepth', () => profile.colorDepth);
              patchGetter(window.screen, 'pixelDepth', () => profile.pixelDepth);
            }
            """
        );
    }

    private static void AppendMediaModule(StringBuilder script, StealthContextOptions options)
    {
        script.AppendLine(
            """
            patchGetter(Navigator.prototype, 'connection', () => ({
              downlink: 10 + Math.round(rand() * 30),
              downlinkMax: 100,
              effectiveType: '4g',
              rtt: 35 + Math.round(rand() * 120),
              saveData: false,
              type: 'wifi'
            }));

            patchValue(Navigator.prototype, 'getBattery', () => Promise.resolve({
              charging: true,
              chargingTime: 0,
              dischargingTime: Infinity,
              level: 0.76 + (rand() * 0.1),
              onchargingchange: null,
              onlevelchange: null,
              onchargingtimechange: null,
              ondischargingtimechange: null
            }));

            """
        );

        switch (options.Surfaces.MediaDevices)
        {
            case StealthSurfaceMode.Spoofed:
                script.AppendLine(
                    """
                    if (navigator.mediaDevices?.enumerateDevices) {
                      patchValue(navigator.mediaDevices, 'enumerateDevices', async () => [
                        { deviceId: 'default', groupId: 'audio-input', kind: 'audioinput', label: 'Microphone Array (Realtek(R) Audio)' },
                        { deviceId: 'default', groupId: 'audio-output', kind: 'audiooutput', label: 'Speakers (Realtek(R) Audio)' },
                        { deviceId: 'default', groupId: 'video-input', kind: 'videoinput', label: 'Integrated Camera' }
                      ]);
                    }
                    """
                );
                break;
            case StealthSurfaceMode.Disabled:
                script.AppendLine(
                    """
                    if (navigator.mediaDevices?.enumerateDevices) {
                      patchValue(navigator.mediaDevices, 'enumerateDevices', async () => []);
                    }
                    """
                );
                break;
        }
    }

    private static void AppendIntlModule(StringBuilder script, StealthContextOptions options)
    {
        switch (options.Surfaces.DocumentFonts)
        {
            case StealthSurfaceMode.Spoofed:
                script.AppendLine(
                    """
                    if (document.fonts && window.FontFaceSet) {
                      const originalFonts = document.fonts;
                      const extraFonts = ['Arial', 'Calibri', 'Courier New', 'Segoe UI', 'Times New Roman'];
                      const proxy = new Proxy(originalFonts, {
                        get(target, property, receiver) {
                          if (property === 'size')
                            return target.size + extraFonts.length;

                          if (property === 'values') {
                            return () => [
                              ...target.values(),
                              ...extraFonts.map(font => new FontFace(font, 'local("' + font + '")'))
                            ];
                          }

                          return Reflect.get(target, property, receiver);
                        }
                      });

                      patchValue(document, 'fonts', proxy);
                    }
                    """
                );
                break;
            case StealthSurfaceMode.Disabled:
                script.AppendLine(
                    """
                    patchGetter(document, 'fonts', () => undefined);
                    """
                );
                break;
        }
    }

    private static void AppendCanvasModule(StringBuilder script, StealthContextOptions options)
    {
        switch (options.Surfaces.Canvas)
        {
            case StealthSurfaceMode.Spoofed:
                script.AppendLine(
                    """
                    const originalToDataURL = HTMLCanvasElement.prototype.toDataURL;
                    patchValue(HTMLCanvasElement.prototype, 'toDataURL', function(...args) {
                      const context = this.getContext('2d');

                      if (context) {
                        try {
                          const shift = 0.00015 + (rand() * 0.0002);
                          context.globalAlpha = Math.max(0.9985, 1 - shift);
                        } catch {}
                      }

                      return originalToDataURL.apply(this, args);
                    });
                    """
                );
                break;
            case StealthSurfaceMode.Disabled:
                script.AppendLine(
                    """
                    patchValue(HTMLCanvasElement.prototype, 'toDataURL', function() {
                      return 'data:,';
                    });
                    """
                );
                break;
        }
    }

    private static void AppendWebGlModule(StringBuilder script, StealthContextOptions options)
    {
        if (options.Surfaces.WebGl != StealthSurfaceMode.Spoofed)
            return;

        script.AppendLine(
            """
            const patchWebGl = prototype => {
              if (!prototype?.getParameter)
                return;

              const originalGetParameter = prototype.getParameter;
              patchValue(prototype, 'getParameter', function(parameter) {
                if (parameter === 37445)
                  return profile.webGlVendor;

                if (parameter === 37446)
                  return profile.webGlRenderer;

                return originalGetParameter.call(this, parameter);
              });
            };

            patchWebGl(window.WebGLRenderingContext?.prototype);
            patchWebGl(window.WebGL2RenderingContext?.prototype);
            """
        );
    }

    private static void AppendWebRtcModule(StringBuilder script)
    {
        script.AppendLine(
            """
            if (window.RTCPeerConnection?.prototype) {
              const peerConnectionPrototype = window.RTCPeerConnection.prototype;
              const stripCandidates = sdp => typeof sdp === 'string'
                ? sdp.replace(/^a=candidate:.* typ host .*$/gmi, '')
                : sdp;

              ['createOffer', 'createAnswer'].forEach(methodName => {
                const originalMethod = peerConnectionPrototype[methodName];
                if (typeof originalMethod !== 'function')
                  return;

                patchValue(peerConnectionPrototype, methodName, async function(...args) {
                  const description = await originalMethod.apply(this, args);
                  if (description?.sdp)
                    return { type: description.type, sdp: stripCandidates(description.sdp) };

                  return description;
                });
              });

              if (peerConnectionPrototype.setLocalDescription) {
                const originalSetLocalDescription = peerConnectionPrototype.setLocalDescription;
                patchValue(peerConnectionPrototype, 'setLocalDescription', function(description) {
                  if (description?.sdp)
                    description = { ...description, sdp: stripCandidates(description.sdp) };

                  return originalSetLocalDescription.call(this, description);
                });
              }
            }
            """
        );
    }
}

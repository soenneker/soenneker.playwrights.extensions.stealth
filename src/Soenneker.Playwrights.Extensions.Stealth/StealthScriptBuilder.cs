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
        AppendDevToolsModule(script);
        AppendWorkerModule(script, options);
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

            const nativeToString = Function.prototype.toString;
            const nativeFunctionSources = new WeakMap();
            const nativeFunctionString = name => `function${name ? ' ' + name : ''}() { [native code] }`;
            const markNative = (fn, name) => {
              if (typeof fn !== 'function')
                return fn;

              try {
                Object.defineProperty(fn, 'name', { value: name, configurable: true });
              } catch {}

              nativeFunctionSources.set(fn, nativeFunctionString(name));
              return fn;
            };
            const createNativeFunction = (name, handler) => {
              const container = {
                [name](...args) {
                  return handler.apply(this, args);
                }
              };

              return markNative(container[name], name);
            };
            const createNativeGetter = (property, getter) => {
              const container = {
                get [property]() {
                  return getter.call(this);
                }
              };

              return markNative(Object.getOwnPropertyDescriptor(container, property).get, `get ${String(property)}`);
            };
            const functionToString = createNativeFunction('toString', function() {
              if (nativeFunctionSources.has(this))
                return nativeFunctionSources.get(this);

              return nativeToString.call(this);
            });

            try {
              const descriptor = Object.getOwnPropertyDescriptor(Function.prototype, 'toString');
              Object.defineProperty(Function.prototype, 'toString', {
                configurable: descriptor?.configurable ?? true,
                enumerable: descriptor?.enumerable ?? false,
                writable: descriptor?.writable ?? true,
                value: functionToString
              });
            } catch {}

            const getDescriptor = (target, property) => {
              let current = target;

              while (current) {
                const descriptor = Object.getOwnPropertyDescriptor(current, property);
                if (descriptor)
                  return descriptor;

                current = Object.getPrototypeOf(current);
              }

              return undefined;
            };
            const sameValue = (left, right) => {
              if (Array.isArray(left) && Array.isArray(right))
                return left.length === right.length && left.every((value, index) => Object.is(value, right[index]));

              return Object.is(left, right);
            };
            const patchGetter = (target, property, getter, overrides = {}) => {
              try {
                const descriptor = getDescriptor(target, property);
                Object.defineProperty(target, property, {
                  configurable: overrides.configurable ?? descriptor?.configurable ?? true,
                  enumerable: overrides.enumerable ?? descriptor?.enumerable ?? true,
                  get: createNativeGetter(property, getter)
                });
              } catch {}
            };
            const patchGetterIfNeeded = (target, receiver, property, getter, overrides = {}) => {
              try {
                const actual = Reflect.get(receiver, property);
                const desired = getter.call(receiver);

                if (sameValue(actual, desired))
                  return;
              } catch {}

              patchGetter(target, property, getter, overrides);
            };
            const patchValue = (target, property, value, overrides = {}) => {
              try {
                const descriptor = getDescriptor(target, property);
                const patchedValue = typeof value === 'function'
                  ? createNativeFunction(overrides.name ?? String(property), value)
                  : value;

                Object.defineProperty(target, property, {
                  configurable: overrides.configurable ?? descriptor?.configurable ?? true,
                  enumerable: overrides.enumerable ?? descriptor?.enumerable ?? true,
                  writable: overrides.writable ?? descriptor?.writable ?? true,
                  value: patchedValue
                });
              } catch {}
            };
            const setNativePrototype = (value, prototypeName, tag) => {
              try {
                const prototype = window[prototypeName]?.prototype;
                if (prototype)
                  Object.setPrototypeOf(value, prototype);
              } catch {}

              try {
                Object.defineProperty(value, Symbol.toStringTag, {
                  configurable: true,
                  enumerable: false,
                  value: tag
                });
              } catch {}

              return value;
            };
            const createArrayLike = (items, key, prototypeName, tag) => {
              const array = {};
              items.forEach((item, index) => {
                array[index] = item;
                if (item[key]) array[item[key]] = item;
              });
              patchValue(array, 'length', items.length, { enumerable: false, writable: false });
              patchValue(array, 'item', function item(index) {
                return this[index] ?? null;
              });
              patchValue(array, 'namedItem', function namedItem(name) {
                return this[name] ?? null;
              });
              patchValue(array, 'refresh', function refresh() {});
              patchValue(array, Symbol.iterator, function iterator() {
                let index = 0;
                return {
                  next: () => index < items.length
                    ? { value: this[index++], done: false }
                    : { value: undefined, done: true }
                };
              }, { name: 'values', enumerable: false });
              return setNativePrototype(array, prototypeName, tag);
            };
            """
        );
    }

    private static void AppendDevToolsModule(StringBuilder script)
    {
        script.AppendLine(
            """
            if (window.console) {
              const sanitizeConsoleArg = value => {
                const type = typeof value;

                if (value == null || (type !== 'object' && type !== 'function'))
                  return value;

                return type === 'function'
                  ? 'function () { [native code] }'
                  : '[object Object]';
              };
              const patchConsoleForwarder = method => {
                const original = console[method];

                if (typeof original !== 'function')
                  return;

                patchValue(console, method, function(...args) {
                  return original.apply(console, args.map(sanitizeConsoleArg));
                }, { name: method });
              };
              const patchConsoleNoop = method => {
                if (typeof console[method] === 'function') {
                  patchValue(console, method, function() {}, { name: method });
                }
              };

              ['debug', 'error', 'info', 'log', 'warn'].forEach(patchConsoleForwarder);
              ['dir', 'dirxml', 'table', 'trace', 'profile', 'profileEnd', 'timeStamp'].forEach(patchConsoleNoop);

              if (typeof console.assert === 'function') {
                const originalAssert = console.assert;
                patchValue(console, 'assert', function assert(condition, ...args) {
                  if (condition)
                    return undefined;

                  return originalAssert.apply(console, [condition, ...args.map(sanitizeConsoleArg)]);
                });
              }
            }
            """
        );
    }

    private static void AppendWorkerModule(StringBuilder script, StealthContextOptions options)
    {
        script.Append("const patchWorkerWebGl = ");
        script.Append(options.Surfaces.WebGl == StealthSurfaceMode.Spoofed ? "true" : "false");
        script.AppendLine(";");
        script.AppendLine(
            """
            const buildWorkerStealthSource = () => {
              const workerProfileJson = JSON.stringify(profile);
              const workerPatchWebGl = JSON.stringify(patchWorkerWebGl);

              return `(() => {
                const profile = ${workerProfileJson};
                const patchWorkerWebGl = ${workerPatchWebGl};

                if (globalThis.__soennekerWorkerStealthApplied)
                  return;

                try {
                  Object.defineProperty(globalThis, '__soennekerWorkerStealthApplied', { value: true, configurable: false, enumerable: false });
                } catch {}

                const nativeToString = Function.prototype.toString;
                const nativeFunctionSources = new WeakMap();
                const nativeFunctionString = name => 'function' + (name ? ' ' + name : '') + '() { [native code] }';
                const markNative = (fn, name) => {
                  if (typeof fn !== 'function')
                    return fn;

                  try {
                    Object.defineProperty(fn, 'name', { value: name, configurable: true });
                  } catch {}

                  nativeFunctionSources.set(fn, nativeFunctionString(name));
                  return fn;
                };
                const createNativeFunction = (name, handler) => {
                  const container = {
                    [name](...args) {
                      return handler.apply(this, args);
                    }
                  };

                  return markNative(container[name], name);
                };
                const createNativeGetter = (property, getter) => {
                  const container = {
                    get [property]() {
                      return getter.call(this);
                    }
                  };

                  return markNative(Object.getOwnPropertyDescriptor(container, property).get, 'get ' + String(property));
                };
                const functionToString = createNativeFunction('toString', function() {
                  if (nativeFunctionSources.has(this))
                    return nativeFunctionSources.get(this);

                  return nativeToString.call(this);
                });

                try {
                  const descriptor = Object.getOwnPropertyDescriptor(Function.prototype, 'toString');
                  Object.defineProperty(Function.prototype, 'toString', {
                    configurable: descriptor?.configurable ?? true,
                    enumerable: descriptor?.enumerable ?? false,
                    writable: descriptor?.writable ?? true,
                    value: functionToString
                  });
                } catch {}

                const getDescriptor = (target, property) => {
                  let current = target;

                  while (current) {
                    const descriptor = Object.getOwnPropertyDescriptor(current, property);
                    if (descriptor)
                      return descriptor;

                    current = Object.getPrototypeOf(current);
                  }

                  return undefined;
                };
                const sameValue = (left, right) => {
                  if (Array.isArray(left) && Array.isArray(right))
                    return left.length === right.length && left.every((value, index) => Object.is(value, right[index]));

                  return Object.is(left, right);
                };
                const patchGetter = (target, property, getter, overrides = {}) => {
                  try {
                    const descriptor = getDescriptor(target, property);
                    Object.defineProperty(target, property, {
                      configurable: overrides.configurable ?? descriptor?.configurable ?? true,
                      enumerable: overrides.enumerable ?? descriptor?.enumerable ?? true,
                      get: createNativeGetter(property, getter)
                    });
                  } catch {}
                };
                const patchGetterIfNeeded = (target, receiver, property, getter, overrides = {}) => {
                  try {
                    const actual = Reflect.get(receiver, property);
                    const desired = getter.call(receiver);

                    if (sameValue(actual, desired))
                      return;
                  } catch {}

                  patchGetter(target, property, getter, overrides);
                };
                const patchValue = (target, property, value, overrides = {}) => {
                  try {
                    const descriptor = getDescriptor(target, property);
                    const patchedValue = typeof value === 'function'
                      ? createNativeFunction(overrides.name ?? String(property), value)
                      : value;

                    Object.defineProperty(target, property, {
                      configurable: overrides.configurable ?? descriptor?.configurable ?? true,
                      enumerable: overrides.enumerable ?? descriptor?.enumerable ?? true,
                      writable: overrides.writable ?? descriptor?.writable ?? true,
                      value: patchedValue
                    });
                  } catch {}
                };
                const navigatorPrototype = typeof navigator !== 'undefined'
                  ? Object.getPrototypeOf(navigator)
                  : undefined;
                const patchWorkerNavigatorGetter = (property, getter) => {
                  if (navigatorPrototype && typeof navigator !== 'undefined')
                    patchGetterIfNeeded(navigatorPrototype, navigator, property, getter);
                };

                patchWorkerNavigatorGetter('webdriver', () => false);
                patchWorkerNavigatorGetter('hardwareConcurrency', () => profile.hardwareConcurrency);
                patchWorkerNavigatorGetter('deviceMemory', () => profile.deviceMemory);
                patchWorkerNavigatorGetter('platform', () => profile.platform);
                patchWorkerNavigatorGetter('language', () => profile.locale);
                patchWorkerNavigatorGetter('languages', () => [...profile.languages]);
                patchWorkerNavigatorGetter('userAgent', () => profile.userAgent);

                if (typeof navigator !== 'undefined' && 'userAgentData' in navigator) {
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
                  const uaData = {
                    brands,
                    mobile: profile.isMobile,
                    platform: profile.osPlatform,
                    getHighEntropyValues: createNativeFunction('getHighEntropyValues', async hints => {
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
                    }),
                    toJSON: createNativeFunction('toJSON', () => ({
                      brands,
                      mobile: profile.isMobile,
                      platform: profile.osPlatform
                    }))
                  };

                  patchWorkerNavigatorGetter('userAgentData', () => uaData);
                }

                if (patchWorkerWebGl) {
                  const patchWebGl = prototype => {
                    if (!prototype?.getParameter)
                      return;

                    const originalGetParameter = prototype.getParameter;
                    patchValue(prototype, 'getParameter', function getParameter(parameter) {
                      if (parameter === 37445)
                        return profile.webGlVendor;

                      if (parameter === 37446)
                        return profile.webGlRenderer;

                      return originalGetParameter.call(this, parameter);
                    });
                  };

                  patchWebGl(globalThis.WebGLRenderingContext?.prototype);
                  patchWebGl(globalThis.WebGL2RenderingContext?.prototype);
                }

                if (globalThis.console) {
                  const sanitizeConsoleArg = value => {
                    const type = typeof value;

                    if (value == null || (type !== 'object' && type !== 'function'))
                      return value;

                    return type === 'function'
                      ? 'function () { [native code] }'
                      : '[object Object]';
                  };
                  const patchConsoleForwarder = method => {
                    const original = console[method];

                    if (typeof original !== 'function')
                      return;

                    patchValue(console, method, function(...args) {
                      return original.apply(console, args.map(sanitizeConsoleArg));
                    }, { name: method });
                  };
                  const patchConsoleNoop = method => {
                    if (typeof console[method] === 'function')
                      patchValue(console, method, function() {}, { name: method });
                  };

                  ['debug', 'error', 'info', 'log', 'warn'].forEach(patchConsoleForwarder);
                  ['dir', 'dirxml', 'table', 'trace', 'profile', 'profileEnd', 'timeStamp'].forEach(patchConsoleNoop);

                  if (typeof console.assert === 'function') {
                    const originalAssert = console.assert;
                    patchValue(console, 'assert', function assert(condition, ...args) {
                      if (condition)
                        return undefined;

                      return originalAssert.apply(console, [condition, ...args.map(sanitizeConsoleArg)]);
                    });
                  }
                }
              })();
              `;
            };

            if (window.Worker && window.Blob && window.URL?.createObjectURL) {
              const workerStealthSource = buildWorkerStealthSource();
              const NativeBlob = window.Blob;
              const NativeWorker = window.Worker;
              const NativeSharedWorker = window.SharedWorker;
              const NativeCreateObjectURL = window.URL.createObjectURL.bind(window.URL);
              const NativeRevokeObjectURL = window.URL.revokeObjectURL?.bind(window.URL);
              const blobSources = new WeakMap();
              const objectUrlSources = new Map();
              const patchConstructorValue = (target, property, value) => {
                try {
                  const descriptor = getDescriptor(target, property);
                  Object.defineProperty(target, property, {
                    configurable: descriptor?.configurable ?? true,
                    enumerable: descriptor?.enumerable ?? false,
                    writable: descriptor?.writable ?? true,
                    value
                  });
                } catch {}
              };
              const mirrorConstructor = (replacement, nativeConstructor, name) => {
                markNative(replacement, name);

                try {
                  Object.defineProperty(replacement, 'length', { value: nativeConstructor.length, configurable: true });
                } catch {}

                try {
                  Object.setPrototypeOf(replacement, nativeConstructor);
                } catch {}

                try {
                  replacement.prototype = nativeConstructor.prototype;
                } catch {}

                return replacement;
              };
              const isLikelyWorkerScript = (source, type) => {
                const normalizedType = String(type ?? '').toLowerCase();

                if (normalizedType.includes('javascript') || normalizedType.includes('ecmascript'))
                  return true;

                return !normalizedType && /(?:postMessage|onmessage|importScripts|navigator|OffscreenCanvas|addEventListener\s*\(\s*['"]message)/.test(source);
              };
              const getBlobSource = parts => Array.isArray(parts) && parts.every(part => typeof part === 'string' || part instanceof String)
                ? parts.map(part => String(part)).join('')
                : undefined;
              const StealthBlob = mirrorConstructor(function Blob(blobParts = [], options = {}) {
                const blob = new NativeBlob(blobParts, options);
                const source = getBlobSource(blobParts);

                if (source !== undefined && isLikelyWorkerScript(source, options?.type)) {
                  try {
                    blobSources.set(blob, source);
                  } catch {}
                }

                return blob;
              }, NativeBlob, 'Blob');
              const resolveWorkerUrl = scriptUrl => {
                try {
                  return new URL(String(scriptUrl), document.baseURI).href;
                } catch {
                  return String(scriptUrl);
                }
              };
              const createInlineWorkerUrl = (source, options) => NativeCreateObjectURL(new NativeBlob([
                workerStealthSource,
                '\n',
                source
              ], { type: 'text/javascript' }));
              const createImportingWorkerUrl = (scriptUrl, options) => {
                const absoluteUrl = resolveWorkerUrl(scriptUrl);
                const isModule = options?.type === 'module';
                const importSource = isModule
                  ? '\nawait import(' + JSON.stringify(absoluteUrl) + ');\n'
                  : '\ntry { importScripts(' + JSON.stringify(absoluteUrl) + '); } catch (error) { setTimeout(() => { throw error; }); }\n';

                return createInlineWorkerUrl(importSource, options);
              };
              const wrapWorkerUrl = (scriptUrl, options) => {
                const source = String(scriptUrl);

                if (objectUrlSources.has(source))
                  return createInlineWorkerUrl(objectUrlSources.get(source), options);

                if (/^(blob:|data:)/i.test(source))
                  return createImportingWorkerUrl(source, options);

                return scriptUrl;
              };
              const createWorkerConstructor = (nativeConstructor, name) => mirrorConstructor(function(scriptUrl, options) {
                if (!new.target)
                  throw new TypeError("Failed to construct '" + name + "': Please use the 'new' operator.");

                return new nativeConstructor(wrapWorkerUrl(scriptUrl, options), options);
              }, nativeConstructor, name);

              patchConstructorValue(window, 'Blob', StealthBlob);
              patchValue(window.URL, 'createObjectURL', function createObjectURL(value) {
                const objectUrl = NativeCreateObjectURL(value);

                try {
                  if (blobSources.has(value))
                    objectUrlSources.set(objectUrl, blobSources.get(value));
                } catch {}

                return objectUrl;
              });

              if (NativeRevokeObjectURL) {
                patchValue(window.URL, 'revokeObjectURL', function revokeObjectURL(objectUrl) {
                  objectUrlSources.delete(String(objectUrl));
                  return NativeRevokeObjectURL(objectUrl);
                });
              }

              patchConstructorValue(window, 'Worker', createWorkerConstructor(NativeWorker, 'Worker'));

              if (NativeSharedWorker)
                patchConstructorValue(window, 'SharedWorker', createWorkerConstructor(NativeSharedWorker, 'SharedWorker'));
            }
            """
        );
    }

    private static void AppendNavigatorModule(StringBuilder script, StealthContextOptions options)
    {
        script.AppendLine(
            """
            const patchNavigatorGetter = (property, getter) => patchGetterIfNeeded(Navigator.prototype, navigator, property, getter);

            patchNavigatorGetter('webdriver', () => false);
            patchNavigatorGetter('hardwareConcurrency', () => profile.hardwareConcurrency);
            patchNavigatorGetter('deviceMemory', () => profile.deviceMemory);
            patchNavigatorGetter('platform', () => profile.platform);
            patchNavigatorGetter('vendor', () => profile.browserVendor);
            patchNavigatorGetter('language', () => profile.locale);
            patchNavigatorGetter('languages', () => [...profile.languages]);
            patchNavigatorGetter('maxTouchPoints', () => profile.maxTouchPoints);
            patchNavigatorGetter('pdfViewerEnabled', () => true);
            patchNavigatorGetter('userAgent', () => profile.userAgent);

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
                      getHighEntropyValues: createNativeFunction('getHighEntropyValues', async hints => {
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
                      }),
                      toJSON: createNativeFunction('toJSON', () => ({
                        brands,
                        mobile: profile.isMobile,
                        platform: profile.osPlatform
                      }))
                    };
                    setNativePrototype(uaData, 'NavigatorUAData', 'NavigatorUAData');

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
            const createMimeType = (type, suffixes, description) => {
              const mimeType = {};
              patchValue(mimeType, 'type', type, { writable: false });
              patchValue(mimeType, 'suffixes', suffixes, { writable: false });
              patchValue(mimeType, 'description', description, { writable: false });
              return setNativePrototype(mimeType, 'MimeType', 'MimeType');
            };
            const pdfMimeTypes = [
              createMimeType('application/pdf', 'pdf', 'Portable Document Format'),
              createMimeType('text/pdf', 'pdf', 'Portable Document Format')
            ];
            const createPlugin = (name, filename, description, pluginMimeTypes) => {
              const plugin = {};
              pluginMimeTypes.forEach((mimeType, index) => {
                plugin[index] = mimeType;
                plugin[mimeType.type] = mimeType;
              });
              patchValue(plugin, 'name', name, { writable: false });
              patchValue(plugin, 'filename', filename, { writable: false });
              patchValue(plugin, 'description', description, { writable: false });
              patchValue(plugin, 'length', pluginMimeTypes.length, { enumerable: false, writable: false });
              patchValue(plugin, 'item', function item(index) {
                return this[index] ?? null;
              });
              patchValue(plugin, 'namedItem', function namedItem(name) {
                return this[name] ?? null;
              });
              return setNativePrototype(plugin, 'Plugin', 'Plugin');
            };
            const plugins = createArrayLike([
              createPlugin('PDF Viewer', 'internal-pdf-viewer', 'Portable Document Format', pdfMimeTypes),
              createPlugin('Chrome PDF Viewer', 'internal-pdf-viewer', 'Portable Document Format', pdfMimeTypes),
              createPlugin('Chromium PDF Viewer', 'internal-pdf-viewer', 'Portable Document Format', pdfMimeTypes),
              createPlugin('Microsoft Edge PDF Viewer', 'internal-pdf-viewer', 'Portable Document Format', pdfMimeTypes),
              createPlugin('WebKit built-in PDF', 'internal-pdf-viewer', 'Portable Document Format', pdfMimeTypes)
            ], 'name', 'PluginArray', 'PluginArray');

            pdfMimeTypes.forEach(mimeType => {
              patchValue(mimeType, 'enabledPlugin', plugins[0], { writable: false });
            });

            const mimeTypes = createArrayLike(pdfMimeTypes, 'type', 'MimeTypeArray', 'MimeTypeArray');

            patchNavigatorGetter('mimeTypes', () => mimeTypes);
            patchNavigatorGetter('plugins', () => plugins);
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
              if (!window.chrome.csi) {
                patchValue(window.chrome, 'csi', function csi() {
                  return {
                    onloadT: Date.now(),
                    startE: Date.now() - Math.round(50 + rand() * 100),
                    pageT: Math.round(100 + rand() * 200),
                    tran: 15
                  };
                });
              }
              if (!window.chrome.loadTimes) {
                patchValue(window.chrome, 'loadTimes', function loadTimes() {
                  return {
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
                  };
                });
              }
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
                  const mediaQueryList = setNativePrototype({}, 'MediaQueryList', 'MediaQueryList');
                  patchGetter(mediaQueryList, 'matches', () => query.includes(profile.prefersDarkMode ? 'dark' : 'light'));
                  patchGetter(mediaQueryList, 'media', () => query);
                  patchValue(mediaQueryList, 'onchange', null);
                  patchValue(mediaQueryList, 'addListener', function addListener() {});
                  patchValue(mediaQueryList, 'removeListener', function removeListener() {});
                  patchValue(mediaQueryList, 'addEventListener', function addEventListener() {});
                  patchValue(mediaQueryList, 'removeEventListener', function removeEventListener() {});
                  patchValue(mediaQueryList, 'dispatchEvent', function dispatchEvent() { return false; });
                  return mediaQueryList;
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
            patchGetterIfNeeded(window, window, 'devicePixelRatio', () => profile.devicePixelRatio);
            const outerWidthOffset = profile.isMobile ? 0 : 16;
            const outerHeightOffset = profile.isMobile ? 0 : 88;
            patchGetter(window, 'outerWidth', () => Math.round(window.innerWidth + outerWidthOffset));
            patchGetter(window, 'outerHeight', () => Math.round(window.innerHeight + outerHeightOffset));

            if (window.screen) {
              patchGetterIfNeeded(window.screen, window.screen, 'width', () => profile.screenWidth);
              patchGetterIfNeeded(window.screen, window.screen, 'height', () => profile.screenHeight);
              patchGetterIfNeeded(window.screen, window.screen, 'availWidth', () => profile.screenWidth);
              patchGetterIfNeeded(window.screen, window.screen, 'availHeight', () => profile.screenHeight - 40);
              patchGetterIfNeeded(window.screen, window.screen, 'colorDepth', () => profile.colorDepth);
              patchGetterIfNeeded(window.screen, window.screen, 'pixelDepth', () => profile.pixelDepth);
            }
            """
        );
    }

    private static void AppendMediaModule(StringBuilder script, StealthContextOptions options)
    {
        script.AppendLine(
            """
            const connection = setNativePrototype({}, 'NetworkInformation', 'NetworkInformation');
            const connectionDownlink = 10 + Math.round(rand() * 30);
            const connectionRtt = 50 + (Math.round(rand() * 5) * 25);
            patchGetter(connection, 'downlink', () => connectionDownlink);
            patchGetter(connection, 'downlinkMax', () => 100);
            patchGetter(connection, 'effectiveType', () => '4g');
            patchGetter(connection, 'rtt', () => connectionRtt);
            patchGetter(connection, 'saveData', () => false);
            patchGetter(connection, 'type', () => 'wifi');
            patchValue(connection, 'onchange', null);
            patchValue(connection, 'addEventListener', function addEventListener() {});
            patchValue(connection, 'removeEventListener', function removeEventListener() {});
            patchValue(connection, 'dispatchEvent', function dispatchEvent() { return false; });

            try {
              const nativeConnection = navigator.connection;
              if (!nativeConnection || nativeConnection.rtt <= 0 || nativeConnection.downlink <= 0) {
                patchGetter(Navigator.prototype, 'connection', () => connection);
              }
            } catch {
              patchGetter(Navigator.prototype, 'connection', () => connection);
            }

            if (navigator.getBattery || Navigator.prototype.getBattery) {
              patchValue(Navigator.prototype, 'getBattery', function getBattery() {
                return Promise.resolve({
                  charging: true,
                  chargingTime: 0,
                  dischargingTime: Infinity,
                  level: 0.76 + (rand() * 0.1),
                  onchargingchange: null,
                  onlevelchange: null,
                  onchargingtimechange: null,
                  ondischargingtimechange: null
                });
              });
            }

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

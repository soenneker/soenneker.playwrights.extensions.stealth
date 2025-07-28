using Microsoft.Playwright;
using Soenneker.Extensions.Task;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Soenneker.Playwrights.Extensions.Stealth;

/// <summary>
/// A collection of Playwright extensions for more stealthy usage
/// </summary>
public static class PlaywrightsStealthExtension
{
    public static async Task<IBrowserContext> CreateStealthContext(this IBrowser browser, string timeZone = "America/New_York", Proxy? proxy = null)
    {
        var profile = HardwareProfile.Generate(timeZone);

        BrowserNewContextOptions options = BuildContextOptions(profile, timeZone, proxy);
        IBrowserContext context = await browser.NewContextAsync(options).NoSync();

        // add all patches BEFORE the page loads
        await context.AddInitScriptAsync(BuildInitScript(profile)).NoSync();

        return context;
    }

    private static BrowserNewContextOptions BuildContextOptions(HardwareProfile p, string tz, Proxy? proxy)
    {
        string ua = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " + $"Chrome/{p.ChromeVersion} Safari/537.36";

        var headers = new Dictionary<string, string>
        {
            ["Accept-Language"] = "en-US,en;q=0.9",
            ["Upgrade-Insecure-Requests"] = "1",
            // UA‑Client‑Hints (network side!)
            ["sec-ch-ua"] = $"\"Chromium\";v=\"114\", \"Google Chrome\";v=\"114\", \"Not;A=Brand\";v=\"99\"",
            ["sec-ch-ua-mobile"] = "?0",
            ["sec-ch-ua-platform"] = "\"Windows\"",
            ["sec-ch-ua-full-version"] = $"\"{p.ChromeVersion}\""
        };

        return new BrowserNewContextOptions
        {
            UserAgent = ua,
            Locale = "en-US",
            TimezoneId = tz,
            ViewportSize = new ViewportSize {Width = p.ScreenW, Height = p.ScreenH},
            ExtraHTTPHeaders = headers,
            Proxy = proxy
        };
    }

    private static string BuildInitScript(HardwareProfile p)
    {
        var b = new StringBuilder();

        // helper for deterministic noise
        b.Append($@"
        (() => {{
            let seed = {p.Seed};
            const rand = () => (Math.sin(seed++) + 1) / 2;

            // ---------- navigator.webdriver ----------
            Object.defineProperty(navigator,'webdriver',{{ get:()=>undefined }});

            // ---------- hardware ----------
            Object.defineProperty(navigator,'hardwareConcurrency',{{ get:()=>{p.Cores} }});
            Object.defineProperty(navigator,'deviceMemory',      {{ get:()=>{p.MemoryGb} }});
            Object.defineProperty(navigator,'platform',          {{ get:()=>'{p.Platform}' }});

            // ---------- userAgentData ----------
            const brands = [
                {{brand:'Chromium', version:'114'}},
                {{brand:'Google Chrome', version:'114'}},
                {{brand:'Not;A=Brand', version:'99'}}
            ];
            const uaData = {{
                brands,
                mobile:false,
                platform:'Windows',
                getHighEntropyValues: async hints => {{
                    const map = {{
                        architecture:'x86',
                        model:'',
                        platformVersion:'15.0.0',
                        uaFullVersion:'{p.ChromeVersion}',
                        bitness:'64',
                        fullVersionList:brands
                    }};
                    return Object.fromEntries(hints.map(h=>[h,map[h]]));
                }}
            }};
            Object.defineProperty(navigator,'userAgentData',{{ get:()=>uaData }});

            // ---------- window.chrome ----------
            window.chrome = {{
                runtime:{{}},
                app:{{ isInstalled:false }},
                webstore:{{ onInstallStageChanged:{{addListener:()=>{{}}}}, onDownloadProgress:{{addListener:()=>{{}}}}}}
            }};

            // ---------- permissions shim ----------
            const permQuery = navigator.permissions.query;
            navigator.permissions.query = function(args){{
                const delay = 20 + rand()*30;
                return new Promise(r => setTimeout(() => r(
                    args && args.name === 'notifications'
                        ? {{ state: Notification.permission }}
                        : permQuery.call(this,args)
                ), delay));
            }};

            // ---------- conn ----------
            Object.defineProperty(navigator,'connection',{{ value:{{ downlink:10, effectiveType:'4g', rtt:50, saveData:false }} }});

            // ---------- timezone (Intl) ----------
            const origDTF = Intl.DateTimeFormat;
            Intl.DateTimeFormat = function(...args){{
                const dtf = new origDTF(...args);
                const ro  = dtf.resolvedOptions();
                Object.defineProperty(ro,'timeZone',{{ get:()=>'{p.Platform}' }});
                dtf.resolvedOptions = () => ro;
                return dtf;
            }};

            // ---------- media devices ----------
            if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices){{
                navigator.mediaDevices.enumerateDevices = async () => ([
                    {{deviceId:'default',groupId:'audio1',kind:'audioinput', label:'Microphone'}},
                    {{deviceId:'default',groupId:'audio1',kind:'audiooutput',label:'Speaker'}},
                    {{deviceId:'default',groupId:'vid1',kind:'videoinput',   label:'Integrated Camera'}}
                ]);
            }}

            // ---------- font fingerprint ----------
            if (document.fonts) {{
                const orig = document.fonts;
                const fake = new Set(orig);
                ['Arial','Courier New','Times New Roman','Segoe UI'].forEach(f=>fake.add(f));
                Object.defineProperty(document,'fonts',{{ value: fake }});
            }}

            // ---------- canvas noise ----------
            const origToDataURL = HTMLCanvasElement.prototype.toDataURL;
            HTMLCanvasElement.prototype.toDataURL = function(...args){{
                const ctx = this.getContext('2d');
                if (ctx && ctx.globalAlpha){{
                    ctx.globalAlpha = 0.9997 + rand()*0.0002;
                }}
                return origToDataURL.apply(this,args);
            }};

            // ---------- WebGL vendor ----------
            const getParam = WebGLRenderingContext.prototype.getParameter;
            WebGLRenderingContext.prototype.getParameter = function(p){{
                if (p===37445) return 'Intel Inc.';
                if (p===37446) return 'Intel Iris OpenGL Engine';
                return getParam.call(this,p);
            }};

            // ---------- WebRTC leak ----------
            const pc = RTCPeerConnection.prototype;
            ['createOffer','createAnswer'].forEach(fn => {{
                const orig = pc[fn];
                pc[fn] = function(...a){{
                    return orig.apply(this,a).then(desc => {{
                        if (desc && desc.sdp) desc.sdp = desc.sdp.replace(/a=candidate:.+\\r?\\n/g,'');
                        return desc;
                    }});
                }};
            }});
            Object.defineProperty(pc,'localDescription',{{ get:()=>null }});
        }})();");

        return b.ToString();
    }
}
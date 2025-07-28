using Microsoft.Playwright;
using Soenneker.Extensions.Task;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Soenneker.Playwrights.Extensions.Stealth;

/// <summary>
/// A collection of Playwright extensions for more stealthy usage
/// </summary>
public static class PlaywrightsStealthExtension
{
    public static Task<IBrowser> LaunchStealthChromium(this IPlaywright pw) =>
        pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = "chromium",
            Args =
            [
                "--disable-blink-features=AutomationControlled",
                "--enable-quic",
                "--origin-to-force-quic-on=*",
                "--use-gl=desktop",
                "--no-sandbox"
            ]
        });

    public static async ValueTask<IBrowserContext> CreateStealthContext(this IBrowser browser, Proxy? proxy = null)
    {
        var profile = HardwareProfile.Generate();

        BrowserNewContextOptions options = BuildContextOptions(profile, proxy);
        IBrowserContext context = await browser.NewContextAsync(options).NoSync();

        // add all patches BEFORE the page loads
        await context.AddInitScriptAsync(BuildInitScript(profile)).NoSync();

        return context;
    }

    private static BrowserNewContextOptions BuildContextOptions(HardwareProfile p, Proxy? proxy)
    {
        string ua = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) " + $"AppleWebKit/537.36 (KHTML, like Gecko) " + $"Chrome/{p.ChromeVersion} Safari/537.36";

        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
            ["Accept-Language"] = "en-US,en;q=0.9",
            ["Upgrade-Insecure-Requests"] = "1",
            ["Accept-Encoding"] = "gzip, deflate, br", // chrome 114 aligned
            ["sec-ch-ua"] = "\"Chromium\";v=\"114\", \"Google Chrome\";v=\"114\", \"Not;A=Brand\";v=\"99\"",
            ["sec-ch-ua-mobile"] = "?0",
            ["sec-ch-ua-platform"] = "\"Windows\"",
            ["sec-ch-ua-full-version-list"] = $"\"Chromium\";v=\"{p.ChromeVersion}\", \"Google Chrome\";v=\"{p.ChromeVersion}\", \"Not;A=Brand\";v=\"99\"",
            ["sec-ch-ua-arch"] = "\"x86\"",
            ["sec-ch-ua-bitness"] = "\"64\""
        };

        return new BrowserNewContextOptions
        {
            UserAgent = ua,
            Locale = "en-US",
            TimezoneId = p.TimeZone,
            DeviceScaleFactor = 1,
            ViewportSize = new ViewportSize {Width = p.ScreenW, Height = p.ScreenH},
            ExtraHTTPHeaders = headers,
            Proxy = proxy
        };
    }

    private static string BuildInitScript(HardwareProfile p)
    {
        var lat = p.Latitude.ToString("F5", CultureInfo.InvariantCulture);
        var lng = p.Longitude.ToString("F5", CultureInfo.InvariantCulture);

        return $$$$"""
                   (()=>{
                       let seed={{{{p.Seed}}}};
                       const rand = () => ((Math.sin(seed++) + 1) / 2);

                       /* webdriver flag */
                       Object.defineProperty(navigator,'webdriver',{get:()=>undefined});

                       /* classic navigator props */
                       Object.defineProperty(navigator,'hardwareConcurrency',{get:()=>{{{{p.Cores}}}}});
                       Object.defineProperty(navigator,'deviceMemory',{get:()=>{{{{p.MemoryGb}}}}});
                       Object.defineProperty(navigator,'platform',{get:()=>'{{{{p.Platform}}}}'});
                       Object.defineProperty(navigator,'vendor',{get:()=>'Google Inc.'});

                       /* window outer dims (viewport + chrome) – DPI aware */
                       Object.defineProperty(window,'outerWidth',{get:()=>{{{{p.ScreenW}}}}});
                       Object.defineProperty(window,'outerHeight',{get:()=>{
                           const chromeBar = Math.min(Math.max(70, 85*window.devicePixelRatio), 115);
                           return {{{p.ScreenH}}} + chromeBar;
                       }});

                       /* UA‑CH high‑entropy shim */
                       const brands=[
                           {brand:'Chromium',version:'114'},
                           {brand:'Google Chrome',version:'114'},
                           {brand:'Not;A=Brand',version:'99'}
                       ];
                       const uaData={
                           brands,
                           mobile:false,
                           platform:'Windows',
                           getHighEntropyValues:async h=>Object.fromEntries(
                               h.map(x=>[x,{
                                   architecture:'x86',
                                   model:'',
                                   bitness:'64',
                                   platformVersion:'15.0.0',
                                   uaFullVersion:'{{{{p.ChromeVersion}}}}',
                                   fullVersionList:brands
                               }[x]]))
                       };
                       Object.defineProperty(navigator,'userAgentData',{get:()=>uaData});

                       /* window.chrome stub */
                       window.chrome={
                           runtime:{},
                           webstore:{
                               onInstallStageChanged:{addListener:()=>{}},
                               onDownloadProgress:{addListener:()=>{}}
                           }
                       };

                       /* permissions latency shim (clone for instanceof safety) */
                       const realQuery = navigator.permissions.query.bind(navigator.permissions);
                       navigator.permissions.query = d => new Promise(res=>{
                           setTimeout(async ()=>{
                               const r = await realQuery(d);
                               res({state:r.state,onchange:null});
                           },20+rand()*30);
                       });

                       /* navigator.connection (extra fields) */
                       Object.defineProperty(navigator,'connection',{ get: () => ({
                           downlink    : 10 + rand()*40,
                           downlinkMax : 100,
                           effectiveType:'4g',
                           rtt         : Math.round(40 + rand()*120),
                           saveData    : false
                       })});

                       /* Battery API */
                       navigator.getBattery = () => Promise.resolve({
                           charging:true,
                           chargingTime:0,
                           dischargingTime:Infinity,
                           level:0.77,
                           onchargingchange:null,
                           onlevelchange:null,
                           onchargingtimechange:null,
                           ondischargingtimechange:null
                       });

                       /* Intl timezone coherence */
                       const origDTF = Intl.DateTimeFormat;
                       Intl.DateTimeFormat = function(...a){
                           const dtf = new origDTF(...a);
                           const ro  = dtf.resolvedOptions();
                           Object.defineProperty(ro,'timeZone',{get:()=>'{{{{p.TimeZone}}}}'});
                           dtf.resolvedOptions = () => ro;
                           return dtf;
                       };

                       /* FontFaceSet proxy */
                       if(document.fonts && window.FontFaceSet){
                           const orig   = document.fonts;
                           const extras = ['Arial','Courier New','Times New Roman','Segoe UI'];
                           const proxy  = new Proxy(orig,{
                               get:(t,p)=>
                                   p==='size'  ? t.size + extras.length :
                                   p==='values'? ()=>[...t.values(),...extras.map(f=>new FontFace(f,'local("'+f+'")'))] :
                                                 Reflect.get(t,p)
                           });
                           Object.defineProperty(document,'fonts',{value:proxy});
                       }

                       /* media devices list */
                       if(navigator.mediaDevices && navigator.mediaDevices.enumerateDevices){
                           navigator.mediaDevices.enumerateDevices = async () => ([
                               {deviceId:'default',groupId:'aud1',kind:'audioinput', label:'Microphone'},
                               {deviceId:'default',groupId:'aud1',kind:'audiooutput',label:'Speaker'},
                               {deviceId:'default',groupId:'vid1',kind:'videoinput', label:'Integrated Camera'}
                           ]);
                       }

                       /* geolocation */
                       const position = {
                           coords:{latitude:{{{{lat}}}},longitude:{{{{lng}}}},accuracy:25},
                           timestamp:Date.now()
                       };
                       navigator.geolocation ??={};
                       navigator.geolocation.getCurrentPosition = cb => setTimeout(()=>cb(position),200+rand()*300);
                       navigator.geolocation.watchPosition      = cb => (cb(position),1);

                       /* canvas noise */
                       const origToDataURL = HTMLCanvasElement.prototype.toDataURL;
                       HTMLCanvasElement.prototype.toDataURL = function(...a){
                           const ctx=this.getContext('2d');
                           if(ctx && ctx.globalAlpha) ctx.globalAlpha = 0.9997 + rand()*0.0002;
                           return origToDataURL.apply(this,a);
                       };

                       /* WebGL vendor spoof */
                       const getParam = WebGLRenderingContext.prototype.getParameter;
                       WebGLRenderingContext.prototype.getParameter = function(p){
                           if(p===37445) return 'Intel Inc.';
                           if(p===37446) return 'Intel Iris OpenGL Engine';
                           return getParam.call(this,p);
                       };

                       /* WebRTC leak block */
                       const pc = RTCPeerConnection.prototype;
                       ['createOffer','createAnswer'].forEach(fn=>{
                           const o = pc[fn];
                           pc[fn] = function(...a){
                               return o.apply(this,a).then(d=>{
                                   if(d && d.sdp) d.sdp = d.sdp.replace(/a=candidate:.+\r?\n/g,'');
                                   return d;
                               });
                           };
                       });
                       Object.defineProperty(pc,'localDescription',{get:()=>null});
                   })();
                   """;
    }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using Arashi.Kestrel;
using ARSoft.Tools.Net.Dns;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using static Arashi.AoiConfig;

namespace Arashi.Aoi.Routes
{
    class AdminRoutes
    {
        public static void AdminRoute(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(Config.AdminPerfix + "/cache/ls", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
                context.Response.ContentType = "text/plain";

                if (context.Request.Cookies.TryGetValue("atoken", out string tokenValue) &&
                    tokenValue.Equals(Config.AdminToken))
                    await context.Response.WriteAsync(MemoryCache.Default.Aggregate(string.Empty,
                        (current, item) =>
                            current + $"{item.Key.ToUpper()}:{((List<DnsRecordBase>)item.Value).FirstOrDefault()}" +
                            Environment.NewLine));
                else await context.Response.WriteAsync("Token Required");
            });
            endpoints.Map(Config.AdminPerfix + "/cnlist/ls", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
                context.Response.ContentType = "text/plain";

                if (context.Request.Cookies.TryGetValue("atoken", out var tokenValue) &&
                    tokenValue.Equals(Config.AdminToken))
                    await context.Response.WriteAsync(string.Join(Environment.NewLine, DNSChina.ChinaList));
                else await context.Response.WriteAsync("Token Required");
            });
            endpoints.Map(Config.AdminPerfix + "/cache/rm", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
                context.Response.ContentType = "text/plain";

                if (context.Request.Cookies.TryGetValue("atoken", out var tokenValue) &&
                    tokenValue.Equals(Config.AdminToken))
                {
                    MemoryCache.Default.Trim(100);
                    await context.Response.WriteAsync("Trim OK");
                }
                else await context.Response.WriteAsync("Token Required");
            });
            endpoints.Map(Config.AdminPerfix + "/set-token", async context =>
            {
                context.Response.Headers.Add("X-Powered-By", "ArashiDNSP/ONE.Aoi");
                context.Response.ContentType = "text/plain";
                if (context.Request.Query.TryGetValue("t", out var tokenValue))
                {
                    context.Response.Cookies.Append("atoken", tokenValue.ToString(),
                        new CookieOptions
                        {
                            Path = "/",
                            HttpOnly = true,
                            MaxAge = TimeSpan.FromDays(30),
                            SameSite = SameSiteMode.Strict,
                            IsEssential = true
                        });
                    await context.Response.WriteAsync("Set OK");
                }
                else await context.Response.WriteAsync("Token Required");
            });
        }
    }
}

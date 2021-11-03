using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using HlidacStatu.Entities;
using HlidacStatu.LibCore.Services;
using HlidacStatu.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace HlidacStatu.LibCore.MiddleWares
{
    public class RequestTrackMiddleware
    {
        public static string TrackDataKey { get; } = "HS_track_data";

        private readonly RequestDelegate _next;

        public RequestTrackMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            string? ip = context.Connection.RemoteIpAddress?.ToString();
            string traceIdentifier = context.TraceIdentifier;
            string method = context.Request.Method;
            string requestUrl = context.Request.GetEncodedUrl();

            await _next(context);
            
            if(!context.Request.Path.StartsWithSegments("/api")) // do this tracking only for /API
                return;

            string? userName = context.User.Identity?.Name;
            
            int statusCode = context.Response.StatusCode;

            string customData = "";
            if (context.Items.TryGetValue(TrackDataKey, out object? trackData))
            {
                var dataList = (TrackDataList)trackData;
                customData = dataList.ToJson();
            }
            
            //get request time...
            long timeElapsed = 0;
            if (context.Items.TryGetValue("timeToProcessRequest", out object? requestTime))
            {
                timeElapsed = (long)requestTime;
            }
            
            string exceptionDetail = "";
            if (context.Items.TryGetValue("errorPageCtx", out object? exception))
            {
                exceptionDetail = (string)exception;
            }

            //assemble string here
            var audit = new Audit()
            {
                date = DateTime.Now,
                operation = Audit.Operations.Call.ToString(),
                userId = userName,
                IP = ip,
                traceId = traceIdentifier,
                method = method,
                requestUrl = requestUrl,
                statusCode = statusCode,
                additionalData = customData,
                timeElapsed = timeElapsed,
                exception = exceptionDetail,
                
            };
            
            AuditRepo.Add(audit);
        }
    }

    public static class RequestTrackMiddlewareExtension
    {
        public static IApplicationBuilder UseRequestTrackMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestTrackMiddleware>();
        }

        public static void SetTrackData(this HttpContext context, string dataString)
        {
            if (context.Items.TryGetValue(RequestTrackMiddleware.TrackDataKey, out var trackData))
            {
                var dataList = (TrackDataList)trackData;
                dataList.Add(dataString);
            }
            else
            {
                var dataList = new TrackDataList();
                dataList.Add(dataString);
                
                context.Items.TryAdd(RequestTrackMiddleware.TrackDataKey, dataList);
            }

        }
    }

    public class TrackDataList
    {
        private List<string> Storage { get; } = new();
        
        public void Add(string item)
        {
            Storage.Add(item);
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(Storage);
        }
    }
    
}
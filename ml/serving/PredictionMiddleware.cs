using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace His.Hope.ML.Serving
{
    public class PredictionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly PredictionClient _client;

        public PredictionMiddleware(RequestDelegate next, PredictionClient client)
        {
            _next = next;
            _client = client;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/api/appointments"))
            {
                context.Items["NoShowEnabled"] = true;
            }

            if (context.Request.Path.StartsWithSegments("/api/clinical"))
            {
                context.Items["ReadmissionEnabled"] = true;
            }

            await _next(context);
        }
    }
}

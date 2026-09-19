using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LH.Main.Unity.Server
{
    public sealed class GameServerHealthServer
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _stopping;
        private Task? _requestLoop;
        private Func<GameServerStatus>? _statusProvider;
        private Func<bool>? _readyProvider;

        public void Start(GameServerConfig config, Func<GameServerStatus> statusProvider, Func<bool> readyProvider)
        {
            if (_listener != null)
                throw new InvalidOperationException("Health server is already running.");

            _statusProvider = statusProvider;
            _readyProvider = readyProvider;
            _stopping = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{config.HttpPort}/");
            _listener.Start();
            _requestLoop = Task.Run(() => RunAsync(_stopping.Token));
        }

        public void Stop()
        {
            CancellationTokenSource? stopping = _stopping;
            HttpListener? listener = _listener;

            _stopping = null;
            _listener = null;
            _statusProvider = null;
            _readyProvider = null;

            try
            {
                stopping?.Cancel();
                listener?.Stop();
                listener?.Close();
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                stopping?.Dispose();
            }
        }

        private async Task RunAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    HttpListener? listener = _listener;
                    if (listener == null)
                        return;

                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(context), stoppingToken);
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            string path = context.Request.Url?.AbsolutePath ?? string.Empty;

            if (path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase))
            {
                bool ready = _readyProvider?.Invoke() == true;
                string body = ready ? "{\"status\":\"ok\"}" : "{\"status\":\"not_ready\"}";
                await WriteJsonAsync(context, ready ? 200 : 503, body).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/status", StringComparison.OrdinalIgnoreCase))
            {
                GameServerStatus? status = _statusProvider?.Invoke();
                await WriteJsonAsync(context, 200, status?.ToJson() ?? "{\"state\":\"failed\"}").ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(context, 404, "{\"error\":\"not_found\"}").ConfigureAwait(false);
        }

        private static async Task WriteJsonAsync(HttpListenerContext context, int statusCode, string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;

            try
            {
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
            catch (HttpListenerException ex)
            {
                Debug.LogWarning($"Health response write failed: {ex.Message}");
            }
            finally
            {
                context.Response.Close();
            }
        }
    }
}

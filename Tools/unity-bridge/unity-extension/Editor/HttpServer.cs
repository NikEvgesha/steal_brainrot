using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace UnityBridge
{
    /// <summary>
    /// РџСЂРѕСЃС‚РѕР№ HTTP СЃРµСЂРІРµСЂ РґР»СЏ Unity Bridge
    /// РўРѕР»СЊРєРѕ HTTP Р»РѕРіРёРєР° - Р±РµР· Р±РёР·РЅРµСЃ-Р»РѕРіРёРєРё
    /// </summary>
    public class HttpServer
    {
        private readonly int port;
        private readonly Func<string, Dictionary<string, object>, Dictionary<string, object>> requestHandler;
        private HttpListener listener;
        private Thread listenerThread;
        private bool isRunning;
        
        public HttpServer(int port, Func<string, Dictionary<string, object>, Dictionary<string, object>> requestHandler)
        {
            this.port = port;
            this.requestHandler = requestHandler;
        }
        
        public bool Start()
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Start();
                
                isRunning = true;
                listenerThread = new Thread(ListenForRequests) 
                { 
                    IsBackground = true,
                    Name = "UnityBridge-HttpListener"
                };
                listenerThread.Start();
                
                Debug.Log($"HTTP Server started on port {port}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to start HTTP server: {ex.Message}");
                return false;
            }
        }
        
        public void Stop()
        {
            if (!isRunning) return;
            
            try
            {
                isRunning = false;
                listener?.Stop();
                listener?.Close();
                
                if (listenerThread?.IsAlive == true)
                {
                    listenerThread.Join(1000);
                }

                listenerThread = null;
                listener = null;
                
                Debug.Log("HTTP Server stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error stopping HTTP server: {ex.Message}");
            }
        }
        
        private void ListenForRequests()
        {
            while (isRunning && listener != null)
            {
                try
                {
                    var context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => ProcessRequest(context));
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (HttpListenerException)
                {
                    if (!isRunning) return;
                }
                catch (ObjectDisposedException)
                {
                    if (!isRunning) return;
                }
                catch (Exception ex)
                {
                    if (isRunning)
                        Debug.LogError($"HTTP listener error: {ex.Message}");
                }
            }
        }
        
        private void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            
            try
            {
                // CORS headers
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                
                // Handle OPTIONS preflight
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }
                
                // Parse request
                var endpoint = request.Url.AbsolutePath;
                var requestData = ParseRequestBody(request);
                
                // Process request
                var responseData = requestHandler(endpoint, requestData);
                
                // Send response
                SendJsonResponse(response, responseData, 200);
            }
            catch (ObjectDisposedException)
            {
                if (!isRunning)
                    return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Request processing error: {ex.Message}");
                var errorResponse = ResponseBuilder.BuildErrorResponse($"Request processing failed: {ex.Message}");
                SendJsonResponse(response, errorResponse, 500);
            }
        }
        
        private Dictionary<string, object> ParseRequestBody(HttpListenerRequest request)
        {
            if (request.ContentLength64 == 0)
                return new Dictionary<string, object>();
                
            try
            {
                // рџљЂ РџСЂРёРЅСѓРґРёС‚РµР»СЊРЅРѕ РёСЃРїРѕР»СЊР·СѓРµРј UTF-8 РґР»СЏ РєРѕСЂСЂРµРєС‚РЅРѕР№ РѕР±СЂР°Р±РѕС‚РєРё РєРёСЂРёР»Р»РёС†С‹
                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                {
                    var body = reader.ReadToEnd();
                    return string.IsNullOrWhiteSpace(body) 
                        ? new Dictionary<string, object>() 
                        : JsonUtils.FromJson(body);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to parse request body: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }
        
        private void SendJsonResponse(HttpListenerResponse response, Dictionary<string, object> data, int statusCode)
        {
            try
            {
                var json = JsonUtils.ToJson(data);
                var buffer = Encoding.UTF8.GetBytes(json);
                
                // рџљЂ РџРѕРґРґРµСЂР¶РєР° UTF-8 РєРѕРґРёСЂРѕРІРєРё РґР»СЏ РєРёСЂРёР»Р»РёС†С‹
                response.ContentType = "application/json; charset=utf-8";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = buffer.Length;
                response.StatusCode = statusCode;
                
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send response: {ex.Message}");
            }
            finally
            {
                try
                {
                    response.Close();
                }
                catch
                {
                    // Ignore close errors
                }
            }
        }
    }
} 

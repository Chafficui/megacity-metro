using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// RestServer is a simple REST server that can be used to create a REST API in Unity.
/// </summary>
/// <remarks>
/// Usage:
/// 1. Create a new instance of RestServer with the desired port number.
/// 2. Add endpoints to the server using the AddEndpoint method.
/// 3. Start the server using the StartServer method.
/// 4. Stop the server using the StopServer method.
/// </remarks>
/// Example:
/// <code>
/// RestServer server = new RestServer(8080);
/// server.port = 8080;
/// server.AddEndpoint("/hello", HttpMethod.GET, (request) => {
///    return new { message = "Hello, World!" };
/// });
/// server.StartServer();
/// </code>
/// <remarks>
/// This will create a server that listens on port 8080 and has one endpoint at /hello that returns a JSON object with a message property.
/// </remarks>
/// <seealso cref="HTTPEndpoint"/>
/// <seealso cref="HttpMethod"/>
public class RestServer
{
    private readonly HttpListener _Listener;
    private readonly int _Port;
    /// <summary>
    /// Gets a value indicating whether the RestServer is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }
    private readonly List<HTTPEndpoint> _Endpoints;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="RestServer"/> class.
    /// </summary>
    /// <param name="port">The port number for the server.</param>
    /// <remarks>
    /// This constructor creates a new instance of the RestServer class with the specified port number.
    /// </remarks>
    /// <seealso cref="StartServer"/>
    /// <seealso cref="StopServer"/>
    public RestServer(int port)
    {
        this._Port = port;
        _Listener = new HttpListener();
        _Endpoints = new List<HTTPEndpoint>();
    }

    /// <summary>
    /// Starts the server.
    /// </summary>
    /// <remarks>
    /// This method starts the server and begins listening for incoming requests.
    /// </remarks>
    /// <seealso cref="StopServer"/>
    /// <seealso cref="Listen"/>
    /// <seealso cref="AddEndpoint"/>
    public void StartServer()
    {
        if (IsRunning) return;

        _Listener.Prefixes.Add($"http://*:{_Port}/");
        _Listener.Start();
        IsRunning = true;
        Listen();
        Debug.Log("[] RestServer started on port " + _Port);
    }

    /// <summary>
    /// Listens for incoming requests.
    /// </summary>
    /// <remarks>
    /// This method listens for incoming requests and processes them using the ProcessRequest method.
    /// </remarks>
    /// <seealso cref="ProcessRequest"/>
    /// <seealso cref="StartServer"/>
    /// <seealso cref="StopServer"/>
    private async void Listen()
    {
        while (IsRunning)
        {
            try
            {
                HttpListenerContext context = await _Listener.GetContextAsync();
                ProcessRequest(context);
            }
            catch (ObjectDisposedException)
            {
                if (IsRunning)
                    Debug.LogError("[] RestServer error: The listener was disposed unexpectedly.");
                Debug.Log("[] Server stopped on port " + _Port);
                return;
            }
            catch (Exception e)
            {
                Debug.LogError("[] RestServer error: " + e.Message);
            }
        }
    }

    /// <summary>
    /// Adds an endpoint to the server.
    /// </summary>
    /// <param name="path">The path for the endpoint.</param>
    /// <param name="method">The HTTP method for the endpoint.</param>
    /// <param name="handler">The handler function for the endpoint.</param>
    /// <remarks>
    /// This method adds an endpoint to the server. The handler function is called when a request is made to the endpoint.
    /// </remarks>
    /// <seealso cref="HTTPEndpoint"/>
    /// <seealso cref="HttpMethod"/>
    /// <seealso cref="StartServer"/>
    /// <seealso cref="StopServer"/>
    /// <seealso cref="ProcessRequest"/>
    /// <seealso cref="Listen"/>
    public void AddEndpoint(string path, HttpMethod method, Func<HttpListenerRequest, object> handler)
    {
        HTTPEndpoint endpoint = new HTTPEndpoint
        {
            Path = path,
            Method = method,
            Handler = handler
        };
        _Endpoints.Add(endpoint);
    }

    /// <summary>
    /// Processes an incoming request.
    /// </summary>
    /// <param name="context">The HttpListenerContext for the request.</param>
    /// <remarks>
    /// This method processes an incoming request and calls the handler function for the corresponding endpoint.
    /// </remarks>
    /// <seealso cref="HTTPEndpoint"/>
    /// <seealso cref="HttpMethod"/>
    private void ProcessRequest(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        // CORS Header hinzufügen
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");

        // Prüfen, ob es sich um eine Preflight-Request handelt
        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = (int)HttpStatusCode.OK;
        }
        else
        {
            var endpoint = _Endpoints.Find(e => e.Path == request.Url.AbsolutePath && e.Method.ToString() == request.HttpMethod);
            
            if (endpoint.Path == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            else
            {
                try
                {
                    object data = endpoint.Handler(request);
                    string jsonResponse = JsonConvert.SerializeObject(data);

                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(jsonResponse);

                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                catch (Exception e)
                {
                    // Für echte Anwendungen sollten Fehler besser gehandhabt werden
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    Debug.LogError("[] RestServer error: " + e.Message);
                }
            }
        }
        
        response.Close();
    }

    /// <summary>
    /// Stops the server.
    /// </summary>
    /// <remarks>
    /// This method stops the server and closes the listener.
    /// </remarks>
    /// <seealso cref="StartServer"/>
    /// <seealso cref="Listen"/>
    public void StopServer()
    {
        if (!IsRunning) return;
        Debug.Log("[] Stopping RestServer on port " + _Port + "...");

        _Listener.Stop();
        IsRunning = false;
    }
}

/// <summary>
/// The HTTP method for an endpoint.
/// </summary>
/// <remarks>
/// This enum represents the HTTP methods that can be used for an endpoint.
/// </remarks>
/// <seealso cref="HTTPEndpoint"/>
/// <seealso cref="RestServer"/>
[Serializable]
public enum HttpMethod
{
    GET,
    POST,
    PUT,
    DELETE
}

/// <summary>
/// The HTTP endpoint for the server.
/// </summary>
/// <remarks>
/// This struct represents an HTTP endpoint for the server. It contains the path, method, and handler function for the endpoint.
/// </remarks>
/// <seealso cref="HttpMethod"/>
/// <seealso cref="RestServer"/>
[Serializable]
public struct HTTPEndpoint 
{
    public string Path;
    public HttpMethod Method;
    public Func<HttpListenerRequest, object> Handler;
}
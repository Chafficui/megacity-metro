using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Linq;

/// <summary>
/// RestAPI is a simple REST API that can be used to create a REST API in Unity.
/// </summary>
/// <remarks>
/// Usage:
/// 1. Create a GameObject and attach the RestAPI script to it.
/// 2. Set the serverName and port properties to the desired values.
/// 3. Add metric methods to the server using the RegisterMetricMethod method.
/// 4. Start the server by running the scene.
/// </remarks>
/// Example:
/// <code>
/// GameObject restAPI = new GameObject("RestAPI");
/// RestAPI api = restAPI.AddComponent<RestAPI>();
/// api.RegisterMetricMethod("uptime", () => {
///    return DateTime.Now - api.startTime;
/// });
/// </code>
/// <remarks>
/// This will create a REST API that listens on port 8080 and has one metric method that returns the uptime of the server.
/// </remarks>
/// <seealso cref="RestServer"/>
public class RestAPI : MonoBehaviour
{
    public static RestAPI Instance { get; private set; }

    [Header("Server Settings")]
    [Tooltip("The name of the server.")]
    public string Name = "europe01";
    [Tooltip("The port number for the server.")]
    public int Port = 8080;
    [Tooltip("If true, the rest api will only be available on the server.")]
    public bool ServerOnly = true;
    [Header("Metrics")]

    private RestServer _Server;
    private readonly Dictionary<string, object> _MetricsMethods = new ();

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (ServerOnly && !Application.isBatchMode)
            return;
        
        _Server = new RestServer(Port);
        
        ConfigureMetrics();
        
        _Server.StartServer();
    }

    private void ConfigureMetrics()
    {
        _Server.AddEndpoint("/metrics", HttpMethod.GET, (request) => {
            var result = new Dictionary<string, object> { [Name] = _MetricsMethods };
            return SerializeMetrics(result);
        });

        //add system info to metrics like server name, cpu name, ram amount, gpu name, etc
        RegisterMetricMethod("info", () => {
            var systemInfo = new Dictionary<string, object>
            {
                ["project"] = Application.productName,
                ["serverName"] = Name,
                ["cpuName"] = SystemInfo.processorType,
                ["cpuCores"] = SystemInfo.processorCount,
                ["ramAmount"] = SystemInfo.systemMemorySize,
                ["gpuName"] = SystemInfo.graphicsDeviceName,
                ["gpuMemory"] = SystemInfo.graphicsMemorySize,
                ["gpuVersion"] = SystemInfo.graphicsDeviceVersion,
                ["os"] = SystemInfo.operatingSystem,
                ["unityVersion"] = Application.unityVersion,
                ["version"] = Application.version,
                ["platform"] = Application.platform.ToString(),
                ["serverTime"] = DateTime.Now
            };
            return systemInfo;
        });
    }

    /// <summary>
    /// Registers a metric method with the server.
    /// </summary>
    /// <param name="name">The name of the metric method.</param>
    /// <param name="method">The method that returns the metric value.</param>
    /// <remarks>
    /// This method registers a metric method with the server. The method should return an object that can be serialized to JSON.
    /// It is also possible to register sub-metric methods by using a forward slash (/) in the name.
    /// </remarks>
    /// <example>
    /// <code>
    /// api.RegisterMetricMethod("uptime", () => {
    ///   return DateTime.Now - api.startTime;
    /// });
    /// api.RegisterMetricMethod("enemies/zombies", () => {
    ///   return EnemyCount;
    /// });
    /// </code>
    /// <seealso cref="RestServer"/>
    public void RegisterMetricMethod(string name, Func<object> method)
    {
        var segments = name.Split('/');
        var currentDict = _MetricsMethods;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (!currentDict.ContainsKey(segments[i]))
            {
                currentDict[segments[i]] = new Dictionary<string, object>();
            }

            currentDict = currentDict[segments[i]] as Dictionary<string, object>;
        }

        currentDict[segments.Last()] = method;
    }

    private object SerializeMetrics(Dictionary<string, object> metrics)
    {
        var serializedMetrics = new Dictionary<string, object>();

        foreach (var key in metrics.Keys)
        {
            if (metrics[key] is Func<object> method)
            {
                serializedMetrics[key] = method.Invoke();
            }
            else if (metrics[key] is Dictionary<string, object> subDict)
            {
                serializedMetrics[key] = SerializeMetrics(subDict);
            }
        }

        return serializedMetrics;
    }

    void OnApplicationQuit()
    {
        _Server?.StopServer();
    }

    void OnDestory()
    {
        _Server?.StopServer();
    }
}
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Telemetry {
    public static void LogEvent(string type, Dictionary<string, object> data) {
        if (data == null) data = new Dictionary<string, object>();
        string msg = type + " " + string.Join(",", System.Linq.Enumerable.Select(data, kv => kv.Key + "=" + kv.Value));
        Debug.Log("[Telemetry] " + msg);
    }
}

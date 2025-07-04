using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

[Serializable]
public class ServerResponse {
    public int status;
    public bool succeeded;
    public object message;  // object 型に変更
    public DateTime timestamp;

    public ServerResponse() {
        status = 200;
        succeeded = true;
        message = "";
        timestamp = DateTime.Now;
    }

    public ServerResponse(int statusCode, bool isSuccess, object msg) {
        status = statusCode;
        succeeded = isSuccess;
        message = msg;
        timestamp = DateTime.Now;
    }

    // 独自のシリアライズ処理
    public string ToJson() {
        var dict = new Dictionary<string, object>();
        dict["status"] = status;
        dict["succeeded"] = succeeded;
        dict["message"] = message;
        dict["timestamp"] = timestamp.ToString("o"); // ISO 8601 形式
        return SimpleJsonBuilder.Serialize(dict);
    }
}
public static class SimpleJsonBuilder {
    public static string Serialize(object obj) {
        if (obj == null)
            return "null";

        // RawJson 型なら中身の JSON をそのまま返す
        if (obj is RawJson raw)
            return raw.Json;

        if (obj is string s)
            return $"\"{Escape(s)}\"";

        if (obj is bool b)
            return b ? "true" : "false";

        if (obj is IDictionary<string, object> dict) {
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (var kv in dict) {
                if (!first)
                    sb.Append(",");
                sb.Append($"\"{Escape(kv.Key)}\":{Serialize(kv.Value)}");
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
        }

        if (obj is IEnumerable enumerable && !(obj is string)) {
            var sb = new StringBuilder();
            sb.Append("[");
            bool first = true;
            foreach (var item in enumerable) {
                if (!first)
                    sb.Append(",");
                sb.Append(Serialize(item));
                first = false;
            }
            sb.Append("]");
            return sb.ToString();
        }

        if (obj is int || obj is long || obj is float || obj is double || obj is decimal)
            return Convert.ToString(obj, CultureInfo.InvariantCulture);

        return $"\"{Escape(obj.ToString())}\"";
    }

    public static string Escape(string str) {
        if (string.IsNullOrEmpty(str))
            return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

public class RawJson {
    public string Json;
    public RawJson(string json) {
        Json = json;
    }
}
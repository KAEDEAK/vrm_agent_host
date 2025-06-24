using System;
using System.Collections.Generic;
using System.Text;

public static class ConfigJsonTree {
    public static object Parse(string json) {
        int idx = 0;
        return ParseValue(json.Trim(), ref idx);
    }

    static object ParseValue(string json, ref int idx) {
        SkipWhitespace(json, ref idx);
        if (json[idx] == '{') return ParseObject(json, ref idx);
        if (json[idx] == '[') return ParseArray(json, ref idx);
        if (json[idx] == '"') return ParseString(json, ref idx);
        if (char.IsDigit(json[idx]) || json[idx] == '-') return ParseNumber(json, ref idx);
        if (json.Substring(idx).StartsWith("true")) { idx += 4; return true; }
        if (json.Substring(idx).StartsWith("false")) { idx += 5; return false; }
        if (json.Substring(idx).StartsWith("null")) { idx += 4; return null; }
        throw new Exception("Unknown value");
    }

    static Dictionary<string, object> ParseObject(string json, ref int idx) {
        var dict = new Dictionary<string, object>();
        idx++; // skip '{'
        while (true) {
            SkipWhitespace(json, ref idx);
            if (json[idx] == '}') { idx++; break; }
            string key = ParseString(json, ref idx);
            SkipWhitespace(json, ref idx);
            if (json[idx] != ':') throw new Exception("Expected ':'");
            idx++;
            object val = ParseValue(json, ref idx);
            dict[key] = val;
            SkipWhitespace(json, ref idx);
            if (json[idx] == ',') { idx++; continue; }
            if (json[idx] == '}') { idx++; break; }
        }
        return dict;
    }

    static List<object> ParseArray(string json, ref int idx) {
        var list = new List<object>();
        idx++; // skip '['
        while (true) {
            SkipWhitespace(json, ref idx);
            if (json[idx] == ']') { idx++; break; }
            object val = ParseValue(json, ref idx);
            list.Add(val);
            SkipWhitespace(json, ref idx);
            if (json[idx] == ',') { idx++; continue; }
            if (json[idx] == ']') { idx++; break; }
        }
        return list;
    }

    static string ParseString(string json, ref int idx) {
        if (json[idx] != '"') throw new Exception("Expected '\"'");
        idx++;
        var sb = new StringBuilder();
        while (json[idx] != '"') {
            if (json[idx] == '\\') {
                idx++;
                if (json[idx] == '"') sb.Append('"');
                else if (json[idx] == '\\') sb.Append('\\');
                else if (json[idx] == 'n') sb.Append('\n');
                else if (json[idx] == 'r') sb.Append('\r');
                else if (json[idx] == 't') sb.Append('\t');
                else throw new Exception("Unknown escape");
            }
            else
                sb.Append(json[idx]);
            idx++;
        }
        idx++; // skip last '"'
        return sb.ToString();
    }

    static object ParseNumber(string json, ref int idx) {
        int start = idx;
        while (idx < json.Length && ("0123456789+-.eE".IndexOf(json[idx]) >= 0)) idx++;
        string s = json.Substring(start, idx - start);
        if (s.Contains(".") || s.Contains("e") || s.Contains("E"))
            return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        return int.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    }

    static void SkipWhitespace(string json, ref int idx) {
        while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
    }

    // --- 再帰的にJSON文字列化 ---
    public static string Dump(object val, int indent = 0) {
        if (val == null) return "null";
        if (val is string s) return "\"" + s.Replace("\"", "\\\"") + "\"";
        if (val is bool b) return b ? "true" : "false";
        if (val is int || val is long || val is double || val is float) return Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture);
        if (val is Dictionary<string, object> dict) {
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (var kv in dict) {
                if (!first) sb.Append(",");
                sb.Append("\n").Append(new string(' ', indent + 2));
                sb.Append("\"" + kv.Key.Replace("\"", "\\\"") + "\": ").Append(Dump(kv.Value, indent + 2));
                first = false;
            }
            if (dict.Count > 0) sb.Append("\n").Append(new string(' ', indent));
            sb.Append("}");
            return sb.ToString();
        }
        if (val is List<object> list) {
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < list.Count; i++) {
                if (i > 0) sb.Append(",");
                sb.Append("\n").Append(new string(' ', indent + 2)).Append(Dump(list[i], indent + 2));
            }
            if (list.Count > 0) sb.Append("\n").Append(new string(' ', indent));
            sb.Append("]");
            return sb.ToString();
        }
        return val.ToString();
    }
    /// <summary>
    /// src（a）の内容を、dst（b）にマージして b を上書き更新する
    /// </summary>
    public static void MergeTo(object src, ref object dst) {
        // --- ① src が null なら何もしない -----------------------------
        if (src == null) return;

        // --- ② src が “空配列 / 空辞書” なら上書きしない -------------
        if (src is List<object> srcList && srcList.Count == 0) return;
        if (src is Dictionary<string, object> srcDict0 && srcDict0.Count == 0) return;

        // --- ③ どちらも Dictionary -------------------------------
        if (src is Dictionary<string, object> srcDict && dst is Dictionary<string, object> dstDict) {
            foreach (var kv in srcDict) {
                // null・空チェック（再帰前にスキップ判定）
                if (kv.Value == null) continue;
                if (kv.Value is List<object> l && l.Count == 0) continue;
                if (kv.Value is Dictionary<string, object> d && d.Count == 0) continue;

                if (dstDict.ContainsKey(kv.Key)) {
                    object child = dstDict[kv.Key];
                    MergeTo(kv.Value, ref child);
                    dstDict[kv.Key] = child;
                }
                else {
                    dstDict[kv.Key] = DeepClone(kv.Value);
                }
            }
            return;                                   // ★ ここで終わり！
        }

        // --- ④ どちらも List --------------------------------------
        if (src is List<object> sList && dst is List<object> dList) {
            if (sList.Count == 0) return;             // 空配列なら無視

            int min = Math.Min(sList.Count, dList.Count);
            for (int i = 0; i < min; i++) {
                object elem = dList[i];
                MergeTo(sList[i], ref elem);
                dList[i] = elem;
            }
            for (int i = dList.Count; i < sList.Count; i++)
                dList.Add(DeepClone(sList[i]));
            return;
        }

        // --- ⑤ プリミティブ：src が「デフォルト値」なら無視 ----------
        if (IsDefaultValue(src)) return;

        // --- ⑥ ここに来たら普通に上書き ----------------------------
        dst = DeepClone(src);
    }

    //  ──────────────────────────────────
    private static bool IsDefaultValue(object v) {
        if (v is long l) return l == 0;
        if (v is double d) return Math.Abs(d) < 1e-12;
        if (v is bool b) return b == false;
        // 文字列は空文字ならスキップしたい場合はここに追加
        return false;
    }



    // 再帰的DeepCopy（List/Dictはclone, 値型はそのまま）
    public static object DeepClone(object src) {
        if (src is Dictionary<string, object> dict) {
            var n = new Dictionary<string, object>();
            foreach (var kv in dict) n[kv.Key] = DeepClone(kv.Value);
            return n;
        }
        if (src is List<object> list) {
            var n = new List<object>();
            foreach (var item in list) n.Add(DeepClone(item));
            return n;
        }
        // string, int, bool, double, null
        return src;
    }
}
/*
Example:

// --- 読み込み ---
string json = File.ReadAllText("default_config.json");
var obj = ConfigJsonTree.Parse(json); // object型（実体はDictionary<string,object>のツリー）

// --- 値の参照・書き換え ---
var root = (Dictionary<string, object>)obj;
var win = (Dictionary<string, object>)root["window"];
win["transparentColor"] = "#00FFAA";

// --- 子リスト操作 ---
var ips = (List<object>)root["allowedRemoteIPs"];
ips.Add("192.168.0.99");

// --- 保存 ---
string outJson = ConfigJsonTree.Dump(root, 0);
File.WriteAllText("config.json", outJson);

// --- merge ---
var a = ConfigJsonTree.Parse(File.ReadAllText("a.json"));
var b = ConfigJsonTree.Parse(File.ReadAllText("b.json"));

ConfigJsonTree.MergeTo(a, ref b); // bをaで上書きマージ！

string merged = ConfigJsonTree.Dump(b, 0);
File.WriteAllText("merged.json", merged);

*/
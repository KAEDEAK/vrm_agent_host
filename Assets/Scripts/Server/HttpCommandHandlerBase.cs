using System;
using System.Net;
using System.Text;
using UnityEngine;
using System.Collections.Specialized;

public abstract class HttpCommandHandlerBase : IHttpCommandHandler {
    public abstract void HandleCommand(HttpListenerContext context, NameValueCollection query);

    /// <summary>
    /// 共通のレスポンス送信処理（旧実装）
    /// </summary>
    /*
    public virtual void SendResponse(HttpListenerContext context, ServerResponse data) {
        try {
            string json = data.ToJson();
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = data.status;
            // ここで Content-Type を明示的に設定
            context.Response.ContentType = "application/json; charset=UTF-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
        catch (System.Exception e) {
            Debug.LogError(string.Format(i18nMsg.ERROR_RESPONSE_SEND, e.Message));
        }
    }
    */

    /// <summary>
    /// ★UPDATE★ using を使って OutputStream を確実にクローズするよう修正
    /// </summary>
    public virtual void SendResponse(HttpListenerContext context, ServerResponse data) {
        if (data == null) {
            Debug.LogError("ServerResponse is null!");
            return;
        }

        if (context == null) {
            Debug.LogWarning("HTTP context is null; skipping response.");
            return;
        }

        try {
            string json = data.ToJson();
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode      = data.status;
            context.Response.ContentType     = "application/json; charset=UTF-8";
            context.Response.ContentLength64 = buffer.Length;

            // ★UPDATE★ using を使ってストリームを自動クローズ
            using (var stream = context.Response.OutputStream) {
                stream.Write(buffer, 0, buffer.Length);
                stream.Flush();
            }
        }
        catch (Exception e) {
            Debug.LogError(string.Format(i18nMsg.ERROR_RESPONSE_SEND, e.Message));
        }
    }

    protected string GetQueryParam(NameValueCollection query, string key, string defaultValue = null) {
        // 必要に応じてバリデーションとかログ出力も加える
        return query[key] ?? defaultValue;
    }

    protected int GetQueryInt(NameValueCollection query, string key, int defaultValue = 0) {
        if (int.TryParse(query[key], out int result)) {
            return result;
        }
        return defaultValue;
    }

    protected float GetQueryFloat(NameValueCollection query, string key, float defaultValue = 0f) {
        if (float.TryParse(query[key], out float result)) {
            return result;
        }
        return defaultValue;
    }

    protected bool GetQueryBool(NameValueCollection query, string key, bool defaultValue = false) {
        if (bool.TryParse(query[key], out bool result)) {
            return result;
        }
        return defaultValue;
    }

    protected string GetQueryFileName(NameValueCollection query, string key) {
        string fileName = query[key];
        if (string.IsNullOrEmpty(fileName)) {
            // ここではnull/emptyを返す
            return null;
        }

        // ディレクトリトラバーサルを防ぐ
        if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\")) {
            // バリデーション失敗を示す
            return null;
        }

        // ※必要に応じてホワイトリスト検証も
        // 例: 半角英数字と_-.のみ許可したいなら
        // if (!Regex.IsMatch(fileName, @"^[a-zA-Z0-9_\-\.]+$")) {
        //     return null;
        // }

        return fileName;
    }

    protected string GetCommandName(NameValueCollection query, string[] allowed, out bool isValid) {
        string cmd = query["cmd"];

        if (string.IsNullOrEmpty(cmd)) {
            isValid = false;
            return "(null)"; // ← ここで明示的に置換しとく！！
        }

        foreach (var c in allowed) {
            if (string.Equals(cmd, c, StringComparison.OrdinalIgnoreCase)) {
                isValid = true;
                return cmd;
            }
        }

        isValid = false;
        return cmd; // allowed にない文字列はそのまま返す
    }

    /// <summary>
    /// yes/no（y/n）形式のブールパラメータ取得ヘルパー
    /// </summary>
    protected bool GetQueryYesNo(NameValueCollection query, string key, bool defaultValue = false) {
        string val = GetQueryParam(query, key, null)?.ToLowerInvariant();
        if (val == "y") return true;
        if (val == "n") return false;
        return defaultValue;
    }

    /// <summary>
    /// 16進カラー文字列を Color に変換 (# 省略可)
    /// </summary>
    protected bool TryParseColor(string hex, out Color color) {
        if (string.IsNullOrEmpty(hex)) {
            color = default;
            return false;
        }
        string normalized = hex.StartsWith("#") ? hex : "#" + hex;
        return ColorUtility.TryParseHtmlString(normalized, out color);
    }
}

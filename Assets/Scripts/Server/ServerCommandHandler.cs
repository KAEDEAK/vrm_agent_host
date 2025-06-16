using System.Collections.Specialized;
using System.Net;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Diagnostics;         // ★Process取得に使う
using System.Runtime.InteropServices; // ★Win32API呼び出しに使う

public class ServerCommandHandler : HttpCommandHandlerBase {
    // 許可コマンド一覧（追加・拡張があればここに追記）
    private static readonly string[] AllowedCommands = {
        "transparent", "allowDragObjects", "stayOnTop", "getstatus", "terminate"
    };

    // ★Win32 API 用：GetWindowRect / GetSystemMetrics
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(System.IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0; // 画面の幅
    private const int SM_CYSCREEN = 1; // 画面の高さ

    [System.Serializable]
    private struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public override void HandleCommand(HttpListenerContext context, NameValueCollection query) {
        var responseData = new ServerResponse();

        // GetCommandName を使用して安全に cmd を取得
        string cmd = GetCommandName(query, AllowedCommands, out bool ok);
        if (!ok) {
            responseData.status = 400;
            responseData.message = string.Format(i18nMsg.ERROR_INVALID_SERVER_CMD, cmd);
            SendResponse(context, responseData);
            return;
        }

        // TransparentWindow の取得
        var transparentWindow = GameObject.FindFirstObjectByType<TransparentWindow>();

        switch (cmd) {
            case "transparent": {
                    // enable パラメータ取得
                    string enableParam = GetQueryParam(query, "enable", null);
                    if (string.IsNullOrEmpty(enableParam)) {
                        responseData.status = 400;
                        responseData.message = "Missing 'enable' parameter.";
                        SendResponse(context, responseData);
                        break;
                    }
                    if (!bool.TryParse(enableParam, out bool enable)) {
                        responseData.status = 400;
                        responseData.message = "Invalid 'enable' parameter. Use true or false.";
                        SendResponse(context, responseData);
                        break;
                    }

                    if (transparentWindow != null) {
                        // color パラメータ取得 ("#" 省略可)
                        string hexColor = GetQueryParam(query, "color", null);
                        if (!string.IsNullOrEmpty(hexColor)) {
                            if (TryParseColor(hexColor, out Color parsedColor)) {
                                transparentWindow.OverrideTransparentColor(parsedColor);
                            } else {
                                responseData.status = 400;
                                responseData.message = $"Invalid color format: {hexColor}";
                                SendResponse(context, responseData);
                                break;
                            }
                        }

                        // opt パラメータ取得
                        string optVal = GetQueryParam(query, "opt", null);
                        bool pointerEventsNone = (optVal == "pointer-events-none");

                        // ここで引数として渡す
                        transparentWindow.SetTransparent(enable, pointerEventsNone);

                        responseData.status = 200;
                        responseData.message = $"Transparent window set to {enable}, pointerEventsNone={pointerEventsNone}"
                            + (string.IsNullOrEmpty(hexColor) ? "" : $" with color {hexColor}");
                    }
                    else {
                        responseData.status = 500;
                        responseData.message = "TransparentWindow component not found.";
                    }

                    SendResponse(context, responseData);
                    break;
                }

            case "allowDragObjects": {
                    bool enableDrag = GetQueryParam(query, "enable", "false").ToLower() == "true";
                    if (transparentWindow != null) {
                        transparentWindow.SetAllowDragObjects(enableDrag);
                        responseData.status = 200;
                        responseData.message = $"allowDragObjects set to: {enableDrag}";
                    }
                    else {
                        responseData.status = 500;
                        responseData.message = "TransparentWindow component not found.";
                    }
                    SendResponse(context, responseData);
                    break;
                }

            case "stayOnTop": {
                    string enableParam = GetQueryParam(query, "enable", null);
                    if (string.IsNullOrEmpty(enableParam)) {
                        responseData.status = 400;
                        responseData.message = "Missing 'enable' parameter.";
                        SendResponse(context, responseData);
                        break;
                    }
                    if (!bool.TryParse(enableParam, out bool stayOnTopEnabled)) {
                        responseData.status = 400;
                        responseData.message = "Invalid 'enable' value. Use true or false.";
                        SendResponse(context, responseData);
                        break;
                    }

                    if (transparentWindow != null) {
                        transparentWindow.SetStayOnTop(stayOnTopEnabled);
                        responseData.status = 200;
                        responseData.message = $"StayOnTop set to {stayOnTopEnabled}";
                    }
                    else {
                        responseData.status = 500;
                        responseData.message = "TransparentWindow component not found.";
                    }
                    SendResponse(context, responseData);
                    break;
                }

            case "getstatus": {
                    var config = ServerConfig.Instance;

                    // AnimationServer 側の listener 状態チェック
                    var server = AnimationServer.Instance;
                    bool isHttpListening = server?.IsHttpListening ?? false;
                    bool isHttpsListening = server?.IsHttpsListening ?? false;

                    // 基本情報をまとめた result
                    var result = new Dictionary<string, object> {
                        { "httpPort", config.httpPort },
                        { "httpsPort", config.httpsPort },
                        { "useHttp", config.useHttp },
                        { "useHttps", config.useHttps },
                        { "isHttpListening", isHttpListening },
                        { "isHttpsListening", isHttpsListening },
                        { "listenLocalhostOnly", config.listenLocalhostOnly },
                        { "allowedRemoteIPs", config.allowedRemoteIPs },
                        { "vsync", config.vsync },
                        { "targetFramerate", config.targetFramerate }
                    };

                    // files 情報（listing:true なものだけ）
                    if (config.fileControl != null) {
                        var filesMap = new Dictionary<string, object>();
                        foreach (var fc in config.fileControl) {
                            if (fc.listing) {
                                var fileNames = ListFilesForKey(fc.key);
                                filesMap[fc.key] = fileNames;
                            }
                        }
                        if (filesMap.Count > 0) {
                            result["files"] = filesMap;
                        }
                    }

                    // ▼ window 情報を常にマージ（fltなしでも含む）
                    System.IntPtr hWnd = Process.GetCurrentProcess().MainWindowHandle;
                    int processId = Process.GetCurrentProcess().Id;

                    int x = 0, y = 0, w = 0, h = 0;
                    if (GetWindowRect(hWnd, out RECT rc)) {
                        x = rc.Left;
                        y = rc.Top;
                        w = rc.Right - rc.Left;
                        h = rc.Bottom - rc.Top;
                    }
                    int screenW = GetSystemMetrics(SM_CXSCREEN);
                    int screenH = GetSystemMetrics(SM_CYSCREEN);

                    result["windowHandle"] = hWnd.ToString();
                    result["processId"] = processId;
                    result["x"] = x;
                    result["y"] = y;
                    result["width"] = w;
                    result["height"] = h;
                    result["screenWidth"] = screenW;
                    result["screenHeight"] = screenH;

                    // ★★★【追加】ServerConfig.outputFilters の適用
                    ApplyOutputFilters(result, config.outputFilters);

                    // ここからフィルタ処理 (flt=... ) + limit=...
                    string flt = GetQueryParam(query, "flt", null);
                    int limit = GetQueryInt(query, "limit", -1); // -1 は制限なし

                    if (string.IsNullOrEmpty(flt)) {
                        // フィルタ指定なし => 全部返す + limitだけ適用
                        var finalAll = ApplyLimitIfNeeded(result, limit);
                        responseData.status = 200;
                        responseData.message = finalAll;
                        SendResponse(context, responseData);
                        break;
                    }

                    // 大文字小文字を気にしないために lower で比較
                    string fltLower = flt.ToLower();
                    var finalResult = new Dictionary<string, object>();

                    // --- 1) 特定フィルタ処理 ---
                    if (fltLower == "web") {
                        // http 関連のみ
                        if (result.ContainsKey("httpPort"))   finalResult["httpPort"] = result["httpPort"];
                        if (result.ContainsKey("httpsPort"))  finalResult["httpsPort"] = result["httpsPort"];
                        if (result.ContainsKey("useHttp"))    finalResult["useHttp"] = result["useHttp"];
                        if (result.ContainsKey("useHttps"))   finalResult["useHttps"] = result["useHttps"];
                    }
                    else if (fltLower == "files") {
                        // 全ファイルマップ
                        if (result.TryGetValue("files", out object filesObj)
                            && filesObj is Dictionary<string, object> filesMap) {
                            finalResult["files"] = filesMap;
                        }
                    }
                    else if (fltLower.StartsWith("files.")) {
                        // 例：files.vrm / files.img / files.vrma 等
                        string subKey = fltLower.Substring("files.".Length);
                        if (result.TryGetValue("files", out object filesObj)
                            && filesObj is Dictionary<string, object> filesDict) {
                            if (filesDict.ContainsKey(subKey)) {
                                var subDict = new Dictionary<string, object>();
                                subDict[subKey] = filesDict[subKey];
                                finalResult["files"] = subDict;
                            }
                        }
                    }
                    else if (fltLower == "window") {
                        // ウィンドウ情報だけ抜き出し
                        finalResult["windowHandle"] = result["windowHandle"];
                        finalResult["processId"] = result["processId"];
                        finalResult["x"] = result["x"];
                        finalResult["y"] = result["y"];
                        finalResult["width"] = result["width"];
                        finalResult["height"] = result["height"];
                        finalResult["screenWidth"] = result["screenWidth"];
                        finalResult["screenHeight"] = result["screenHeight"];
                    }
                    else {
                        // --- 2) それ以外のキー => 部分一致フィルタ ---
                        foreach (var kv in result) {
                            string topKey = kv.Key.ToLower();

                            bool topMatch = topKey.Contains(fltLower);
                            bool childMatch = false;

                            // 子に Dictionary<string, object> がある場合、キーを走査
                            if (kv.Value is Dictionary<string, object> subDict) {
                                foreach (var childK in subDict.Keys) {
                                    if (childK.ToLower().Contains(fltLower)) {
                                        childMatch = true;
                                        break;
                                    }
                                }
                            }
                            // 子が string[] などの場合は今回はスルー

                            if (topMatch || childMatch) {
                                finalResult[kv.Key] = kv.Value;
                            }
                        }
                    }

                    // --- 3) finalResult が空の場合は空オブジェクトを返す ---
                    if (finalResult.Count == 0) {
                        responseData.status = 200;
                        responseData.message = new Dictionary<string, object>();
                        SendResponse(context, responseData);
                        break;
                    }

                    // --- 4) limit の適用 ---
                    var finalWithLimit = ApplyLimitIfNeeded(finalResult, limit);

                    responseData.status = 200;
                    responseData.message = finalWithLimit;
                    SendResponse(context, responseData);
                    break;
                }

            case "terminate": {
                    responseData.status = 200;
                    responseData.message = i18nMsg.RESPONSE_SERVER_TERMINATE;
                    SendResponse(context, responseData);
                    UnityEngine.Debug.Log(i18nMsg.LOG_SERVER_SHUTDOWN_INITIATED);
                    // レスポンス返後に shutdown 呼び出し
                    if (AnimationServer.Instance != null) {
                        AnimationServer.Instance.InvokeShutdown();
                    }
                    break;
                }

            default: {
                    // デフォルト => 廃止コマンドとして 410 Gone
                    responseData.status = 410; // Gone
                    responseData.message = string.Format(i18nMsg.ERROR_DEPRECATED_COMMAND, cmd);
                    SendResponse(context, responseData);
                    break;
                }
        }
    }

    /// <summary>
    /// outputFilters リストに含まれているキーを result から削除するヘルパー
    /// ※トップレベルKeyのみ削除する想定
    /// </summary>
    private void ApplyOutputFilters(Dictionary<string, object> result, List<string> filters) {
        if (filters == null || filters.Count == 0) {
            return; // フィルタ未設定なら何もしない
        }
        var allKeys = new List<string>(result.Keys);
        for (int i = 0; i < allKeys.Count; i++) {
            string key = allKeys[i];
            // フィルタリストに含まれるかチェック(大文字小文字は区別する想定)
            // 区別しないなら ToLower() 同士で比較すればOK
            if (filters.Contains(key)) {
                result.Remove(key);
            }
        }
    }

    /// <summary>
    /// limitパラメータを適用して結果を切り詰めるヘルパー
    /// </summary>
    private Dictionary<string, object> ApplyLimitIfNeeded(Dictionary<string, object> source, int limit) {
        if (limit < 1) {
            // 制限なし => そのまま返す
            return source;
        }

        var limitedResult = new Dictionary<string, object>();
        foreach (var kv in source) {
            // キーごとに value をチェックして、配列やListなら .Take(limit)
            object val = kv.Value;
            object limitedVal = ApplyLimitToObject(val, limit);
            limitedResult[kv.Key] = limitedVal;
        }
        return limitedResult;
    }

    /// <summary>
    /// 配列/リスト/ディクショナリ等に limitをかける
    /// </summary>
    private object ApplyLimitToObject(object val, int limit) {
        if (val == null) {
            return null;
        }

        // (1) string[] や List<string> など IEnumerable<string>
        if (val is string[] arr) {
            return arr.Take(limit).ToArray();
        }
        if (val is List<string> strList) {
            return strList.Take(limit).ToList();
        }
        if (val is IEnumerable<object> objEnum) {
            return objEnum.Take(limit).ToList();
        }

        // (2) Dictionary<string,object>
        if (val is Dictionary<string, object> dict) {
            // TopレベルのKey-Valueを limit件ぶんだけ
            var limited = dict
                .Take(limit)
                .ToDictionary(p => p.Key, p => ApplyLimitToObject(p.Value, limit));
            return limited;
        }

        // (3) 他（int, bool, string 単体など）はそのまま
        return val;
    }

    /// <summary>
    /// [CDK-00300] 指定キーに応じて、対応するフォルダからファイル一覧を取得する簡易メソッド。
    /// </summary>
    private string[] ListFilesForKey(string key) {
        string folderPath;
        switch (key) {
            case "img":
                folderPath = UserPaths.GetFullPath("00_img");
                break;
            case "vrm":
                folderPath = UserPaths.GetFullPath("00_vrm");
                break;
            case "vrma":
                folderPath = UserPaths.GetFullPath("00_vrma");
                break;
            default:
                return new string[0];
        }

        if (!Directory.Exists(folderPath)) {
            UnityEngine.Debug.LogWarning($"Directory does not exist: {folderPath}");
            return new string[0];
        }

        // 拡張子フィルタ
        var allFiles = Directory.GetFiles(folderPath);
        var filtered = new List<string>();
        for (int i = 0; i < allFiles.Length; i++) {
            string f = allFiles[i].ToLower();
            if (f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg")
                || f.EndsWith(".vrm") || f.EndsWith(".vrma")) {
                filtered.Add(Path.GetFileName(allFiles[i]));
            }
        }
        return filtered.ToArray();
    }
}

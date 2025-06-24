using System.Collections.Specialized;
using System.Net;
using UnityEngine;
using System.Linq;

public class CameraCommandHandler : HttpCommandHandlerBase {
    private readonly VRMLoader _vrmLoader;
    // 許可するコマンドのみ定義
    private static readonly string[] AllowedCommands = { "orthographic", "adjust" };

    public CameraCommandHandler(VRMLoader vrmLoader) {
        _vrmLoader = vrmLoader;
    }

    public override void HandleCommand(HttpListenerContext context, NameValueCollection query) {
        var responseData = new ServerResponse();

        // cmd の取得と許可チェック（生の query["cmd"] は廃止）
        string cmd = GetCommandName(query, AllowedCommands, out bool ok);
        if (!ok) {
            responseData.status = 400;
            responseData.message = $"未対応の camera コマンドです: {cmd}";
            SendResponse(context, responseData);
            return;
        }

        // Main Camera のチェック
        var camera = Camera.main;
        if (camera == null) {
            responseData.status = 500;
            responseData.message = "Main Camera が見つかりませんでした。";
            SendResponse(context, responseData);
            return;
        }

        switch (cmd) {
            case "orthographic": {
                bool updated = false;
                string resultMsg = "";
                var config = ServerConfig.Instance;

                // enable パラメータの処理：存在していれば取得し、bool 型に変換
                string enableParam = GetQueryParam(query, "enable", null);
                if (!string.IsNullOrEmpty(enableParam)) {
                    if (bool.TryParse(enableParam, out bool enable)) {
                        camera.orthographic = enable;
                        config.Camera.orthographic = enable;
                        updated = true;
                        resultMsg += $"Orthographic を {enable} に設定しました。";
                    } else {
                        responseData.status = 400;
                        responseData.message = "enable パラメータは true または false を指定してください。";
                        SendResponse(context, responseData);
                        return;
                    }
                }

                // size パラメータの処理：存在していれば取得し、float 型に変換
                string sizeParam = GetQueryParam(query, "size", null);
                if (!string.IsNullOrEmpty(sizeParam)) {
                    if (float.TryParse(sizeParam, out float size)) {
                        if (size <= 0f) {
                            responseData.status = 400;
                            responseData.message = "size パラメータは正の数値を指定してください。";
                            SendResponse(context, responseData);
                            return;
                        }
                        camera.orthographicSize = size;
                        config.Camera.orthographicSize = size;
                        updated = true;
                        resultMsg += $" OrthographicSize を {size} に設定しました。";
                    } else {
                        responseData.status = 400;
                        responseData.message = "size パラメータが不正です。数値を指定してください。";
                        SendResponse(context, responseData);
                        return;
                    }
                }

                if (!updated) {
                    responseData.status = 400;
                    responseData.message = "enable または size パラメータが必要です。";
                    SendResponse(context, responseData);
                    return;
                }

                responseData.status = 200;
                responseData.message = resultMsg.Trim();
                SendResponse(context, responseData);
                return;
            }

            case "adjust": {
                if (_vrmLoader != null && _vrmLoader.VrmInstance != null) {
                    _vrmLoader.AdjustCameraFromExt();
                    responseData.status = 200;
                    responseData.message = "カメラ位置をVRMの頭部に合わせました。";
                } else {
                    responseData.status = 400;
                    responseData.message = "VRMがロードされていないため、adjust は使用できません。";
                }
                SendResponse(context, responseData);
                return;
            }

            default: {
                responseData.status = 400;
                responseData.message = $"未対応の camera コマンドです: {cmd}";
                SendResponse(context, responseData);
                return;
            }
        }
    }
}

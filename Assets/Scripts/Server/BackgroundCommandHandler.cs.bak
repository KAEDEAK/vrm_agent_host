using System.Collections.Specialized;
using System.Net;
using UnityEngine;
using System.IO;

public class BackgroundCommandHandler : HttpCommandHandlerBase {
    private LocalImageLoader _imageLoader;
    private static readonly string[] AllowedCommands = { "load", "fill" };

    public BackgroundCommandHandler(LocalImageLoader imageLoader) {
        _imageLoader = imageLoader;
    }

    public override void HandleCommand(HttpListenerContext context, NameValueCollection query) {
        var responseData = new ServerResponse();

        string cmd = GetCommandName(query, AllowedCommands, out bool ok);
        if (!ok) {
            responseData.status = 400;
            responseData.message = string.Format(i18nMsg.ERROR_INVALID_BACKGROUND_CMD, cmd);
            SendResponse(context, responseData);
            return;
        }

        switch (cmd) {
            case "load":
                _imageLoader.EnableCanvas(true);

                // GetQueryParam を使用して "file" パラメータ取得
                string file = GetQueryParam(query, "file", null);
                if (string.IsNullOrEmpty(file)) {
                    responseData.status = 400;
                    responseData.message = i18nMsg.RESPONSE_FILE_PARAM_MISSING;
                }
                else {
                    // 修正箇所：UserPaths.GetIMGFilePath を用いてフルパスを取得
                    string fullPath = UserPaths.GetIMGFilePath(file);

                    if (!File.Exists(fullPath)) {
                        responseData.status = 404;
                        responseData.message = string.Format(i18nMsg.BACKGROUND_FILE_NOT_FOUND, file);
                    }
                    else if (!_imageLoader.LoadImageToCanvas(fullPath)) {
                        responseData.status = 500;
                        responseData.message = string.Format(i18nMsg.ERROR_IMAGE_LOAD_FAILURE, fullPath);
                    }
                    else {
                        responseData.status = 200;
                        responseData.message = string.Format(i18nMsg.RESPONSE_BACKGROUND_IMAGE_CHANGED, file);
                    }
                }
                break;

            case "fill":
                _imageLoader.EnableCanvas(false); // UI Canvas は不要なので無効化

                // GetQueryParam を使用して "color" パラメータ取得（なければ "#000000"）
                string hexColor = GetQueryParam(query, "color", "#000000");
                if (!ColorUtility.TryParseHtmlString(hexColor, out Color fillColor)) {
                    responseData.status = 400;
                    responseData.message = $"Invalid color format: {hexColor}";
                    break;
                }

                // Camera.main.backgroundColor へ Color32 形式で設定
                Camera.main.backgroundColor = (Color32)fillColor;

                responseData.status = 200;
                responseData.message = $"Camera backgroundColor set to {hexColor}";
                break;

            /*
            case "fill_image(deprecated)":
                string hexColor = GetQueryParam(query, "color", "#000000");

                if (!ColorUtility.TryParseHtmlString(hexColor, out Color fillColor)) {
                    responseData.status = 400;
                    responseData.message = $"Invalid color format: {hexColor}";
                    break;
                }

                // 指定サイズの単色テクスチャを生成
                const int width = 1920;
                const int height = 1080;
                Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                Color[] pixels = new Color[width * height];
                for (int i = 0; i < pixels.Length; i++) {
                    pixels[i] = fillColor;
                }
                texture.SetPixels(pixels);
                texture.Apply();

                // 背景に適用（SpriteとしてCanvas Imageに貼る）
                if (!_imageLoader.LoadTextureAsSprite(texture)) {
                    responseData.status = 500;
                    responseData.message = "Failed to apply filled background.";
                } else {
                    responseData.status = 200;
                    responseData.message = $"Background filled with color {hexColor}";
                }
                break;
            */

            default:
                responseData.status = 400;
                responseData.message = string.Format(i18nMsg.ERROR_INVALID_BACKGROUND_CMD, cmd);
                break;
        }

        SendResponse(context, responseData);
    }
}

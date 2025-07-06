using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using UnityEngine;

public class WingMenuCommandHandler : HttpCommandHandlerBase
{
    private static readonly string[] AllowedCommands = {
        "menus_show", "menus_hide", "menus_define", "menus_clear", 
        "config", "shape", "rotate", "position", "scale", "menus_status", "color"
    };

    private static readonly string[] BuiltinFunctions = {
        "reset_pose", "reset_shape", "exit"
    };

    public override void HandleCommand(HttpListenerContext context, NameValueCollection query)
    {
        var responseData = new ServerResponse();
        string cmd = GetCommandName(query, AllowedCommands, out bool ok);
        
        if (!ok) {
            responseData.status = 400;
            responseData.succeeded = false;
            responseData.message = $"Invalid wingsys command: {cmd}";
            SendResponse(context, responseData);
            return;
        }

        var wingMenuSystem = GameObject.FindFirstObjectByType<WingMenuSystem>();
        if (wingMenuSystem == null) {
            responseData.status = 500;
            responseData.succeeded = false;
            responseData.message = "WingMenuSystem not found";
            SendResponse(context, responseData);
            return;
        }

        try {
            switch (cmd) {
                case "menus_show":
                    HandleMenusShow(wingMenuSystem, query, responseData);
                    break;
                    
                case "menus_hide":
                    HandleMenusHide(wingMenuSystem, query, responseData);
                    break;
                    
                case "menus_define":
                    HandleMenusDefine(wingMenuSystem, query, responseData);
                    break;
                    
                case "menus_clear":
                    HandleMenusClear(wingMenuSystem, query, responseData);
                    break;
                    
                case "config":
                    HandleConfig(wingMenuSystem, query, responseData);
                    break;
                    
                case "shape":
                    HandleShape(wingMenuSystem, query, responseData);
                    break;
                    
                case "rotate":
                case "position":
                case "scale":
                    HandleTransform(wingMenuSystem, query, responseData, cmd);
                    break;
                    
                case "menus_status":
                    HandleMenusStatus(wingMenuSystem, query, responseData);
                    break;
                    
                case "color":
                    HandleColor(wingMenuSystem, query, responseData);
                    break;
                    
                default:
                    responseData.status = 400;
                    responseData.succeeded = false;
                    responseData.message = $"Unhandled command: {cmd}";
                    break;
            }
        }
        catch (Exception ex) {
            Debug.LogError($"[WingMenuCommandHandler] Error handling command '{cmd}': {ex.Message}");
            responseData.status = 500;
            responseData.succeeded = false;
            responseData.message = $"Internal error: {ex.Message}";
        }
        
        SendResponse(context, responseData);
    }

    private void HandleMenusShow(WingMenuSystem wingMenuSystem, NameValueCollection query, ServerResponse responseData)
    {
        string side = GetQueryParam(query, "side", null);
        
        // MainThreadInvokerを使用してメインスレッドで実行
        MainThreadInvoker.Invoke(() => {
            wingMenuSystem.ShowMenuViaHttp(side);
        });
        
        responseData.status = 200;
        responseData.succeeded = true;
        responseData.message = $"Menu shown (side: {side ?? "both"})";
    }

    private void HandleMenusHide(WingMenuSystem wingMenuSystem, NameValueCollection query, ServerResponse responseData)
    {
        string side = GetQueryParam(query, "side", null);
        
        MainThreadInvoker.Invoke(() => {
            wingMenuSystem.HideMenuViaHttp(side);
        });
        
        responseData.status = 200;
        responseData.succeeded = true;
        responseData.message = $"Menu hidden (side: {side ?? "both"})";
    }

    private void HandleMenusDefine(WingMenuSystem wingMenuSystem, NameValueCollection query, ServerResponse responseData)
    {
        string menusParam = GetQueryParam(query, "menus", null);
        string leftParam = GetQueryParam(query, "menu_left", null);
        string rightParam = GetQueryParam(query, "menu_right", null);
        
        if (string.IsNullOrEmpty(menusParam) && string.IsNullOrEmpty(leftParam) && string.IsNullOrEmpty(rightParam)) {
            responseData.status = 400;
            responseData.succeeded = false;
            responseData.message = "Missing menu definition parameters (menus, menu_left, or menu_right)";
            return;
        }
        
        MainThreadInvoker.Invoke(() => {
            wingMenuSystem.DefineMenusViaHttp(menusParam, leftParam, rightParam);
        });
        
        responseData.status = 200;
        responseData.succeeded = true;
        responseData.message = "Menu definitions updated";
    }

    private void HandleMenusClear(WingMenuSystem wingMenuSystem, NameValueCollection query, ServerResponse responseData)
    {
        MainThreadInvoker.Invoke(() => {
            wingMenuSystem.ClearMenusViaHttp();
        });
        
        responseData.status = 200;
        responseData.succeeded = true;
        responseData.message = "Menu cleared to default (exit only)";
    }

    private void HandleConfig(WingMenuSystem wingMenuSystem, NameValueCollection query, ServerResponse responseData)
    {
        int? leftLength = null;
        int? rightLength = null;
        float? angleDelta = null;
        float? angleStart = null;
        
        string leftLengthParam = GetQueryParam(query, "left_length", null);
        if (!string.IsNullOrEmpty(leftLengthParam)) {
            int value = GetQueryInt(query, "left_length", 4);
            if (value >= 1 && value <= 16) {
                leftLength = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "left_length must be between 1 and 16";
                return;
            }
        }
        
        string rightLengthParam = GetQueryParam(query, "right_length", null);
        if (!string.IsNullOrEmpty(rightLengthParam)) {
            int value = GetQueryInt(query, "right_length", 4);
            if (value >= 1 && value <= 16) {
                rightLength = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "right_length must be between 1 and 16";
                return;
            }
        }
        
        string angleDeltaParam = GetQueryParam(query, "angle_delta", null);
        if (!string.IsNullOrEmpty(angleDeltaParam)) {
            float value = GetQueryFloat(query, "angle_delta", 20f);
            if (value >= -360f && value <= 360f) {
                angleDelta = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "angle_delta must be between -360 and 360";
                return;
            }
        }
        
        string angleStartParam = GetQueryParam(query, "angle_start", null);
        if (!string.IsNullOrEmpty(angleStartParam)) {
            float value = GetQueryFloat(query, "angle_start", 0f);
            if (value >= -360f && value <= 360f) {
                angleStart = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "angle_start must be between -360 and 360";
                return;
            }
        }
        
        MainThreadInvoker.Invoke(() => {
            wingMenuSystem.ConfigureWingsViaHttp(leftLength, rightLength, angleDelta, angleStart);
        });
        
        responseData.status = 200;
        responseData.succeeded = true;
        responseData.message = "Wing configuration updated";
    }

    private void HandleShape(WingMenuSystem wingMenuSystem, NameValueCollection query, ServerResponse responseData)
    {
        // 共通パラメータ
        float? bladeLength = null;
        float? bladeEdge = null;
        float? bladeModifier = null;
        
        // 左右独立パラメータ
        float? bladeLeftLength = null;
        float? bladeLeftEdge = null;
        float? bladeLeftModifier = null;
        float? bladeRightLength = null;
        float? bladeRightEdge = null;
        float? bladeRightModifier = null;
        
        // 共通パラメータの処理
        string bladeLengthParam = GetQueryParam(query, "blade_length", null);
        if (!string.IsNullOrEmpty(bladeLengthParam)) {
            float value = GetQueryFloat(query, "blade_length", 1.0f);
            if (value >= 0.1f && value <= 3.0f) {
                bladeLength = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "blade_length must be between 0.1 and 3.0";
                return;
            }
        }
        
        string bladeEdgeParam = GetQueryParam(query, "blade_edge", null);
        if (!string.IsNullOrEmpty(bladeEdgeParam)) {
            float value = GetQueryFloat(query, "blade_edge", 0.5f);
            if (value >= 0.01f && value <= 1.0f) {
                bladeEdge = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "blade_edge must be between 0.01 and 1.0";
                return;
            }
        }
        
        string bladeModifierParam = GetQueryParam(query, "blade_modifier", null);
        if (!string.IsNullOrEmpty(bladeModifierParam)) {
            float value = GetQueryFloat(query, "blade_modifier", 0.0f);
            if (value >= 0.0f && value <= 0.5f) {
                bladeModifier = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "blade_modifier must be between 0.0 and 0.5";
                return;
            }
        }
        
        // 左側独立パラメータの処理
        string bladeLeftLengthParam = GetQueryParam(query, "blade_left_length", null);
        if (!string.IsNullOrEmpty(bladeLeftLengthParam)) {
            float value = GetQueryFloat(query, "blade_left_length", 1.0f);
            if (value >= 0.1f && value <= 3.0f) {
                bladeLeftLength = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "blade_left_length must be between 0.1 and 3.0";
                return;
            }
        }
        
        string bladeLeftEdgeParam = GetQueryParam(query, "blade_left_edge", null);
        if (!string.IsNullOrEmpty(bladeLeftEdgeParam)) {
            float value = GetQueryFloat(query, "blade_left_edge", 0.5f);
            if (value >= 0.01f && value <= 1.0f) {
                bladeLeftEdge = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "blade_left_edge must be between 0.01 and 1.0";
                return;
            }
        }
        
        string bladeLeftModifierParam = GetQueryParam(query, "blade_left_modifier", null);
        if (!string.IsNullOrEmpty(bladeLeftModifierParam)) {
            float value = GetQueryFloat(query, "blade_left_modifier", 0.0f);
            if (value >= 0.0f && value <= 0.5f) {
                bladeLeftModifier = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "blade_left_modifier must be between 0.0 and 0.5";
                return;
            }
        }
        
        // 右側独立パラメータの処理
        string bladeRightLengthParam = GetQueryParam(query, "blade_right_length", null);
        if (!string.IsNullOrEmpty(bladeRightLengthParam)) {
            float value = GetQueryFloat(query, "blade_right_length", 1.0f);
            if (value >= 0.1f && value <= 3.0f) {
                bladeRightLength = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "blade_right_length must be between 0.1 and 3.0";
                return;
            }
        }
        
        string bladeRightEdgeParam = GetQueryParam(query, "blade_right_edge", null);
        if (!string.IsNullOrEmpty(bladeRightEdgeParam)) {
            float value = GetQueryFloat(query, "blade_right_edge", 0.5f);
            if (value >= 0.01f && value <= 1.0f) {
                bladeRightEdge = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "blade_right_edge must be between 0.01 and 1.0";
                return;
            }
        }
        
        string bladeRightModifierParam = GetQueryParam(query, "blade_right_modifier", null);
        if (!string.IsNullOrEmpty(bladeRightModifierParam)) {
            float value = GetQueryFloat(query, "blade_right_modifier", 0.0f);
            if (value >= 0.0f && value <= 0.5f) {
                bladeRightModifier = value;
            } else {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = "blade_right_modifier must be between 0.0 and 0.5";
                return;
            }
        }
        
        // 左右独立パラメータが指定されているかチェック
        bool hasIndependentParams = bladeLeftLength.HasValue || bladeLeftEdge.HasValue || bladeLeftModifier.HasValue ||
                                   bladeRightLength.HasValue || bladeRightEdge.HasValue || bladeRightModifier.HasValue;
        
        MainThreadInvoker.Invoke(() => {
            if (hasIndependentParams) {
                // 左右独立設定
                wingMenuSystem.ConfigureShapeIndependentViaHttp(
                    bladeLeftLength, bladeLeftEdge, bladeLeftModifier,
                    bladeRightLength, bladeRightEdge, bladeRightModifier);
            } else {
                // 共通設定
                wingMenuSystem.ConfigureShapeViaHttp(bladeLength, bladeEdge, bladeModifier);
            }
        });
        
        responseData.status = 200;
        responseData.succeeded = true;
        responseData.message = hasIndependentParams ? 
            "Wing shape configuration updated (independent left/right)" : 
            "Wing shape configuration updated";
    }

    private void HandleTransform(WingMenuSystem wingMenuSystem, NameValueCollection query, ServerResponse responseData, string transformType)
    {
        string xyzParam = GetQueryParam(query, "xyz", null);
        if (string.IsNullOrEmpty(xyzParam)) {
            responseData.status = 400;
            responseData.succeeded = false;
            responseData.message = "Missing xyz parameter";
            return;
        }
        
        if (!TryParseVector3(xyzParam, out Vector3 vector)) {
            responseData.status = 400;
            responseData.succeeded = false;
            responseData.message = $"Invalid xyz format: {xyzParam}. Expected format: x,y,z";
            return;
        }
        
        MainThreadInvoker.Invoke(() => {
            Vector3? position = transformType == "position" ? vector : null;
            Vector3? rotation = transformType == "rotate" ? vector : null;
            Vector3? scale = transformType == "scale" ? vector : null;
            
            wingMenuSystem.SetTransformViaHttp(position, rotation, scale);
        });
        
        responseData.status = 200;
        responseData.succeeded = true;
        responseData.message = $"Transform {transformType} set to {vector}";
    }

    private void HandleMenusStatus(WingMenuSystem wingMenuSystem, NameValueCollection query, ServerResponse responseData)
    {
        // ステータス取得は読み取り専用なので直接実行
        // （MainThreadInvokerは非同期のため、同期実行が必要な場合は直接呼び出し）
        Dictionary<string, object> status = wingMenuSystem.GetMenuStatusViaHttp();
        
        responseData.status = 200;
        responseData.succeeded = true;
        responseData.message = status;
    }

    private void HandleColor(WingMenuSystem wingMenuSystem, NameValueCollection query, ServerResponse responseData)
    {
        string valuesParam = GetQueryParam(query, "values", null);
        if (string.IsNullOrEmpty(valuesParam)) {
            responseData.status = 400;
            responseData.succeeded = false;
            responseData.message = "Missing values parameter";
            return;
        }
        
        string[] colorValues = valuesParam.Split(',');
        if (colorValues.Length != 4) {
            responseData.status = 400;
            responseData.succeeded = false;
            responseData.message = "values must contain exactly 4 color specifications (normal,animation,hover_no_command,hover_with_command)";
            return;
        }
        
        // 色指定の妥当性をチェック
        string[] validColors = { "white", "gaming", "lightblue", "yellow", "red", "green", "blue", "black" };
        for (int i = 0; i < colorValues.Length; i++) {
            string color = colorValues[i].Trim().ToLower();
            colorValues[i] = color; // 正規化された値で更新
            
            bool isValid = false;
            foreach (string validColor in validColors) {
                if (color == validColor) {
                    isValid = true;
                    break;
                }
            }
            
            if (!isValid) {
                responseData.status = 400;
                responseData.succeeded = false;
                responseData.message = $"Invalid color '{color}' at position {i + 1}. Valid colors: {string.Join(", ", validColors)}";
                return;
            }
        }
        
        MainThreadInvoker.Invoke(() => {
            wingMenuSystem.SetColorsViaHttp(colorValues[0], colorValues[1], colorValues[2], colorValues[3]);
        });
        
        responseData.status = 200;
        responseData.succeeded = true;
        responseData.message = $"Wing colors updated: normal={colorValues[0]}, animation={colorValues[1]}, hover_no_command={colorValues[2]}, hover_with_command={colorValues[3]}";
    }

    private bool TryParseVector3(string xyz, out Vector3 result)
    {
        result = Vector3.zero;
        if (string.IsNullOrEmpty(xyz)) return false;
        
        string[] parts = xyz.Split(',');
        if (parts.Length != 3) return false;
        
        if (float.TryParse(parts[0].Trim(), out float x) &&
            float.TryParse(parts[1].Trim(), out float y) &&
            float.TryParse(parts[2].Trim(), out float z))
        {
            result = new Vector3(x, y, z);
            return true;
        }
        return false;
    }

    public static bool IsBuiltinFunction(string functionName)
    {
        if (string.IsNullOrEmpty(functionName)) return false;
        
        foreach (string builtin in BuiltinFunctions) {
            if (string.Equals(functionName, builtin, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        return false;
    }
}

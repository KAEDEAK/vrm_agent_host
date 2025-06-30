using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using UnityEngine;
using UniVRM10;

public class AnimationCommandHandler : HttpCommandHandlerBase
{
    private AnimationHandler _animationHandler;
    private VRMLoader _vrmLoader; // VRMを参照するために必要

    // ▼▼▼ 自動瞬き管理用 ▼▼▼
    private bool _autoBlinkEnabled = false;   // 現在の自動瞬き On/Off
    private float _autoBlinkFreqMs = 2000f;     // デフォルト: 2秒間隔(2000ms)
    private Coroutine _autoBlinkRoutine = null;
    private System.Random _random = new System.Random();
    private System.Diagnostics.Stopwatch blinkSw = new System.Diagnostics.Stopwatch();
    private static readonly string[] AllowedCommands = {
        "reset", "play", "stop", "resume", "getstatus",
        "shape", "mouth", "autoPrepareSeamless", "getAutoPrepareSeamless",
        "reset_blink", "reset_mouth", "autoBlink"
    };

    public AnimationCommandHandler(AnimationHandler animationHandler, VRMLoader vrmLoader)
    {
        _animationHandler = animationHandler;
        _vrmLoader = vrmLoader;
    }
    
    private string DebugExpressionKey(ExpressionKey key)
    {
        return $"{{ \"Preset\": \"{key.Preset}\", \"Name\": \"{key.Name}\" }}";
    }

    #region HandleCommand
    public override void HandleCommand(HttpListenerContext context, NameValueCollection query)
    {
        var responseData = new ServerResponse();
        string cmd = GetCommandName(query, AllowedCommands, out bool ok);
        if (!ok)
        {
            responseData.status = 400;
            responseData.message = string.Format(i18nMsg.ERROR_INVALID_ANIMATION_COMMAND, cmd);
            SendResponse(context, responseData);
            return;
        }

        switch (cmd)
        {
            #region 1) AGIAリセット / 再生 / 停止 / 再開 / ステータス
            case "reset":
                if (!_animationHandler.IsInitialized)
                {
                    responseData.status = 503;
                    responseData.message = i18nMsg.RESPONSE_ANIMATION_SYSTEM_NOT_INITIALIZED;
                }
                else
                {
                    _animationHandler.ResetAGIAAnimation();
                    responseData.status = 200;
                    responseData.message = i18nMsg.RESPONSE_AGIA_RESET;
                }
                break;

            case "play":
            {
                // vrmaファイル指定の場合 (.vrma で終わるなら)
                string fileParam = GetQueryParam(query, "file", null);
                if (!string.IsNullOrEmpty(fileParam) && fileParam.EndsWith(".vrma", StringComparison.OrdinalIgnoreCase))
                {
                    bool shouldLoop = GetQueryYesNo(query, "continue", false);
                    _animationHandler.PlayVrmaAnimation(fileParam, shouldLoop);
                    responseData.status = 200;
                    responseData.message = string.Format(i18nMsg.RESPONSE_VRMA_ANIMATION_STARTED, fileParam, shouldLoop);
                }
                else
                {
                    if (!_animationHandler.IsInitialized)
                    {
                        responseData.status = 503;
                        responseData.message = i18nMsg.RESPONSE_ANIMATION_SYSTEM_NOT_INITIALIZED;
                    }
                    else
                    {
                        // 通常のアニメーション再生の場合
                        string idParam = GetQueryParam(query, "id", null);
                        if (string.IsNullOrEmpty(idParam))
                        {
                            responseData.status = 400;
                            responseData.message = i18nMsg.RESPONSE_ANIMATION_ID_NOT_SPECIFIED;
                        }
                        else
                        {
                            string category = "Idle";
                            string aliasPart = idParam;
                            if (idParam.Contains("_"))
                            {
                                string[] parts = idParam.Split(new char[] { '_' }, 2);
                                category = parts[0];
                                aliasPart = parts[1];
                            }

                            var config = ServerConfig.Instance;
                            var mappingEntry = config.GetMappingByAlias(category, aliasPart);
                            if (mappingEntry == null)
                            {
                                responseData.status = 400;
                                responseData.message = string.Format(i18nMsg.RESPONSE_INVALID_ANIMATION_ID, idParam);
                            }
                            else
                            {
                                // seamless は y/n 仕様に対応
                                bool seamless = GetQueryYesNo(query, "seamless", false);
                                if (mappingEntry.Value.type == AnimationType.IntBased)
                                {
                                    _animationHandler.PlayAnimationByID(mappingEntry.Value.intValue, category, seamless);
                                }
                                else if (mappingEntry.Value.type == AnimationType.PlayBased)
                                {
                                    _animationHandler.PlayAnimationByState(mappingEntry.Value.stateName, mappingEntry.Value.playLayer, mappingEntry.Value.normalizedTime);
                                }
                                responseData.status = 200;
                                responseData.message = seamless ? i18nMsg.RESPONSE_SEAMLESS_ANIMATION_STARTED
                                                                : i18nMsg.RESPONSE_ANIMATION_STARTED;
                            }
                        }
                    }
                }
                break;
            }

            case "stop":
                _animationHandler.StopAnimation();
                responseData.status = 200;
                responseData.message = i18nMsg.RESPONSE_ANIMATION_STOP;
                break;

            case "resume":
                if (!_animationHandler.IsInitialized)
                {
                    responseData.status = 503;
                    responseData.message = i18nMsg.RESPONSE_ANIMATION_SYSTEM_NOT_INITIALIZED;
                }
                else
                {
                    _animationHandler.ResumeAnimation();
                    responseData.status = 200;
                    responseData.message = i18nMsg.RESPONSE_ANIMATION_STARTED;
                }
                break;

            case "getstatus":
            {
                var serverConfig = ServerConfig.Instance;
                string opt = GetQueryParam(query, "opt", null);
                List<string> defaultKeys;

                if (!string.IsNullOrEmpty(opt) && opt.ToLower() == "alias")
                {
                    defaultKeys = serverConfig.GetDefaultMappingKeys();
                }
                else
                {
                    defaultKeys = serverConfig.GetLogicalKeys();
                }

                string innerJson = _animationHandler.GetAnimationStatusJson();
                string keysJson = "\"defaultMappingKeys\":" + SimpleJsonBuilder.Serialize(defaultKeys);
                string mergedJson = innerJson.TrimEnd('}') + "," + keysJson + "}";
                responseData.status = 200;
                responseData.message = new RawJson(mergedJson);
                break;
            }
            #endregion

            #region 2) shape & mouth (表情系)
            case "shape":
            {
                string blinkParam = GetQueryParam(query, "blink", null);
                string shapeKey = GetQueryParam(query, "word", null);

                if (string.IsNullOrEmpty(blinkParam) && string.IsNullOrEmpty(shapeKey))
                {
                    responseData.status = 400;
                    responseData.message = i18nMsg.RESPONSE_SHAPE_PARAM_REQUIRED;
                    break;
                }

                if (_vrmLoader == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_VRMLOADER_NOT_FOUND;
                    break;
                }
                var instance = _vrmLoader.VrmInstance;
                if (instance == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_VRM_MODEL_NOT_LOADED;
                    break;
                }
                var expression = instance.Runtime.Expression;
                if (expression == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_EXPRESSION_SYSTEM_NOT_AVAILABLE;
                    break;
                }

                // ▼ blink param 処理
                if (!string.IsNullOrEmpty(blinkParam))
                {
                    if (blinkParam.Equals("reset", StringComparison.OrdinalIgnoreCase))
                    {
                        bool blinkSeamless = GetQueryYesNo(query, "seamless", false);
                        if (blinkSeamless)
                        {
                            _animationHandler.StartCoroutine(SeamlessResetBlink(expression, 0.3f));
                            responseData.status = 200;
                            responseData.message = i18nMsg.RESPONSE_SEAMLESS_BLINK_RESET_STARTED;
                        }
                        else
                        {
                            expression.SetWeight(ExpressionKey.BlinkLeft, 0.0f);
                            expression.SetWeight(ExpressionKey.BlinkRight, 0.0f);
                            responseData.status = 200;
                            responseData.message = i18nMsg.RESPONSE_BLINK_RESET_ZERO;
                        }
                        break;
                    }
                    else
                    {
                        // {1.0,1.0} 等のパース
                        try
                        {
                            string raw = blinkParam.Trim().Trim('{', '}');
                            var parts = raw.Split(',');
                            float blinkLeft = 0f, blinkRight = 0f;

                            if (parts.Length == 1)
                            {
                                blinkLeft = float.Parse(parts[0]);
                                blinkRight = blinkLeft;
                            }
                            else if (parts.Length >= 2)
                            {
                                blinkLeft = float.Parse(parts[0]);
                                blinkRight = float.Parse(parts[1]);
                            }

                            Debug.Log($"[AnimationCommandHandler] blinkParam parsed: raw='{raw}', blinkLeft={blinkLeft}, blinkRight={blinkRight}");

                            bool blinkSeamless = GetQueryYesNo(query, "seamless", false);
                            if (blinkSeamless)
                            {
                                _animationHandler.StartCoroutine(InterpolateBlinkLR(expression, blinkLeft, blinkRight, 0.3f));
                                responseData.status = 200;
                                responseData.message = string.Format(i18nMsg.RESPONSE_SEAMLESS_BLINK_SET, blinkParam);
                            }
                            else
                            {
                                expression.SetWeight(ExpressionKey.BlinkLeft, blinkLeft);
                                expression.SetWeight(ExpressionKey.BlinkRight, blinkRight);
                                responseData.status = 200;
                                responseData.message = string.Format(i18nMsg.RESPONSE_BLINK_APPLIED, blinkParam);
                            }
                        }
                        catch (Exception ex)
                        {
                            responseData.status = 400;
                            responseData.message = string.Format(i18nMsg.ERROR_INVALID_BLINK_PARAM, blinkParam, ex.Message);
                        }
                        break;
                    }
                }

                // ▼ mouth / word 処理
                if (shapeKey.ToLower() == "reset")
                {
                    bool seamlessParamShape = GetQueryYesNo(query, "seamless", false);
                    if (seamlessParamShape)
                    {
                        _animationHandler.StartCoroutine(SeamlessResetExpressionWeights(expression, 0.3f));
                        responseData.status = 200;
                        responseData.message = i18nMsg.RESPONSE_SEAMLESS_RESET_BLENDSHAPES_STARTED;
                    }
                    else
                    {
                        foreach (var k in expression.ExpressionKeys)
                        {
                            expression.SetWeight(k, 0.0f);
                        }
                        responseData.status = 200;
                        responseData.message = i18nMsg.RESPONSE_ALL_BLENDSHAPES_RESET;
                    }
                }
                else
                {
                    if (Enum.TryParse(shapeKey, true, out ExpressionPreset presetKey))
                    {
                        ExpressionKey key = ExpressionKey.CreateFromPreset(presetKey);
                        bool seamlessParamShape = GetQueryYesNo(query, "seamless", false);
                        if (seamlessParamShape)
                        {
                            _animationHandler.StartCoroutine(InterpolateExpressionWeight(expression, key, 0.3f));
                            responseData.status = 200;
                            responseData.message = string.Format(i18nMsg.RESPONSE_SEAMLESS_BLENDSHAPE_APPLIED_ALT, shapeKey);
                        }
                        else
                        {
                            expression.SetWeight(key, 1.0f);
                            responseData.status = 200;
                            responseData.message = string.Format(i18nMsg.RESPONSE_BLENDSHAPE_APPLIED_ALT, shapeKey);
                        }
                    }
                    else
                    {
                        responseData.status = 400;
                        responseData.message = string.Format(i18nMsg.ERROR_INVALID_BLENDSHAPE_NAME_ALT, shapeKey);
                    }
                }
                break;
            }
            #endregion

            #region 3) mouth
            case "mouth":
            {
                string word = GetQueryParam(query, "word", null);
                if (string.IsNullOrEmpty(word))
                {
                    responseData.status = 400;
                    responseData.message = i18nMsg.RESPONSE_MOUTH_PARAM_REQUIRED;
                    break;
                }
                if (_vrmLoader == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_VRMLOADER_NOT_FOUND;
                    break;
                }
                var instance = _vrmLoader.VrmInstance;
                if (instance == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_VRM_MODEL_NOT_LOADED;
                    break;
                }
                var expression = instance.Runtime.Expression;
                if (expression == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_EXPRESSION_SYSTEM_NOT_AVAILABLE;
                    break;
                }

                ExpressionKey key;
                switch (word.ToUpper())
                {
                    case "A": key = ExpressionKey.Aa; break;
                    case "I": key = ExpressionKey.Ih; break;
                    case "U": key = ExpressionKey.Ou; break;
                    case "E": key = ExpressionKey.Ee; break;
                    case "O": key = ExpressionKey.Oh; break;

                    case "RESET":
                    {
                        bool seamlessParamMouth = GetQueryYesNo(query, "seamless", false);
                        if (seamlessParamMouth)
                        {
                            _animationHandler.StartCoroutine(SeamlessResetExpressionWeights(expression, 0.3f));
                            responseData.status = 200;
                            responseData.message = i18nMsg.RESPONSE_SEAMLESS_MOUTH_RESET_STARTED_ALT;
                        }
                        else
                        {
                            foreach (var exKey in expression.ExpressionKeys)
                            {
                                expression.SetWeight(exKey, 0.0f);
                            }
                            responseData.status = 200;
                            responseData.message = i18nMsg.RESPONSE_MOUTH_ALL_RESET_ALT;
                        }
                        goto EndSwitch;
                    }

                    default:
                        responseData.status = 400;
                        responseData.message = string.Format(i18nMsg.ERROR_UNSUPPORTED_MOUTH_SHAPE_ALT, word);
                        goto EndSwitch;
                }
                {
                    // 通常の A,I,U,E,O
                    Debug.Log(DebugExpressionKey(key));
                    bool seamlessParamMouth2 = GetQueryYesNo(query, "seamless", false);
                    if (seamlessParamMouth2)
                    {
                        _animationHandler.StartCoroutine(InterpolateExpressionWeight(expression, key, 0.3f));
                        responseData.status = 200;
                        responseData.message = string.Format(i18nMsg.RESPONSE_SEAMLESS_MOUTH_ANIMATION_ALT, word);
                    }
                    else
                    {
                        expression.SetWeight(key, 1.0f);
                        responseData.status = 200;
                        responseData.message = string.Format(i18nMsg.RESPONSE_MOUTH_ANIMATION_ALT, word);
                    }
                }
                break;
            }
            #endregion

            #region 4) autoPrepareSeamless / getAutoPrepareSeamless
            case "autoPrepareSeamless":
            {
                string enableParam = GetQueryParam(query, "enable", null);
                if (string.IsNullOrEmpty(enableParam))
                {
                    responseData.status = 400;
                    responseData.message = i18nMsg.ERROR_ENABLE_PARAM_MISSING;
                    break;
                }
                if (!bool.TryParse(enableParam, out bool enable))
                {
                    responseData.status = 400;
                    responseData.message = i18nMsg.ERROR_ENABLE_PARAM_INVALID;
                    break;
                }

                var configInstance = ServerConfig.Instance;
                configInstance.SetAutoPrepareSeamless(enable);
                responseData.status = 200;
                responseData.message = string.Format(i18nMsg.RESPONSE_AUTO_PREPARE_SEAMLESS_SET, enable);
                break;
            }

            case "getAutoPrepareSeamless":
            {
                var serverConfigGet = ServerConfig.Instance;
                responseData.status = 200;
                responseData.message = string.Format(i18nMsg.RESPONSE_AUTO_PREPARE_SEAMLESS_STATUS, serverConfigGet.GetAutoPrepareSeamless());
                break;
            }
            #endregion

            #region 5) reset_blink / reset_mouth（追加のリセット系コマンド）
            case "reset_blink":
            {
                if (_vrmLoader == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_VRMLOADER_NOT_FOUND;
                    break;
                }
                var instance = _vrmLoader.VrmInstance;
                if (instance == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_VRM_MODEL_NOT_LOADED;
                    break;
                }
                var expression = instance.Runtime.Expression;
                if (expression == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_EXPRESSION_SYSTEM_NOT_AVAILABLE;
                    break;
                }
                bool blinkSeamless = GetQueryYesNo(query, "seamless", false);
                if (blinkSeamless)
                {
                    _animationHandler.StartCoroutine(SeamlessResetBlink(expression, 0.3f));
                    responseData.message = i18nMsg.RESPONSE_SEAMLESS_BLINK_RESET_CMD;
                }
                else
                {
                    expression.SetWeight(ExpressionKey.BlinkLeft, 0.0f);
                    expression.SetWeight(ExpressionKey.BlinkRight, 0.0f);
                    responseData.message = i18nMsg.RESPONSE_BLINK_RESET_DONE_CMD;
                }
                responseData.status = 200;
                break;
            }

            case "reset_mouth":
            {
                if (_vrmLoader == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_VRMLOADER_NOT_FOUND;
                    break;
                }
                var instance = _vrmLoader.VrmInstance;
                if (instance == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_VRM_MODEL_NOT_LOADED;
                    break;
                }
                var expression = instance.Runtime.Expression;
                if (expression == null)
                {
                    responseData.status = 500;
                    responseData.message = i18nMsg.ERROR_EXPRESSION_SYSTEM_NOT_AVAILABLE;
                    break;
                }

                bool seamlessParamMouth = GetQueryYesNo(query, "seamless", false);
                if (seamlessParamMouth)
                {
                    _animationHandler.StartCoroutine(SeamlessResetExpressionWeights(expression, 0.3f));
                    responseData.message = i18nMsg.RESPONSE_SEAMLESS_MOUTH_RESET_CMD;
                }
                else
                {
                    foreach (var exKey in expression.ExpressionKeys)
                    {
                        expression.SetWeight(exKey, 0.0f);
                    }
                    responseData.message = i18nMsg.RESPONSE_MOUTH_ALL_RESET_CMD;
                }
                responseData.status = 200;
                break;
            }
            #endregion

            #region 6) autoBlink（自動瞬き）
            case "autoBlink":
            {
                // 例: ?target=animation&cmd=autoBlink&enabled=true|false&freq=1000
                string enableParam = GetQueryParam(query, "enabled", null);
                bool hasEnabled = !string.IsNullOrEmpty(enableParam);
                bool wantEnable = _autoBlinkEnabled; // デフォは現在の状態を維持

                if (hasEnabled && bool.TryParse(enableParam, out bool tmpEnable))
                {
                    wantEnable = tmpEnable;
                }

                // freq があれば更新（GetQueryInt で取得、単位は ms）
                int freqVal = GetQueryInt(query, "freq", (int)_autoBlinkFreqMs);
                if (freqVal > 0)
                {
                    _autoBlinkFreqMs = freqVal;
                }

                // 状態変更があれば更新
                if (wantEnable != _autoBlinkEnabled)
                {
                    _autoBlinkEnabled = wantEnable;
                    if (_autoBlinkEnabled)
                    {
                        if (_autoBlinkRoutine != null)
                        {
                            _animationHandler.StopCoroutine(_autoBlinkRoutine);
                        }
                        _autoBlinkRoutine = _animationHandler.StartCoroutine(AutoBlinkRoutine());
                    }
                    else
                    {
                        if (_autoBlinkRoutine != null)
                        {
                            _animationHandler.StopCoroutine(_autoBlinkRoutine);
                            _autoBlinkRoutine = null;
                        }
                    }
                }

                responseData.status = 200;
                responseData.message = string.Format(i18nMsg.RESPONSE_AUTOBLINK_STATUS, _autoBlinkEnabled, _autoBlinkFreqMs);
                break;
            }
            #endregion

            #region 7) 存在しないコマンド（deprecated）
            default:
                Debug.LogWarning($"[DEPRECATED COMMAND] '{cmd}' was previously accepted but is now deprecated.");
                responseData.status = 410; // HTTP 410 Gone
                responseData.message = string.Format(i18nMsg.ERROR_DEPRECATED_COMMAND, cmd);
                break;
            #endregion
        }
    EndSwitch:
        SendResponse(context, responseData);
    }
    #endregion

    #region 表情補間＆コルーチン関連のユーティリティメソッド

    // 特定の ExpressionKey をシームレスに weight=targetWeight まで補間
    private IEnumerator InterpolateExpressionWeight(Vrm10RuntimeExpression expression, ExpressionKey key, float duration, float targetWeight = 1.0f)
    {
        float startWeight = expression.GetWeight(key);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float w = Mathf.Lerp(startWeight, targetWeight, elapsed / duration);
            expression.SetWeight(key, w);
            elapsed += Time.deltaTime;
            yield return null;
        }
        expression.SetWeight(key, targetWeight);
    }

    // 全 ExpressionKey をシームレスに 0 へ補間
    private IEnumerator SeamlessResetExpressionWeights(Vrm10RuntimeExpression expression, float duration)
    {
        var keys = expression.ExpressionKeys;
        Dictionary<ExpressionKey, float> startWeights = new Dictionary<ExpressionKey, float>();
        foreach (var k in keys)
        {
            startWeights[k] = expression.GetWeight(k);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            foreach (var k in keys)
            {
                float newWeight = Mathf.Lerp(startWeights[k], 0.0f, t);
                expression.SetWeight(k, newWeight);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        foreach (var k in keys)
        {
            expression.SetWeight(k, 0.0f);
        }
    }

    // まぶた左右をシームレスに指定値まで補間
    private IEnumerator InterpolateBlinkLR(Vrm10RuntimeExpression expression, float targetLeft, float targetRight, float duration)
    {
        float startLeft = expression.GetWeight(ExpressionKey.BlinkLeft);
        float startRight = expression.GetWeight(ExpressionKey.BlinkRight);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float newLeft = Mathf.Lerp(startLeft, targetLeft, t);
            float newRight = Mathf.Lerp(startRight, targetRight, t);
            expression.SetWeight(ExpressionKey.BlinkLeft, newLeft);
            expression.SetWeight(ExpressionKey.BlinkRight, newRight);
            elapsed += Time.deltaTime;
            yield return null;
        }
        expression.SetWeight(ExpressionKey.BlinkLeft, targetLeft);
        expression.SetWeight(ExpressionKey.BlinkRight, targetRight);
    }

    // まぶた左右をシームレスに 0 へ補間
    private IEnumerator SeamlessResetBlink(Vrm10RuntimeExpression expression, float duration)
    {
        float startLeft = expression.GetWeight(ExpressionKey.BlinkLeft);
        float startRight = expression.GetWeight(ExpressionKey.BlinkRight);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float newLeft = Mathf.Lerp(startLeft, 0f, t);
            float newRight = Mathf.Lerp(startRight, 0f, t);
            expression.SetWeight(ExpressionKey.BlinkLeft, newLeft);
            expression.SetWeight(ExpressionKey.BlinkRight, newRight);
            elapsed += Time.deltaTime;
            yield return null;
        }
        expression.SetWeight(ExpressionKey.BlinkLeft, 0.0f);
        expression.SetWeight(ExpressionKey.BlinkRight, 0.0f);
    }

    // クラス内フィールド：瞬き関連追加パラメータ
    private float extraDoubleBlinkProb = 0f; // 瞬きしなかった場合に蓄積する追加パラメータ
    private const float doubleBlinkThreshold = 0.2f; // この閾値以上ならダブルブリンクに切り替え
    private const float extraIncrement = 0.1f;         // 瞬きしなかった場合の増加量

    // 最後の瞬き時間と直近の瞬き回数の記録（必要なら）
    private float lastBlinkTime = 0f;
    private List<float> blinkTimestamps = new List<float>();

    private void RecordBlink()
    {
        lastBlinkTime = Time.time;
        blinkTimestamps.Add(lastBlinkTime);
        blinkTimestamps.RemoveAll(t => Time.time - t > 5f); // 過去5秒以内のみ保持
    }

    private int GetBlinkCount()
    {
        return blinkTimestamps.Count;
    }

    // AutoBlinkRoutine の修正版
    // ★UPDATE★: 高精度 & ノーアロケ版 AutoBlinkRoutine
    private IEnumerator AutoBlinkRoutine()
    {
        blinkSw.Restart();

        while (_autoBlinkEnabled)
        {
            while (blinkSw.ElapsedMilliseconds < _autoBlinkFreqMs)
                yield return null;
            blinkSw.Restart();

            if (UnityEngine.Random.value < 0.3333f)
            {
                if (extraDoubleBlinkProb >= doubleBlinkThreshold)
                    PerformDoubleBlink();   // ← ここで呼ぶ
                else
                    PerformSingleBlink();   // ← ここで呼ぶ

                extraDoubleBlinkProb = 0f;
                RecordBlink();
            }
            else
            {
                extraDoubleBlinkProb = Mathf.Clamp(extraDoubleBlinkProb + extraIncrement, 0f, 0.8f);
            }
        }
    }

    // ★UPDATE★ PerformSingle/DoubleBlink を _animationHandler.StartCoroutine で起動
    // ※ AnimationCommandHandler 自身は MonoBehaviour ではないため、
    //   StartCoroutine は必ず MonoBehaviour インスタンス経由で呼びます。

    private void PerformSingleBlink()  => _animationHandler.StartCoroutine(SingleBlink());
    private void PerformDoubleBlink()  => _animationHandler.StartCoroutine(DoubleBlink());



    /// <summary>
    /// 1回のまばたき (Single Blink)
    /// </summary>
    private IEnumerator SingleBlink()
    {
        var instance = _vrmLoader?.VrmInstance;
        if (instance == null) yield break;
        var expression = instance.Runtime.Expression;
        if (expression == null) yield break;

        // 目を閉じる (0.05秒)
        yield return InterpolateBlinkLR(expression, 1.0f, 1.0f, 0.05f);
        // 閉じた状態を維持 (0.03秒)
        yield return new WaitForSeconds(0.03f);
        // 目を開く (0.05秒)
        yield return InterpolateBlinkLR(expression, 0.0f, 0.0f, 0.05f);
    }

    /// <summary>
    /// 2回連続のまばたき (Double Blink)
    /// </summary>
    private IEnumerator DoubleBlink()
    {
        var instance = _vrmLoader?.VrmInstance;
        if (instance == null) yield break;
        var expression = instance.Runtime.Expression;
        if (expression == null) yield break;

        // --- 1回目のまばたき ---
        yield return InterpolateBlinkLR(expression, 1.0f, 1.0f, 0.06f);
        yield return new WaitForSeconds(0.04f);
        yield return InterpolateBlinkLR(expression, 0.0f, 0.0f, 0.06f);

        // 1回目と2回目の間隔 (極めて短い 0.015秒)
        yield return new WaitForSeconds(0.015f);

        // --- 2回目のまばたき (より速く) ---
        yield return InterpolateBlinkLR(expression, 1.0f, 1.0f, 0.04f);
        yield return new WaitForSeconds(0.03f);
        yield return InterpolateBlinkLR(expression, 0.0f, 0.0f, 0.04f);
    }
    #endregion
}

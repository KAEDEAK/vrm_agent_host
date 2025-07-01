using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using System.IO;

public class VrmCommandHandler : HttpCommandHandlerBase {
    private VRMLoader _vrmLoader;
    private AnimationHandler _animationHandler;

    // 許可するコマンド一覧を統一（必要に応じて拡張）
    private static readonly string[] AllowedCommands = {
        "load", "setLoc", "getLoc", "getRot", "setRot",
        "move", "rotate", "stop_move", "stop_rotate"
    };

    // 移動・回転用のコルーチンハンドル
    private Coroutine _moveCoroutine;
    private Coroutine _rotateCoroutine;

    public VrmCommandHandler(VRMLoader vrmLoader, AnimationHandler animationHandler) {
        _vrmLoader = vrmLoader;
        _animationHandler = animationHandler;
    }

    public override async void HandleCommand(HttpListenerContext context, NameValueCollection query) {
        var responseData = new ServerResponse();

        // コマンドを安全に取得
        string cmd = GetCommandName(query, AllowedCommands, out bool ok);
        if (!ok) {
            responseData.status = 400;
            responseData.message = string.Format(i18nMsg.ERROR_INVALID_VRM_CMD, cmd);
            SendResponse(context, responseData);
            return;
        }

        // "load" コマンドは特殊扱い
        if (cmd == "load") {
            // If AGIA animation is playing, reject with busy status
            if (_animationHandler != null && _animationHandler.IsAgiaPlaying) {
                responseData.status = 409;
                responseData.message = "busy";
                SendResponse(context, responseData);
                return;
            }

            string file = GetQueryParam(query, "file", null);
            if (!string.IsNullOrEmpty(file)) {
                string fullPath = UserPaths.GetVRMFilePath(file);
                if (!File.Exists(fullPath)) {
                    responseData.status = 500;
                    responseData.message = string.Format(i18nMsg.ERROR_VRM_FILE_NOT_FOUND, file);
                }
                else {
                    Debug.Log(string.Format(i18nMsg.LOG_VRM_LOAD_REQUEST, fullPath));
                    _animationHandler?.PrepareForVrmReload();
                    await _vrmLoader.ReloadVRMAsync(fullPath);
                    responseData.status = 200;
                    responseData.message = string.Format(i18nMsg.RESPONSE_VRM_LOADED, file);
                }
            }
            else {
                responseData.status = 400;
                responseData.message = i18nMsg.RESPONSE_FILE_PARAM_MISSING;
            }
            SendResponse(context, responseData);
            return;
        }

        // VRMインスタンスの取得
        var vrmInstanceObj = _vrmLoader?.VrmInstance?.gameObject;
        if (vrmInstanceObj == null) {
            responseData.status = 500;
            responseData.message = i18nMsg.ERROR_VRM_NOT_LOADED;
            SendResponse(context, responseData);
            return;
        }

        switch (cmd) {
            case "setLoc": {
                    string xyzParam = GetQueryParam(query, "xyz", null);
                    if (string.IsNullOrEmpty(xyzParam)) {
                        responseData.status = 400;
                        responseData.message = i18nMsg.ERROR_PARAM_XYZ_MISSING;
                    }
                    else {
                        try {
                            Vector3 pos = ParseVector3(xyzParam);
                            vrmInstanceObj.transform.position = pos;
                            responseData.status = 200;
                            responseData.message = string.Format(i18nMsg.RESPONSE_CURRENT_POSITION, pos);
                        }
                        catch (Exception ex) {
                            responseData.status = 400;
                            responseData.message = i18nMsg.ERROR_PARAM_XYZ_INVALID_FORMAT;
                        }
                    }
                    break;
                }
            case "getLoc": {
                    Vector3 currentPos = vrmInstanceObj.transform.position;
                    responseData.status = 200;
                    responseData.message = string.Format(i18nMsg.RESPONSE_CURRENT_POSITION, currentPos);
                    break;
                }
            case "getRot": {
                    Vector3 currentRot = vrmInstanceObj.transform.eulerAngles;
                    responseData.status = 200;
                    responseData.message = string.Format(i18nMsg.RESPONSE_CURRENT_ROTATION, currentRot);
                    break;
                }
            case "setRot": {
                    string xyzRotParam = GetQueryParam(query, "xyz", null);
                    if (!string.IsNullOrEmpty(xyzRotParam)) {
                        try {
                            Vector3 rot = ParseVector3(xyzRotParam);

                            // ▼▼▼ 性的表現が禁止されている場合、X/Z軸に±20°制限を適用 ▼▼▼
                            if (!VrmUsagePolicy.Instance.IsSexualExpressionAllowed) {
                                rot.x = ClampAngleWithinLimit(rot.x);
                                rot.z = ClampAngleWithinLimit(rot.z);
                                Debug.Log($"[SetRotLimitation] {i18nMsg.INFO_ROTATION_LIMITED_BY_LICENSE} to=({rot.x:F1}, {rot.y:F1}, {rot.z:F1})");
                            }

                            vrmInstanceObj.transform.rotation = Quaternion.Euler(rot);
                            responseData.status = 200;
                            responseData.message = string.Format(i18nMsg.RESPONSE_ROTATION_SET, rot);
                        }
                        catch (Exception ex) {
                            responseData.status = 400;
                            responseData.message = i18nMsg.ERROR_PARAM_XYZ_INVALID_FORMAT;
                        }
                    }
                    else {
                        responseData.status = 400;
                        responseData.message = i18nMsg.ERROR_PARAM_XYZ_MISSING;
                    }
                    break;
                }

            case "move": {
                    // 必須パラメータ: to, duration, delta。fromはオプション
                    string toParam = GetQueryParam(query, "to", null);
                    string durationParam = GetQueryParam(query, "duration", null);
                    string deltaParam = GetQueryParam(query, "delta", "{100}");
                    if (string.IsNullOrEmpty(toParam) ||
                        string.IsNullOrEmpty(durationParam) ||
                        string.IsNullOrEmpty(deltaParam)) {
                        responseData.status = 400;
                        responseData.message = "パラメータ 'to', 'duration', 'delta' は必須です。";
                        break;
                    }
                    Vector3 startPos;
                    bool hasFrom = false;
                    string fromParam = GetQueryParam(query, "from", null);
                    if (!string.IsNullOrEmpty(fromParam)) {
                        try {
                            startPos = ParseVector3(fromParam);
                            hasFrom = true;
                        }
                        catch (Exception ex) {
                            responseData.status = 400;
                            responseData.message = $"パラメータ 'from' の形式が不正です: {ex.Message}";
                            break;
                        }
                    }
                    else {
                        startPos = vrmInstanceObj.transform.position;
                    }
                    Vector3 toPos;
                    try {
                        toPos = ParseVector3(toParam);
                    }
                    catch (Exception ex) {
                        responseData.status = 400;
                        responseData.message = $"パラメータ 'to' の形式が不正です: {ex.Message}";
                        break;
                    }
                    float durationMs;
                    if (!float.TryParse(durationParam, out durationMs)) {
                        responseData.status = 400;
                        responseData.message = "パラメータ 'duration' の数値変換に失敗しました。";
                        break;
                    }
                    float totalDuration = durationMs / 1000f; // 秒へ変換
                    List<float> deltaList;
                    try {
                        deltaList = ParseDelta(deltaParam);
                    }
                    catch (Exception ex) {
                        responseData.status = 400;
                        responseData.message = $"パラメータ 'delta' の解析に失敗しました: {ex.Message}";
                        break;
                    }
                    // delta の合計が100にならなければ補正
                    float sumDelta = 0f;
                    foreach (float d in deltaList) {
                        sumDelta += d;
                    }
                    if (Mathf.Abs(sumDelta - 100f) > 0.01f) {
                        deltaList[deltaList.Count - 1] += (100f - sumDelta);
                    }
                    // 既存の移動処理があれば停止
                    if (_moveCoroutine != null) {
                        _vrmLoader.StopCoroutine(_moveCoroutine);
                        _moveCoroutine = null;
                    }
                    if (hasFrom) {
                        vrmInstanceObj.transform.position = startPos;
                    }
                    _moveCoroutine = _vrmLoader.StartCoroutine(MoveCoroutine(startPos, toPos, totalDuration, deltaList));
                    responseData.status = 200;
                    responseData.message = "移動処理を開始しました。";
                    break;
                }
            case "rotate": {
                    // 必須パラメータ: to, duration, delta。fromはオプション
                    string toParam = GetQueryParam(query, "to", null);
                    string durationParam = GetQueryParam(query, "duration", null);
                    string deltaParam = GetQueryParam(query, "delta", "{100}");
                    if (string.IsNullOrEmpty(toParam) ||
                        string.IsNullOrEmpty(durationParam) ||
                        string.IsNullOrEmpty(deltaParam)) {
                        responseData.status = 400;
                        responseData.message = "パラメータ 'to', 'duration', 'delta' は必須です。";
                        break;
                    }
                    Vector3 startRot;
                    bool hasFrom = false;
                    string fromParam = GetQueryParam(query, "from", null);
                    if (!string.IsNullOrEmpty(fromParam)) {
                        try {
                            startRot = ParseVector3(fromParam);
                            hasFrom = true;
                        }
                        catch (Exception ex) {
                            responseData.status = 400;
                            responseData.message = $"パラメータ 'from' の形式が不正です: {ex.Message}";
                            break;
                        }
                    }
                    else {
                        startRot = vrmInstanceObj.transform.eulerAngles;
                    }
                    Vector3 toRot;
                    /*
                    try {
                        toRot = ParseVector3(toParam);
                    }
                    */
                    try {
                        Debug.Log("TROT");
                        toRot = ParseVector3(toParam);

                        // ▼▼▼ 性的表現が禁止されている場合、X/Z軸に±20°制限を適用 ▼▼▼
                        if (!VrmUsagePolicy.Instance.IsSexualExpressionAllowed) {
                            toRot.x = ClampAngleWithinLimit(toRot.x);
                            toRot.z = ClampAngleWithinLimit(toRot.z);
                            Debug.Log($"[RotateLimitation] {i18nMsg.INFO_ROTATION_LIMITED_BY_LICENSE} to=({toRot.x:F1}, {toRot.y:F1}, {toRot.z:F1})");
                        }
                    }
                    catch (Exception ex) {
                        responseData.status = 400;
                        responseData.message = $"パラメータ 'to' の形式が不正です: {ex.Message}";
                        break;
                    }
                    float durationMs;
                    if (!float.TryParse(durationParam, out durationMs)) {
                        responseData.status = 400;
                        responseData.message = "パラメータ 'duration' の数値変換に失敗しました。";
                        break;
                    }
                    float totalDuration = durationMs / 1000f;
                    List<float> deltaList;
                    try {
                        deltaList = ParseDelta(deltaParam);
                    }
                    catch (Exception ex) {
                        responseData.status = 400;
                        responseData.message = $"パラメータ 'delta' の解析に失敗しました: {ex.Message}";
                        break;
                    }
                    float sumDelta = 0f;
                    foreach (float d in deltaList) {
                        sumDelta += d;
                    }
                    if (Mathf.Abs(sumDelta - 100f) > 0.01f) {
                        deltaList[deltaList.Count - 1] += (100f - sumDelta);
                    }
                    if (_rotateCoroutine != null) {
                        _vrmLoader.StopCoroutine(_rotateCoroutine);
                        _rotateCoroutine = null;
                    }
                    if (hasFrom) {
                        vrmInstanceObj.transform.eulerAngles = startRot;
                    }
                    _rotateCoroutine = _vrmLoader.StartCoroutine(RotateCoroutine(startRot, toRot, totalDuration, deltaList));
                    responseData.status = 200;
                    responseData.message = "回転処理を開始しました。";
                    break;
                }
            case "stop_move": {
                    if (_moveCoroutine != null) {
                        _vrmLoader.StopCoroutine(_moveCoroutine);
                        _moveCoroutine = null;
                        responseData.status = 200;
                        responseData.message = "移動処理を中断しました。";
                    }
                    else {
                        responseData.status = 200;
                        responseData.message = "実行中の移動処理はありません。";
                    }
                    break;
                }
            case "stop_rotate": {
                    if (_rotateCoroutine != null) {
                        _vrmLoader.StopCoroutine(_rotateCoroutine);
                        _rotateCoroutine = null;
                        responseData.status = 200;
                        responseData.message = "回転処理を中断しました。";
                    }
                    else {
                        responseData.status = 200;
                        responseData.message = "実行中の回転処理はありません。";
                    }
                    break;
                }
            default: {
                    responseData.status = 400;
                    responseData.message = string.Format(i18nMsg.ERROR_INVALID_VRM_CMD, cmd);
                    break;
                }
        }

        SendResponse(context, responseData);
    }

    // 文字列 "{x,y,z}" を Vector3 に変換するヘルパー
    private Vector3 ParseVector3(string input) {
        input = input.Trim();
        if (input.StartsWith("{") && input.EndsWith("}")) {
            input = input.Substring(1, input.Length - 2);
        }
        string[] parts = input.Split(',');
        if (parts.Length != 3) {
            throw new FormatException("Vector3は3要素必要です。");
        }
        float x = float.Parse(parts[0]);
        float y = float.Parse(parts[1]);
        float z = float.Parse(parts[2]);
        return new Vector3(x, y, z);
    }

    // 文字列 "{10,80,10}" 等を float リストに変換するヘルパー
    private List<float> ParseDelta(string deltaParam) {
        List<float> result = new List<float>();
        deltaParam = deltaParam.Trim();
        if (deltaParam.StartsWith("{") && deltaParam.EndsWith("}")) {
            deltaParam = deltaParam.Substring(1, deltaParam.Length - 2);
        }
        string[] parts = deltaParam.Split(',');
        foreach (string part in parts) {
            float val = float.Parse(part.Trim());
            result.Add(val);
        }
        return result;
    }

    // 移動用の補間コルーチン
    private IEnumerator MoveCoroutine(Vector3 start, Vector3 end, float totalDuration, List<float> deltaList) {
        int segments = deltaList.Count;

        // 区間ごとの累積進捗（割合）を等分割で計算（※ここは元ロジックのまま）
        List<float> cumFractions = new List<float>();
        float cumulative = 0f;
        for (int i = 0; i < segments; i++) {
            cumulative += 1f / segments;
            cumFractions.Add(cumulative);
        }

        Vector3 previousPos = start;
        for (int i = 0; i < segments; i++) {
            float segmentDuration = totalDuration * (deltaList[i] / 100f);
            float segElapsed = 0f;

            Vector3 segmentStart = previousPos;
            Vector3 segmentEnd = Vector3.Lerp(start, end, cumFractions[i]);

            while (segElapsed < segmentDuration) {
                segElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(segElapsed / segmentDuration);
                Vector3 newPos = Vector3.Lerp(segmentStart, segmentEnd, t);
                _vrmLoader.VrmInstance.gameObject.transform.position = newPos;
                yield return null;
            }

            previousPos = segmentEnd;
        }

        _vrmLoader.VrmInstance.gameObject.transform.position = end;
        _moveCoroutine = null;
    }

    // 回転用の補間コルーチン (Quaternion を使用)
    private IEnumerator RotateCoroutine(Vector3 startEuler, Vector3 endEuler, float totalDuration, List<float> deltaList) {
        int segments = deltaList.Count;
        Quaternion startRot = Quaternion.Euler(startEuler);
        Quaternion endRot = Quaternion.Euler(endEuler);

        List<float> cumFractions = new List<float>();
        float cumulative = 0f;
        for (int i = 0; i < segments; i++) {
            cumulative += 1f / segments;
            cumFractions.Add(cumulative);
        }

        Quaternion previousQuat = startRot;
        for (int i = 0; i < segments; i++) {
            float segmentDuration = totalDuration * (deltaList[i] / 100f);
            float segElapsed = 0f;

            Quaternion segmentStart = previousQuat;
            Quaternion segmentEnd = Quaternion.Slerp(startRot, endRot, cumFractions[i]);

            while (segElapsed < segmentDuration) {
                segElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(segElapsed / segmentDuration);
                Quaternion newRot = Quaternion.Slerp(segmentStart, segmentEnd, t);
                _vrmLoader.VrmInstance.gameObject.transform.rotation = newRot;
                yield return null;
            }

            previousQuat = segmentEnd;
        }

        _vrmLoader.VrmInstance.gameObject.transform.rotation = endRot;
        _rotateCoroutine = null;
    }
    /// <summary>
    /// 角度を 0〜360 に正規化
    /// </summary>
    private float NormalizeAngle(float angle) {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }

    /// <summary>
    /// 性的表現が不許可の場合のX/Z回転制限（±20° or 340°〜360°）
    /// </summary>
    private float ClampAngleWithinLimit(float angle) {
        angle = NormalizeAngle(angle);

        if (angle >= 0f && angle <= 20f) return angle;
        if (angle >= 340f && angle <= 360f) return angle;

        // 範囲外 → 最も近い制限値にクランプ
        float distToLower = Mathf.Abs(angle - 20f);
        float distToUpper = Mathf.Abs(angle - 340f);
        return (distToLower < distToUpper) ? 20f : 340f;
    }

}


// 回転用の補間コルーチン（Euler角の線形補間）
/*
private IEnumerator RotateCoroutine(Vector3 start, Vector3 end, float totalDuration, List<float> deltaList) {
    int segments = deltaList.Count;

    // 区間ごとの回転割合を計算
    List<float> cumFractions = new List<float>();
    float cumulative = 0f;
    for (int i = 0; i < segments; i++) {
        cumulative += 1f / segments; // 各区間の角度を等分割
        cumFractions.Add(cumulative);
    }

    Vector3 previousRot = start;

    for (int i = 0; i < segments; i++) {
        float segmentDuration = totalDuration * (deltaList[i] / 100f);
        float segElapsed = 0f;

        // 各区間の回転開始角度と終了角度を計算
        Vector3 segmentStart = previousRot;
        Vector3 segmentEnd = Vector3.Lerp(start, end, cumFractions[i]);

        while (segElapsed < segmentDuration) {
            segElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(segElapsed / segmentDuration);
            Vector3 newRot = Vector3.Lerp(segmentStart, segmentEnd, t);
            _vrmLoader.VrmInstance.gameObject.transform.eulerAngles = newRot;
            yield return null;
        }

        // ▼▼▼ 重要：ここで毎回ちゃんと更新 ▼▼▼
        previousRot = segmentEnd;
    }

    _vrmLoader.VrmInstance.gameObject.transform.eulerAngles = end;
    _rotateCoroutine = null;
}
*/
/*

継続した移動と継続した回転です。
?target=vrm&cmd=move&from={1,0,1}&to={2,1,1}&duration=1000&delta={10,30,60}
?target=vrm&cmd=rotate&from={0,45,0}&to={180,0,0}&duration=1000&delta={10,40,50}

fromが指定されている場合、まず、from の値に設定します。その上で to を目標に
durationの時間(msec)をかけて移動または回転します。
duration=1000は1秒です。
delta={10,80,10} は、加速度です。
この場合、from から to までの移動を3分割し、それぞれ1000msecの10%かけて移動,1000msecの80%かけて移動、1000msecの10%かけて移動します。
delta={10,80,0} のように 100% にならない場合は、最後の値に不足分を補い10%になります。
なお、delta={10,10,60,10,10} の場合は5分割になります。

?target=vrm&cmd=move&to={2,1,1}&duration=1000&delta={10,80,10}
?target=vrm&cmd=rotate&to={180,0,0}&duration=1000&delta={10,80,10}
このように、fromがない場合、現在の座標、または現在の角度から開始し to を目標に移動または回転します。

また途中で停止させたいときのために以下のコマンドも用意してください。
移動を中断します。
?target=vrm&cmd=stop_move
回転を中断します。
?target=vrm&cmd=stop_rotate

*/
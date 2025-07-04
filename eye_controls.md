# eye_controls.md

## [Implemented] 概要
本ドキュメントは、Unity 6 + UniVRM10 環境下での VRoid アバターの視線制御（Eye Controls）機能に関する要件定義および実装指南書です。
- HTTP コマンドサーバー拡張として実装
- 視線の直接指定および骨ターゲット追従をサポート
- ステータス管理を含む

## [Implemented] 要件定義
### 1. 視線制御モード
- `look`: 任意の方向（Yaw/Pitch）指定
- `lookAtBone`: 指定骨（例：LeftIndexDistal）への追従
- `lookAtCamera`: メインカメラ方向への追従

### 2. 単位・パラメータ仕様
- `mode`: `deg`／`rad`／`norm`
- `yaw`, `pitch`: 数値（deg: -180～180、rad: -π～π、norm: -1.0～1.0）
- `bone`: `HumanBodyBones` 列挙名
- `eye`: `both`／`left`／`right`

### 3. HTTP コマンド設計
- エンドポイント: `/?target=vrm&cmd=<command>&...`
- レスポンス形式: プレーンテキスト (`200 OK` / エラーメッセージ)

### 4. エラー & バリデーション
- 必須パラメータチェック
- 型変換失敗時の 400 応答
- ボーン未検出時、アニメーター未設定時の 500 応答

### [Hold] 5. 補間パラメータ検討
- `duration`: 視線移動に要する時間 (ms)
- `delta`: 加速度割合リスト

## [Implemented] HTTP コマンド設計
| コマンド       | パラメータ                         | 説明                 |
|---------------|------------------------------------|----------------------|
| `look`         | `mode`, `yaw`, `pitch`, `eye`     | 自由視線指定         |
| `lookAtBone`   | `bone`, `mode`, `eye`             | 骨ターゲット追従     |
| `lookAtCamera` | `mode`, `eye`                     | カメラ追従           |

#### [Implemented] コマンド例
```text
/?target=vrm&cmd=look&mode=deg&yaw=30&pitch=-15&eye=both
/?target=vrm&cmd=lookAtBone&bone=LeftIndexDistal&mode=norm&eye=left
/?target=vrm&cmd=lookAtCamera&mode=deg&eye=right
```

## [Implemented] 実装ガイド
1. `LookCommandHandler` クラス作成
2. `LookAtBoneCommandHandler` クラス作成
3. `HttpCommandHandlerBase` で共通ヘルパー利用
4. アバタールート取得 (`GameObject.FindWithTag("AvatarRoot")`)
5. `Animator.GetBoneTransform(...)` でボーン取得
6. `Quaternion.Euler(...)` で回転適用

### [Implemented] LookCommandHandler.cs
```csharp
using System;
using System.Net;
using UnityEngine;

public class LookCommandHandler : HttpCommandHandlerBase
{
    public override string CommandName => "look";

    public override void HandleCommand(HttpListenerContext context)
    {
        var mode = GetQueryParam(context, "mode") ?? "deg";
        var yawParam = GetQueryParam(context, "yaw") ?? "0";
        var pitchParam = GetQueryParam(context, "pitch") ?? "0";

        if (!float.TryParse(yawParam, out var rawYaw) ||
            !float.TryParse(pitchParam, out var rawPitch))
        {
            SendResponseWithContentType(context, 400, "text/plain", "invalid parameters");
            return;
        }

        float yawRad, pitchRad;
        switch (mode)
        {
            case "rad":
                yawRad = rawYaw;
                pitchRad = rawPitch;
                break;
            case "norm":
                const float maxDegNorm = 45f;
                yawRad = rawYaw * maxDegNorm * Mathf.Deg2Rad;
                pitchRad = rawPitch * maxDegNorm * Mathf.Deg2Rad;
                break;
            default:
                yawRad = rawYaw * Mathf.Deg2Rad;
                pitchRad = rawPitch * Mathf.Deg2Rad;
                break;
        }

        var root = GameObject.FindWithTag("AvatarRoot");
        if (root == null)
        {
            SendResponseWithContentType(context, 500, "text/plain", "avatar not found");
            return;
        }

        var animator = root.GetComponent<Animator>();
        if (animator == null)
        {
            SendResponseWithContentType(context, 500, "text/plain", "animator not found");
            return;
        }

        var leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
        var rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
        if (leftEye == null || rightEye == null)
        {
            SendResponseWithContentType(context, 500, "text/plain", "eye bones not found");
            return;
        }

        var eyeRotation = Quaternion.Euler(pitchRad * Mathf.Rad2Deg,
                                           yawRad * Mathf.Rad2Deg,
                                           0f);

        leftEye.localRotation = eyeRotation;
        rightEye.localRotation = eyeRotation;

        SendResponseWithContentType(context, 200, "text/plain", "ok");
    }
}
```

### [Implemented] LookAtBoneCommandHandler.cs
```csharp
using System;
using System.Net;
using UnityEngine;

public class LookAtBoneCommandHandler : HttpCommandHandlerBase
{
    public override string CommandName => "lookAtBone";

    public override void HandleCommand(HttpListenerContext context)
    {
        var boneName = GetQueryParam(context, "bone");
        if (string.IsNullOrEmpty(boneName))
        {
            SendResponseWithContentType(context, 400, "text/plain", "missing bone param");
            return;
        }

        var mode = GetQueryParam(context, "mode") ?? "deg";

        if (!Enum.TryParse<HumanBodyBones>(boneName, out var boneEnum))
        {
            SendResponseWithContentType(context, 400, "text/plain", "invalid bone name");
            return;
        }

        var root = GameObject.FindWithTag("AvatarRoot");
        if (root == null)
        {
            SendResponseWithContentType(context, 500, "text/plain", "avatar not found");
            return;
        }

        var animator = root.GetComponent<Animator>();
        if (animator == null)
        {
            SendResponseWithContentType(context, 500, "text/plain", "animator not found");
            return;
        }

        var boneTransform = animator.GetBoneTransform(boneEnum);
        if (boneTransform == null)
        {
            SendResponseWithContentType(context, 500, "text/plain", "bone transform not found");
            return;
        }

        var leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
        var rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
        if (leftEye == null || rightEye == null)
        {
            SendResponseWithContentType(context, 500, "text/plain", "eye bones not found");
            return;
        }

        Vector3 localDir = root.transform.InverseTransformPoint(boneTransform.position).normalized;
        float yawRad = Mathf.Atan2(localDir.x, localDir.z);
        float pitchRad = Mathf.Asin(localDir.y);

        switch (mode)
        {
            case "rad":
                break;
            case "norm":
                const float maxDegNormBone = 45f;
                yawRad = yawRad * maxDegNormBone * Mathf.Deg2Rad;
                pitchRad = pitchRad * maxDegNormBone * Mathf.Deg2Rad;
                break;
            default:
                yawRad = yawRad * Mathf.Rad2Deg * Mathf.Deg2Rad;
                pitchRad = pitchRad * Mathf.Rad2Deg * Mathf.Deg2Rad;
                break;
        }

        var eyeRotation = Quaternion.Euler(pitchRad * Mathf.Rad2Deg,
                                           yawRad * Mathf.Rad2Deg,
                                           0f);
        leftEye.localRotation = eyeRotation;
        rightEye.localRotation = eyeRotation;

        SendResponseWithContentType(context, 200, "text/plain", "ok");
    }
}
```

## [Implemented] VRMLookAtHead と Applyer の連携
VRMLookAtHead はターゲットのワールド座標を頭ボーンのローカル空間に変換し、そこから Yaw と Pitch を算出して `YawPitchChanged` イベントを発火します。

```csharp
public void LookWorldPosition()
{
    if (Target == null) return;
    float yaw;
    float pitch;
    LookWorldPosition(Target.position, out yaw, out pitch);
}

public void LookWorldPosition(Vector3 targetPosition, out float yaw, out float pitch)
{
    var localPos = Head.worldToLocalMatrix.MultiplyPoint(targetPosition);
    Matrix4x4.identity.CalcYawPitch(localPos, out yaw, out pitch);
    RaiseYawPitchChanged(yaw, pitch);
}
```

VRMLookAtBoneApplyer はこのイベントを購読し、左右の眼ボーンへ回転を適用します。

```csharp
void Start()
{
    m_head = GetComponentOrNull<VRMLookAtHead>();
    if (m_head == null)
    {
        enabled = false;
        Debug.LogError("[VRMLookAtBoneApplyer]VRMLookAtHead not found");
        return;
    }
    m_head.YawPitchChanged += ApplyRotations;
    LeftEye.Setup();
    RightEye.Setup();
}

void ApplyRotations(float yaw, float pitch)
{
    float leftYaw, rightYaw;
    if (yaw < 0)
    {
        leftYaw  = -HorizontalOuter.Map(-yaw);
        rightYaw = -HorizontalInner.Map(-yaw);
    }
    else
    {
        rightYaw = HorizontalOuter.Map(yaw);
        leftYaw  = HorizontalInner.Map(yaw);
    }

    if (pitch < 0)
        pitch = -VerticalDown.Map(-pitch);
    else
        pitch =  VerticalUp.Map(pitch);

    if (LeftEye.Transform != null && RightEye.Transform != null)
    {
        LeftEye.Transform.rotation  = LeftEye.InitialWorldMatrix.ExtractRotation()
                                * Matrix4x4.identity.YawPitchRotation(leftYaw, pitch);
        RightEye.Transform.rotation = RightEye.InitialWorldMatrix.ExtractRotation()
                                * Matrix4x4.identity.YawPitchRotation(rightYaw, pitch);
    }
}
```

同様に VRMLookAtBlendShapeApplyer では受け取った Yaw/Pitch をブレンドシェイプ値へ変換し、表情に反映します。
## [Implemented] テスト & 検証
- 単体テスト: パラメータパース、エラーハンドリング
- 結合テスト: HTTP リクエスト → アバター回転確認
- 手動検証: Unity Editor 上で HTTP クライアントツールを使用

## [Implemented] ステータス管理
| セクション               | ステータス     |
|--------------------------|----------------|
| 概要                     | Implemented    |
| 要件定義                 | Implemented    |
| HTTP コマンド設計        | Implemented    |
| 実装ガイド               | Implemented    |
| テスト & 検証            | Implemented    |

*ステータス:*
- `NotStarted`: 未着手
- `Review`: レビュー待ち
- `Completed`: 完了
- `Hold`: 保留

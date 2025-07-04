# eye_controls.md

## [Implemented] 概要
本ドキュメントは、Unity 6 + UniVRM10 環境下での VRoid アバターの視線制御（Eye Controls）機能に関する要件定義および実装指南書です。
- HTTP コマンドサーバー拡張として実装
- ステータス管理を含む

## [Implemented] 要件定義
### 1. 視線制御モード
- `lookAtHead`: VRMLookAtHead によるカメラ追従

### 2. 単位・パラメータ仕様
- `enable`: `true` / `false`

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
| コマンド       | パラメータ             | 説明                 |
|---------------|----------------------|----------------------|
| `lookAtHead`  | `enable`             | LookAtHead の有効/無効 |

#### [Implemented] コマンド例
```text
/?target=vrm&cmd=lookAtHead&enable=true
```

## [Implemented] 実装ガイド
`VRMLookAtHead` コンポーネントを利用し、カメラの `Transform` を `Target` に設定するだけで視線追従が機能します。

```csharp
var lookAtHead = loadedModel.GetComponent<VRM.VRMLookAtHead>();
if (lookAtHead != null && Camera.main != null)
{
    lookAtHead.Target = Camera.main.transform;
}
```

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

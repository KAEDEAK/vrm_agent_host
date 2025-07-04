# eye\_controls\_reference.md

## 1. UniVRM 標準機能の活用

- **VRMBlendShapeProxy の確認**
  - まばたきや表情ブレンドシェイプと干渉しないかチェック
  - `VRMBlendShapeProxy` の更新タイミング（LateUpdate vs Update）で視線制御が上書きされるケースあり
- **LookAtBoneSampler（サンプラー機能）**
  - UniVRM のサンプルに含まれる「LookAtBone」コンポーネントを参考に、骨への追従方法や制限角度の処理フローを確認

## 2. 座標系＆回転の注意点

- **ローカル vs ワールド座標**
  - `InverseTransformPoint` でローカル方向ベクトルを取得
  - `Quaternion.Euler` の軸順（X: ピッチ, Y: ヨー）に注意
- **ジンバルロック回避**
  - 回転を直接セットすると意図しない軸回転が起こる場合あり
  - `Quaternion.LookRotation(forward, up)` を併用して安定化可能

## 3. デバッグ／可視化テクニック

```csharp
void OnDrawGizmos() {
    Gizmos.color = Color.cyan;
    Gizmos.DrawLine(leftEye.position, leftEye.position + leftEye.forward * 0.5f);
    Gizmos.DrawLine(rightEye.position, rightEye.position + rightEye.forward * 0.5f);
}
```

- PlayMode テスト自動化: Unity Test Framework で HTTP コマンド投げ→ボーン回転値をアサーション

## 4. パフォーマンス & スレッド安全性

- **HTTP 処理と Unity メインスレッド**
  - Mono の `HttpListener` は別スレッド受信。Unity 呼び出しはメインスレッドでキューイング実行
  - パターン: `ConcurrentQueue<Action>` に登録→`Update()` 内で実行
- **更新頻度制限**
  - フレーム毎処理は重い。0.05～0.1 秒間隔でリミットをかけると安定性向上

## 5. ドキュメント & 仕様リファレンス

- **UniVRM GitHub リポジトリ**
  - [https://github.com/vrm-c/UniVRM](https://github.com/vrm-c/UniVRM) ← サンプルコードや制限仕様を参照
- **VRM1.0 仕様書（lookAt 拡張）**
  - 正規化パラメータ範囲や補間方法を確認して実装精度を担保


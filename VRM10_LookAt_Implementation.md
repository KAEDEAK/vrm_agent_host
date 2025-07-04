# VRM10 視線制御実装ガイド

## 概要

VRM10の標準的な視線制御システムを使用した実装に変更しました。以前のEyeRotationOverride.csによる独自実装から、VRM10の組み込み機能を活用する方法に移行しています。

## 主な変更点

### 1. 新しいコンポーネント: VRM10LookAtController

`Assets/Scripts/VRM10LookAtController.cs`

- VRM10の標準的な視線制御システム（Vrm10RuntimeLookAt）をラップ
- シンプルなAPIで視線制御を提供
- シングルトンパターンで実装され、グローバルアクセスが可能

### 2. VRMLoader.csの更新

- VRM10モデルロード時に自動的にVRM10LookAtControllerを追加
- 初期設定でメインカメラをターゲットに設定
- UpdateTypeをLateUpdateに設定して適切なタイミングで更新

### 3. VrmCommandHandlerの更新

- EyeRotationOverride.SetGlobalLookRotationの呼び出しを
- VRM10LookAtController.SetGlobalLookRotationに変更

## 使用方法

### 基本的な使い方

```csharp
// Yaw/Pitch角度を直接設定
VRM10LookAtController.SetGlobalLookRotation(yawDegrees, pitchDegrees);

// 特定のTransformを見るように設定
VRM10LookAtController.SetGlobalLookAtTarget(targetTransform);

// 視線をリセット（正面を向く）
VRM10LookAtController controller = GetComponent<VRM10LookAtController>();
controller.ResetLook();
```

### HTTPエンドポイント経由での制御

```
// 視線角度を設定
?target=vrm&cmd=look&yaw=15&pitch=-10

// カメラを見る
?target=vrm&cmd=lookAtCamera

// 特定のボーンを見る
?target=vrm&cmd=lookAtBone&bone=LeftHand
```

## テスト方法

### VRM10LookAtTestスクリプトの使用

1. シーンの任意のGameObjectに`VRM10LookAtTest`コンポーネントを追加
2. プレイモードで以下のキーを使用してテスト：
   - **矢印キー**: 手動で視線を制御
   - **A**: 自動テスト（視線が自動的に動く）
   - **Space**: 視線をリセット
   - **T**: ターゲットモード（赤い球を見る）
   - **M**: 手動モード
   - **Shift + マウス**: ターゲットの位置を移動

## 技術的な詳細

### VRM10の視線制御システム

1. **Vrm10Instance**: VRMモデルの最上位コンポーネント
2. **Vrm10Runtime**: 実行時の制御を管理
3. **Vrm10RuntimeLookAt**: 視線制御の実装
4. **LookAtEyeDirectionApplicableToBone**: ボーンベースの視線制御

### 座標系について

- **Yaw（ヨー）**: 水平方向の回転。正の値は右、負の値は左
- **Pitch（ピッチ）**: 垂直方向の回転。正の値は上、負の値は下

### 制御モード

1. **YawPitchValue**: 角度を直接指定
2. **SpecifiedTransform**: 特定のTransformを追跡

## トラブルシューティング

### 視線が動かない場合

1. VRM10モデルが正しくロードされているか確認
2. Vrm10Instanceコンポーネントが存在するか確認
3. UpdateTypeがLateUpdateに設定されているか確認
4. コンソールにエラーが出ていないか確認

### 視線の動きが不自然な場合

1. VRMモデルの視線設定（VRM10ObjectLookAt）を確認
2. 角度の制限値（RangeMapperCurve）を調整
3. ボーンの設定が正しいか確認

## 今後の拡張

- BlendShapeベースの視線制御のサポート
- 視線の滑らかな補間
- 複数ターゲットの優先度管理
- 視線制御のアニメーション統合

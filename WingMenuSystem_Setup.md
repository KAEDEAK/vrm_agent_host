# WingMenuSystem セットアップガイド

## 概要
WingMenuSystemは、天使の羽のような形状のメニューシステムです。
アバターの背後に8つの羽（左右4つずつ）を表示し、各羽をクリックすることで機能を実行できます。

## セットアップ手順

### 1. GameObjectの作成
1. Unityのヒエラルキーで右クリック
2. **Create Empty** を選択
3. 作成されたGameObjectの名前を「WingMenuSystem」に変更（任意）

### 2. スクリプトのアタッチ
1. 作成したGameObjectを選択
2. Inspectorで **Add Component** をクリック
3. **WingMenuSystem** を検索して選択
   - または、WingMenuSystem.csをドラッグ＆ドロップ

### 3. レイヤーの確認
- プロジェクトに「UI」レイヤーが存在することを確認
- 存在しない場合は、Edit > Project Settings > Tags and Layers で追加

## 使い方

### メニューの表示
- **初回起動時**：自動的に画面中央に表示
- **右クリック**：メニューを表示
- **アバタークリック**：メニューの表示/非表示を切り替え（アバターにColliderが必要）

### メニューの操作
- **羽をクリック**：対応する機能を実行
- **メニュー外をクリック**：メニューを閉じる
- **右下の赤い羽（EXIT）**：アプリケーションを終了

### 羽の配置
```
    左翼              右翼
    羽4              羽8
   羽3                羽7
  羽2                  羽6
 羽1                    羽5(EXIT)
```

## トラブルシューティング

### メニューが表示されない場合
1. **コンソールログを確認**
   - カメラが見つかっているか
   - レイヤーが正しく設定されているか
   - 羽が作成されているか

2. **カメラの設定を確認**
   - Main CameraのCulling MaskにUIレイヤーが含まれているか

3. **透過ウィンドウの場合**
   - 羽の色が背景と同じになっていないか
   - Z座標が適切か（現在は-2.0fに設定）

### クリックが反応しない場合
1. **レイヤーマスクを確認**
   - UIレイヤーが正しく設定されているか

2. **MovableWindowとの競合**
   - 右クリックでメニューを表示することで回避可能

## カスタマイズ

コード内の以下の値を変更することでカスタマイズ可能：

```csharp
private float wingScale = 0.5f;        // 羽のサイズ
private float menuRadius = 2.0f;       // メニューの半径
private float animationDuration = 0.3f; // アニメーション時間
private Color wingColor = new Color(0.3f, 0.7f, 1f, 1f);  // 羽の色
private Color hoverColor = new Color(1f, 1f, 0.3f, 1f);   // ホバー時の色
private Color exitColor = new Color(1f, 0.3f, 0.3f, 1f);  // EXIT羽の色
```

## 注意事項

- WingMenuSystemはCanvasではなく、通常のGameObjectにアタッチしてください
- 3Dメッシュとして羽を生成するため、Canvasは不要です
- TransparentWindowが有効な場合でも動作するよう設計されています

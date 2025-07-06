# WingMenuSystem HTTP Command Control - 要件定義書

## 進捗管理
- [Completed] Phase 1: WingMenuCommandHandler作成
- [Completed] Phase 2: WingMenuSystem拡張
- [Completed] Phase 3: AnimationServerへの統合
- [Ready] Phase 4: テスト・検証

## 概要
WingMenuSystemをHTTPコマンドで制御可能にする機能の実装。
AIチャットシステムなどからRESTful APIでメニューシステムを操作することを想定。

## 基本仕様

### 1. 基本制御コマンド [Pending]
```
target=wingsys&cmd=menus_show     # メニュー表示（両方）
target=wingsys&cmd=menus_hide     # メニュー非表示（両方）
target=wingsys&cmd=menus_show&side=left   # 左側のみ表示
target=wingsys&cmd=menus_show&side=right  # 右側のみ表示
target=wingsys&cmd=menus_hide&side=left   # 左側のみ非表示
target=wingsys&cmd=menus_hide&side=right  # 右側のみ非表示
target=wingsys&cmd=menus_status   # メニュー状態取得
```

### 2. メニュー定義コマンド [Pending]
```
# 全メニュー定義（8個、カンマ区切り、空の場合はプレースホルダー）
target=wingsys&cmd=menus_define&menus=reset_pose,reset_shape,,,exit,custom1,custom2,

# 右側のみ更新（4個）
target=wingsys&cmd=menus_define&menu_right=exit,reset_shape,reset_pose,custom1

# 左側のみ更新（4個）  
target=wingsys&cmd=menus_define&menu_left=reset_pose,reset_shape,placeholder,placeholder

# デフォルト状態にリセット（5番目にexitのみ）
target=wingsys&cmd=menus_clear
```

### 3. 設定コマンド [Pending]
```
# 羽の枚数設定
target=wingsys&cmd=config&left_length=8&right_length=8

# 角度設定
target=wingsys&cmd=config&angle_delta=20&angle_start=0

# 複合設定
target=wingsys&cmd=config&left_length=6&right_length=8&angle_delta=25&angle_start=10
```

### 4. 変形コマンド [Completed]
```
# 回転（度数法、XYZ順）
target=wingsys&cmd=rotate&xyz=0,45,0

# 位置（Unity座標系）
target=wingsys&cmd=position&xyz=0,1.5,0

# スケール（倍率）
target=wingsys&cmd=scale&xyz=1.2,1.2,1.2
```

### 5. 形状制御コマンド [Completed]
```
# 羽の形状設定（共通設定）
target=wingsys&cmd=shape&blade_length=1.0&blade_edge=0.5&blade_modifier=0.0

# 羽の形状設定（左右独立設定）
target=wingsys&cmd=shape&blade_left_length=1.2&blade_left_edge=0.4&blade_left_modifier=0.1
target=wingsys&cmd=shape&blade_right_length=1.0&blade_right_edge=0.6&blade_right_modifier=0.05

# パラメータ説明:
# blade_length: 羽の長さ（0.1〜3.0）- 共通設定
# blade_edge: 形状の減衰率（0.01〜1.0）- 共通設定
#   - 1.0 = 長方形
#   - 0.5 = 台形  
#   - 0.01 = 三角形に近い
# blade_modifier: 次の羽のサイズ減少率（0.0〜0.5）- 共通設定
#   - 0.0 = 全て同じサイズ
#   - 0.1 = 次の羽が10%小さくなる

# 左右独立パラメータ:
# blade_left_length, blade_left_edge, blade_left_modifier: 左側専用
# blade_right_length, blade_right_edge, blade_right_modifier: 右側専用
```

### 6. 色制御コマンド [Completed]
```
# 羽の色設定（デフォルト）
target=wingsys&cmd=color&values=white,gaming,blue,yellow

# ゲーミング効果の組み合わせ
target=wingsys&cmd=color&values=white,gaming,yellow,gaming

# 全てゲーミング効果
target=wingsys&cmd=color&values=gaming,gaming,gaming,gaming

# カスタム色の組み合わせ
target=wingsys&cmd=color&values=blue,red,yellow,green

# パラメータ説明:
# values: 4つの色指定をカンマ区切りで指定
#   1番目: 通常時の色
#   2番目: アニメーション時（開閉時）の色
#   3番目: ホバー時（コマンド無）の色
#   4番目: ホバー時（コマンド有）の色

# 利用可能な色:
# - white: 白色
# - gaming: ゲーミング効果（虹色の高速変化）
# - lightblue: 薄い青色
# - yellow: 黄色
# - red: 赤色
# - green: 緑色
# - blue: 青色
# - black: 黒色

# コマンドの有無判定:
# - Built-in Functions (reset_pose, reset_shape, exit): コマンド有り
# - placeholder: コマンド無し
# - カスタムメニュー: コマンド有り
```

## Built-in Functions [Pending]

### 1. reset_pose
- **機能**: ポーズリセット（AGIA待機アニメーション）
- **実装**: AnimationHandler.ResetAGIAAnimation() または Generic_01 (intValue=1) 再生
- **参照**: AnimationCommandHandler.cs "reset"コマンド

### 2. reset_shape  
- **機能**: 口の形状リセット（全表情ウェイトを0に）
- **実装**: VRM Expression System の全ExpressionKeyを0.0fに設定
- **参照**: AnimationCommandHandler.cs "reset_mouth"コマンド

### 3. exit
- **機能**: アプリケーション終了
- **実装**: 既存のWingMenuSystem.OnExitClick()と同じ処理
- **参照**: WingMenuSystem.cs OnExitClick()メソッド

## レスポンス仕様

### 成功レスポンス例
```json
{
  "status": 200,
  "succeeded": true,
  "message": "Menu shown (side: both)",
  "timestamp": "2025-01-06T21:54:38.123Z"
}
```

### ステータスレスポンス例
```json
{
  "status": 200,
  "succeeded": true,
  "message": {
    "visible": {"left": true, "right": true},
    "position": {"x": 0, "y": 0, "z": 0},
    "rotation": {"x": 0, "y": 45, "z": 0},
    "scale": {"x": 1, "y": 1, "z": 1},
    "config": {
      "left_length": 4,
      "right_length": 4,
      "angle_delta": 20,
      "angle_start": 0
    },
    "menus": {
      "left": [
        {"index": 0, "label": "reset_pose", "type": "builtin"},
        {"index": 1, "label": "reset_shape", "type": "builtin"},
        {"index": 2, "label": "placeholder", "type": "placeholder"},
        {"index": 3, "label": "placeholder", "type": "placeholder"}
      ],
      "right": [
        {"index": 4, "label": "exit", "type": "builtin"},
        {"index": 5, "label": "custom_action1", "type": "future_iot"},
        {"index": 6, "label": "placeholder", "type": "placeholder"},
        {"index": 7, "label": "placeholder", "type": "placeholder"}
      ]
    }
  },
  "timestamp": "2025-01-06T21:54:38.123Z"
}
```

### エラーレスポンス例
```json
{
  "status": 400,
  "succeeded": false,
  "message": "Invalid wingsys command: invalid_cmd",
  "timestamp": "2025-01-06T21:54:38.123Z"
}
```

## 技術仕様

### パラメータ形式
- **シンプル形式**: レガシーシステム・curl対応のためエスケープ不要
- **カンマ区切り**: `menus=action1,action2,action3,action4,exit,action6,action7,action8`
- **XYZ座標**: `xyz=0,45,0` (カンマ区切り、スペースなし)
- **数値**: `left_length=8` (整数・小数点対応)

### 拡張性考慮
- **IoT対応準備**: 将来的にRESTful APIコール機能を想定
- **ラベル管理**: 羽には表示せず、天使の輪システムでのホバー表示用
- **プレースホルダー**: 未定義メニューは"placeholder"として管理

## 実装計画

### Phase 1: WingMenuCommandHandler作成 [InProgress]
- [ ] HttpCommandHandlerBaseを継承したクラス作成
- [ ] 許可コマンドリストの定義
- [ ] 各コマンドの処理ロジック実装
- [ ] パラメータ解析・検証機能

### Phase 2: WingMenuSystem拡張 [Pending]
- [ ] HTTP制御用パブリックメソッド追加
- [ ] Built-in Functions実装
- [ ] 片側表示制御機能
- [ ] 設定可能プロパティ追加
- [ ] ステータス取得機能

### Phase 3: AnimationServerへの統合 [Pending]
- [ ] commandHandlers辞書への登録
- [ ] ルーティング確認
- [ ] エラーハンドリング統合

### Phase 4: テスト・検証 [Pending]
- [ ] 基本コマンドテスト
- [ ] Built-in Functions動作確認
- [ ] エラーケーステスト
- [ ] パフォーマンステスト

## 制約・注意事項

1. **スレッドセーフティ**: HTTPリクエストは別スレッドから来るため、MainThreadInvokerを使用
2. **後方互換性**: 既存のWingMenuSystem動作を変更しない
3. **パラメータ検証**: 数値範囲・文字列安全性のチェック必須
4. **エラーハンドリング**: 適切なHTTPステータスコードとメッセージ
5. **見た目重視**: ラベルは羽に表示せず、内部管理のみ

## 使用例とデフォルト設定

### デフォルト設定の再現
```bash
# 基本設定のデフォルト（左右4枚ずつ、角度20度間隔、0度開始）
target=wingsys&cmd=config&left_length=4&right_length=4&angle_delta=20&angle_start=0

# 元の羽配置の再現（-30度から90度の天使の羽配置）
target=wingsys&cmd=config&left_length=4&right_length=4&angle_start=-30&angle_delta=40

# 形状のデフォルト（台形状の羽、同じサイズ）
target=wingsys&cmd=shape&blade_length=1.0&blade_edge=0.5&blade_modifier=0.0
```

### 様々な羽スタイルの例

#### 鳥の羽スタイル
```bash
# 細長い三角形の羽、外側ほど小さく
target=wingsys&cmd=shape&blade_length=1.5&blade_edge=0.2&blade_modifier=0.15
target=wingsys&cmd=config&left_length=6&right_length=6&angle_delta=15&angle_start=-20
```

#### 蝶の羽スタイル
```bash
# 幅広の台形、サイズ均一
target=wingsys&cmd=shape&blade_length=0.8&blade_edge=0.8&blade_modifier=0.0
target=wingsys&cmd=config&left_length=3&right_length=3&angle_delta=30&angle_start=0
```

#### ドラゴンの羽スタイル
```bash
# 大きな三角形、外側ほど大幅に小さく
target=wingsys&cmd=shape&blade_length=2.0&blade_edge=0.1&blade_modifier=0.3
target=wingsys&cmd=config&left_length=5&right_length=5&angle_delta=25&angle_start=-15
```

#### 天使の輪風（多数の小さな羽）
```bash
# 小さな長方形、多数配置
target=wingsys&cmd=shape&blade_length=0.6&blade_edge=1.0&blade_modifier=0.05
target=wingsys&cmd=config&left_length=8&right_length=8&angle_delta=12&angle_start=-45
```

### 形状パラメータの詳細説明

#### blade_length（羽の長さ）
- **範囲**: 0.1〜3.0
- **効果**: 羽全体の縦方向のサイズ
- **例**: 
  - 0.5 = 短い羽
  - 1.0 = 標準的な羽
  - 2.0 = 長い羽

#### blade_edge（形状の減衰率）
- **範囲**: 0.01〜1.0
- **効果**: 根元から先端への幅の変化
- **例**:
  - 1.0 = 長方形（先端も根元と同じ幅）
  - 0.5 = 台形（先端が根元の半分の幅）
  - 0.1 = 鋭い三角形（先端がとても細い）

#### blade_modifier（サイズ減少率）
- **範囲**: 0.0〜0.5
- **効果**: 羽のインデックスが増えるごとのサイズ減少
- **例**:
  - 0.0 = 全ての羽が同じサイズ
  - 0.1 = 次の羽が10%小さくなる
  - 0.2 = 次の羽が20%小さくなる

### 実用的な組み合わせ例

#### コンパクトメニュー
```bash
target=wingsys&cmd=config&left_length=3&right_length=3
target=wingsys&cmd=shape&blade_length=0.8&blade_edge=0.6&blade_modifier=0.0
```

#### 華やかな表示
```bash
target=wingsys&cmd=config&left_length=6&right_length=6&angle_delta=18
target=wingsys&cmd=shape&blade_length=1.2&blade_edge=0.4&blade_modifier=0.1
```

#### ミニマルデザイン
```bash
target=wingsys&cmd=config&left_length=2&right_length=2&angle_delta=45
target=wingsys&cmd=shape&blade_length=1.0&blade_edge=0.3&blade_modifier=0.0
```

## 更新履歴
- 2025-01-06: 初版作成、要件定義完了
- 2025-01-06: 羽の形状制御機能追加、使用例とデフォルト設定説明追加

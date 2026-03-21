# VRM Agent Host 尽くせ理 - ユーザーガイド

これは、Unity 6 ベースのアバター表示アプリです。
HTTP経由で制御するためのサーバ機能を提供します。 
このドキュメントは、主に開発者向けに、コマンド操作の基本と、利用可能なターゲット別コマンドの一覧をまとめています。
起動から実行までの最短手順は、パッケージに含まれる付属のドキュメントを参照してください。

なおドキュメントは、日本語が原本になっています。プロジェクトも主に日本語向けになっています。
（疑似リップシンクも基本的には日本語の発音に最適化されていいます）

注意
- コマンドは、エンコードを極力必要としない構文を採用しています。
  主要なコマンドは、エンコードしなくても通ります。色指定などは # 等は不要です。#RRGGBB ではなく RRGGBB としてください。

概要
- 待受ポート: `http://localhost:34560/`
- 制御方法: クエリで `target` と `cmd` を指定してGETアクセスします。
  例: `http://localhost:34560/?target=vrm&cmd=load&file=model.vrm`
- 音声再生: `POST /waveplay/`（RIFF/WAVE mono 16bit 48kHz）にバイナリを送信すると即時再生します。

設定の読み込み順（起動時）
- ベース: `Assets/Resources/default_config.json`
- マージ順: `config.json` → `lastWindowBounds.json` → `override_*.json`（昇順）→ `override.json`
- マージ規則: 連想配列は深い上書き、配列は置換、false/0/空も上書き対象。無効JSONはスキップして警告。
- 保存方針: ランタイムから `config.json` は書き換えません。`saveWindowBounds=true` の場合でも `lastWindowBounds.json` に `window.position` のみ保存します。
  - 保存先: プロジェクトルート直下（`config_dump.json` と同じ場所）。

クイックスタート
- 動作確認（サーバ状態）: `GET /?target=server&cmd=getstatus`
- VRMを読み込む: `GET /?target=vrm&cmd=load&file=model.vrm`
- 背景を単色にする: `GET /?target=background&cmd=fill&color=FF0000`
- アニメーション再生: `GET /?target=animation&cmd=play&id=Idle_generic&seamless=y`

setup_example 実行後の確認例
- `GET /?target=server&cmd=getstatus`
- `GET /?target=animation&cmd=play&id=Idle_cute&seamless=y`
- `GET /?target=animation&cmd=play&id=Idle_energetic&seamless=y`
- `GET /?target=animation&cmd=play&id=Idle_think&seamless=y`
- `GET /?target=animation&cmd=play&id=Layer_nod_once`

リクエストの基本形
- 形式: `GET /?target=<対象>&cmd=<コマンド>&<パラメータ…>`
- 例: `/?target=vrm&cmd=move&from=0,0,0&to=1,2,1&duration=1000&delta=10,80,10`
- 色指定に # や %23 は不要 `#RRGGBB` ではなく `RRGGBB`で指定できます。

ターゲット別コマンド一覧

vrm
VRM/VRM10 の読み込み、位置・回転、視線制御など。

| cmd | 概要 | 例 |
| --- | --- | --- |
| load | VRMファイルを読み込む | `?target=vrm&cmd=load&file=model.vrm[&keep_posture=true]` |
| setLoc | 位置を設定 | `?target=vrm&cmd=setLoc&xyz=0,1,0` |
| getLoc | 現在の位置を取得（VRM本体、neck、spine座標含む） | `?target=vrm&cmd=getLoc` |
| getRot | 現在の回転を取得（VRM本体、neck、spine角度含む） | `?target=vrm&cmd=getRot` |
| setRot | 回転を設定 | `?target=vrm&cmd=setRot&xyz=0,90,0` |
| move | 位置を補間移動 | `?target=vrm&cmd=move&from=0,0,0&to=1,2,1&duration=1000&delta=10,80,10` |
| rotate | 回転を補間 | `?target=vrm&cmd=rotate&from=0,0,0&to=0,180,0&duration=1000&delta=10,80,10` |
| stop_move | 移動処理を停止 | `?target=vrm&cmd=stop_move` |
| stop_rotate | 回転処理を停止 | `?target=vrm&cmd=stop_rotate` |
| look | 視線角度を直接指定 | `?target=vrm&cmd=look&mode=deg&yaw=30&pitch=-15` |
| look_at_bone | 指定ボーンへ視線を向ける | `?target=vrm&cmd=look_at_bone&bone=LeftHand` |
| look_at_camera | カメラ方向へ視線を向ける | `?target=vrm&cmd=look_at_camera` |
| gaze_control | 視線制御の有効/無効 | `?target=vrm&cmd=gaze_control&enable=true` |
| micro_gaze_adjustment_simulation | 自動瞬き前の視線微動ON/OFF | `?target=vrm&cmd=micro_gaze_adjustment_simulation&enable=true` |
| look_at_head | (旧) 視線制御の有効/無効 | `?target=vrm&cmd=look_at_head&enable=false` |
| debug_lock_status | AGIAロック状態確認 | `?target=vrm&cmd=debug_lock_status` |
| debug_force_unlock | AGIAロック強制解除 | `?target=vrm&cmd=debug_force_unlock` |
| body_interaction | ボディのインタラクション（クリック/ドラッグ）有効化 | `?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true\|false&useSpringBone=true\|false[&useFacePokeShader=true\|false][&channel=all\|head\|chest\|arms\|lower]` |
| body_interaction_status | ボディのインタラクション状態取得/編集 | `?target=vrm&cmd=body_interaction_status[&detailed=true|false][&refresh=true|false][&value=true|false][&equal=N][&greater=N][&less=N]` または `&op=edit&(targetModelName=NAME|targetStandardName=NAME)&[clickCount=N][&dragCount=N][&feel=TEXT]` |
| body_partitioning | パーティション定義/記録/保存 | `?target=vrm&cmd=body_partitioning&op=set|rec_start|rec_end|reset|delete|save|load[...]` |
| body_partitioning_status | パーティション状態取得（構造は body_interaction_status と大枠一致） | `?target=vrm&cmd=body_partitioning_status[&detailed=true|false][&refresh=true|false][&value=true|false][&equal=N][&greater=N][&less=N]` |
| lock_pos | 位置ロック（重力ON/OFF） | `?target=vrm&cmd=lock_pos&enable=true[&ground_offset=0.5][&springbone_gravity=true][&springbone_power=1.0]` |
| springbone_gravity | SpringBone物理制御（髪・衣装） | `?target=vrm&cmd=springbone_gravity&power=1.0[&stiffness=1.0][&drag=0.4][&radius=0.05][&category=hair][&part=Transform名]` |
| springbone_gravity_get | SpringBone物理設定取得 | `?target=vrm&cmd=springbone_gravity_get` |

getLoc / getRot のレスポンス例（抜粋）
```json
{
  "status": 200,
  "data": {
    "pos_xyz": {"x": 0.0, "y": 0.0, "z": 0.0},
    "neck_xyz": {"x": 0.0, "y": 1.5, "z": 0.0},
    "spine_xyz": {"x": 0.0, "y": 1.2, "z": 0.0}
  }
}
```
備考
- 視線制御は VRM10 の標準 LookAt（Vrm10RuntimeLookAt）ベースです。
- `micro_gaze_adjustment_simulation` で自動瞬き前の視線微動を有効化すると、最新の視線コマンドを基準に左右中心の揺らぎを挟んだ後に瞬きを行います。
- VRM10では左右の目の個別制御（`eye` パラメータ）は未サポートです。
- AGIA アニメーション再生中に `load` が来た場合は `409 busy` を返します。
- `keep_posture=true` を指定すると、ロード前の位置・回転を保存し、ロード後に自動復元します（クライアント側処理）。

ボディインタラクション（クリック/ドラッグ）
- 有効化: `?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true|false&useSpringBone=true|false[&useFacePokeShader=true|false][&visibleCollider=true|false][&visibleFacePoke=true|false]`
- **個別制御**: `useFacePoke`（CPU版FacePoke）と `useSpringBone`（SpringBoneコライダー）を個別に制御可能
- **実行条件**: 各機能は `enable=true` **かつ** 各フラグ `=true` で動作
- 状態取得: `?target=vrm&cmd=body_interaction_status`（`&detailed=true` で履歴や dragStart/dragCurrent/lastDrag を返す）
- 注意: `body_interaction_status` に `enable` を付けるのは不正。400 + `invalid parameter` を返す。
- 併存ポリシー: 羽メニューが開いていても body interaction は有効です。ただし RESIZE_MOVE の UI が有効な間は、ウィンドウ操作を優先するため body interaction は開始/継続しません。

**可視化機能**（デバッグ・調整用）:
- SpringBoneコライダー可視化: `&visibleCollider=true` - 赤い球体で反応範囲を表示
- FacePokeメッシュ干渉可視化: `&visibleFacePoke=true` - 緑の球体で変形位置・強度を表示
- 両方を表示: `&visible=true`（レガシー互換）または `&visibleCollider=true&visibleFacePoke=true`
- 仕様（簡易）:
  - Top-level: `clicked`, `dragged`, `dragging`
  - parts: `standardName`, `modelName`, `clicked/clickCount/lastClickedAt/lastClickedAgoMs`, `dragged/dragCount/lastDraggedAt/lastDraggedAgoMs`, `feel`（空でない場合のみ）
- detailed=true 追加情報:
  - 履歴 `history`（クリック/ドラッグ混在, 最大N件）, `historySize`, `expirationMs`
  - `dragging=true` 時: `dragPart`, `dragStart`, `dragCurrent`
  - `dragging=false` 時: `lastDrag`（`{ timestamp, part, modelName, action:"drag", start:{}, end:{} }`）

フィルタ機能:
- `value=true|false`: クリック済み/未クリック部分のフィルタ
- `equal=N`: クリック回数がN回の部分のみ
- `greater=N` (または `grater=N`): N回超過の部分のみ
- `less=N`: N回未満の部分のみ
- 複数条件はAND結合: `&value=true&greater=2` (クリック済みで3回以上)
- フィルタ後、全体の `clicked`/`dragged` も再計算される

編集機能 (op=edit):
- `op=edit`: 編集モードを有効化
- `targetModelName=NAME` または `targetStandardName=NAME`: 編集対象部位を指定（いずれか1つ必須）
- `clickCount=N`: クリック回数を設定（0以上の整数）
- `dragCount=N`: ドラッグ回数を設定（0以上の整数）
- `feel=TEXT`: feelテキストを設定（最大255文字、`&feel=` のように空文字列を指定するとフィールドを削除）
- カウンタ0設定時: フラグとタイムスタンプをリセット
- カウンタ1以上設定時: フラグをtrueにして現在時刻を設定
- 例: `?target=vrm&cmd=body_interaction_status&op=edit&targetStandardName=UpperChest&clickCount=0`
- 例: `?target=vrm&cmd=body_interaction_status&op=edit&targetModelName=J_Bip_C_Head&dragCount=5&feel=touched`
- 例: `?target=vrm&cmd=body_interaction_status&op=edit&targetModelName=J_Bip_C_Head&feel=` （feelフィールドを削除）
- 実装メモ:
  - VRM0.x: ドラッグ中のみ小さな球コライダーを対象スプリングボーンに動的追加（髪など）。半径は `pointerRadius`、深さは `pointerDepth` で調整。
  - VRM1.0(FastSpringBone): ポインタ近傍ジョイントだけに局所的な力を適用（`gravityPower/Dir`）+ VRM10SpringBoneCollider を動的生成・追加。
  - Face Poke: 顔メッシュのCPU変形またはShader変形（CPU推奨）。変形半径・強度は `facePokeRadius`, `facePokeForceGain` で調整。
  - 可視化: `visibleCollider` で赤球（SpringBone範囲）、`visibleFacePoke` で緑球（メッシュ変形）を表示。

主要パラメータ（任意）
- **可視化**: `visibleCollider`, `visibleFacePoke`, `visible`（デバッグ・調整用）
- **VRM1.0制御**: `useVrm10PointerCollider`（FastSpringBone用コライダー）
- **Face Poke**: `facePokeImplementation=cpu|shader`, `facePokeRadius`, `facePokeForceGain`
- **FacePoke 部位指定**: `channel=all|head|chest|arms|lower`（デフォルト: all）
  - `all`: 全ての部位に適用（デフォルト）
  - `head`: head 部位のみ（face, head, hair を含むメッシュ）
  - `chest`: chest 部位のみ（chest, breast, body, torso を含むメッシュ）
  - `arms`: arms 部位のみ（arm, hand, glove を含むメッシュ）
  - `lower`: lower 部位のみ（leg, foot, skirt, hip を含むメッシュ）
- **ローカライズ**: `localRadius`, `localForce`, `falloff=gaussian|linear|none`
- **SpringBone**: `maxJoints`, `patterns`（名前パターン、`,`区切り）
- **距離制御**:
  - `springBonePointerDepth`: SpringBoneコライダーの接触点からの距離（-0.1 ～ +0.1m、デフォルト: 0.02m）
  - `facePokeDepth`: FacePokeの接触点からの距離（-0.05 ～ +0.05m、デフォルト: 0.01m）
  - マイナス値でモデル内部方向に配置可能
- **SpringBoneコライダー制御**:
  - `springBonePointerRadius`: SpringBoneポインタコライダー半径（0.01-0.5m、デフォルト: 0.03m）
  - `springBonePointerDepth`: SpringBoneポインタコライダー深度（0.0-0.1m、デフォルト: 0.02m）
  - レガシー: `radius`/`depth` も利用可能（後方互換性）

例（髪だけ、弱め・局所的）
```
# FacePokeのみ有効化（頬つん）
?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&useSpringBone=false&facePokeRadius=0.03&facePokeForceGain=0.3

# SpringBoneのみ有効化（髪の揺れ）
?target=vrm&cmd=body_interaction&enable=true&useFacePoke=false&useSpringBone=true&localRadius=0.06&localForce=1.5

# 両方有効化
?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&useSpringBone=true&facePokeRadius=0.03&localRadius=0.06

# FacePoke 部位指定
# 全部位（デフォルト）
?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=all

# head のみ
?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=head

# chest のみ
?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=chest

# arms のみ
?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=arms

# lower のみ
?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&channel=lower
```

可視化の使用例:
```
# SpringBoneコライダーのみ表示（髪の反応範囲確認）
?target=vrm&cmd=body_interaction&enable=true&useSpringBone=true&visibleCollider=true&visibleFacePoke=false

# 顔のメッシュ変形のみ表示（頬つんの位置・強度確認）
?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&visibleCollider=false&visibleFacePoke=true

# 両方表示（パラメータ調整用）
?target=vrm&cmd=body_interaction&enable=true&useFacePoke=true&useSpringBone=true&visibleCollider=true&visibleFacePoke=true

# 距離制御の使用例
?target=vrm&cmd=body_interaction&enable=true&springBonePointerDepth=0.03&facePokeDepth=-0.005

# SpringBoneコライダー制御の使用例
?target=vrm&cmd=body_interaction&enable=true&springBonePointerRadius=0.1&springBonePointerDepth=0.05
```

運用ヒント（UIとの共存）
- RESIZE_MOVE を有効化/無効化: `?target=server&cmd=resize_move_ui&enable=true|false&auto_hide=true&time=30000`
- 現在のメニュー状態: `?target=wingsys&cmd=menus_status`（`isMenuOpen`, `resizeMoveActive`）

status拡張
- `springBone`: `modelType`, `method`, `engine`, `externalForceMag` など
- `interaction`: `channel`, `localRadius`, `localForce`, `falloff`, `maxJoints`, `patterns`, `selectedJointCount`

Body Partitioning（概要）
- 目的: head/chest/arms/lower 内の細分領域（耳・目など）をユーザー定義し、その領域単位のクリック/ドラッグ集計を行う。
- 定義: `?target=vrm&cmd=body_partitioning&op=set&tune_target=<standardName|modelName>&partition_id=1&partition_name=left_ear`
- 記録/終了: `op=rec_start&partition_id=1` → ドラッグでサンプル蓄積 → `op=rec_end[&dilate=0.005]`
- 状態: `body_partitioning_status` は `enabled/parts/clicked/dragged/dragging` を含む（クライアントは body_interaction_status と同様に扱える）。
- フィルタ: `value/equal/greater/less` は `clickCount` を対象に適用（AND）。

background
背景画像の設定、単色塗りつぶし。

| cmd | 概要 | 例 |
| --- | --- | --- |
| load | 画像を背景に設定 | `?target=background&cmd=load&file=bg.png` |
| fill | 単色で背景を塗りつぶす | `?target=background&cmd=fill&color=FF0000` |
| transparent | 透過ウィンドウをアルファブレンドで有効化 | `?target=background&cmd=transparent&enable=true` |

Windows の `cmd.exe` では `%` が環境変数展開に使われるため `color=%23FF0000` のように書く場合は `%%23...` としてください。色指定は `RRGGBB` / `#RRGGBB` どちらも可です。

image
画像オーバーレイと反映先の制御。

| cmd | 概要 | 例 |
| --- | --- | --- |
| enable | 画像オーバーレイを有効化し既定の自動非表示時間を更新 | `?target=image&cmd=enable&enable=true&autohide=15000` |
| clear | 現在の表示をクリア | `?target=image&cmd=clear` |
| status | 表示状態を取得 | `?target=image&cmd=status` |
| size | サイズと単位を設定/取得 | `?target=image&cmd=size&w=80&h=60&unit=percent` |
| show | 直近受信した画像を再表示 | `?target=image&cmd=show` |
| set_destination | 画像の反映先を切り替え | `?target=image&cmd=set_destination&where=image_display` |
| reset_eyes | 目に適用したテクスチャを元に戻す | `?target=image&cmd=reset_eyes` |
| eyes_debug | 目テクスチャの診断情報を取得 | `?target=image&cmd=eyes_debug` |

world
ワールド重力の設定と取得。

| cmd | 概要 | 例 |
| --- | --- | --- |
| gravity | 重力ベクトルと有効化を設定 | `?target=world&cmd=gravity&part=world&xyz=0,-9.81,0&enable=true` |
| gravity_get | 現在の重力設定を取得 | `?target=world&cmd=gravity_get` |

重力・SpringBone制御
- **重力システム**: ワールド重力（デフォルト: 無効）、アバター位置ロック、SpringBone物理制御
- **`lock_pos`**: `enable=true` で空中固定、`false` で重力有効。`springbone_gravity=true` で位置ロック中も髪・衣装に重力適用
- **`springbone_gravity`**: 髪・衣装の物理パラメータを制御（`power`, `stiffness`, `drag`, `radius`）
  - グローバル: 全SpringBoneに適用
  - カテゴリ: `category=hair|chest|arms|lower|body` で部位別に制御
- **`springbone_gravity_get`**: 現在のSpringBone物理設定を取得（グローバル/カテゴリ/パート設定）
  - パート: `part=Transform名` で個別制御
  - 優先順位: パート > カテゴリ > グローバル
- **用途例**: 逆さアバター（髪が下に流れる）、ゼロG空間、髪と胸の独立制御

animation
AGIA 再生やブレンドシェイプ、瞬き・口形状など。

| cmd | 概要 | 例 |
| --- | --- | --- |
| reset | アニメーション初期化（ロックもリセット） | `?target=animation&cmd=reset` |
| play | ID または vrma を再生 | `?target=animation&cmd=play&id=Idle_generic&seamless=y`（ファイル時 `&file=anim.vrma&continue=y`） |
| stop | 停止 | `?target=animation&cmd=stop` |
| resume | 再開 | `?target=animation&cmd=resume` |
| getstatus | 状態取得（neck, spine座標含む） | `?target=animation&cmd=getstatus&opt=alias` |
| shape | ブレンドシェイプ操作 (Relaxed/Angry/Aa/Ih/Ou/Ee/Oh) | `?target=animation&cmd=shape&word=Relaxed&seamless=y&refresh=y` |
| mouth | 口形状操作 (A/I/U/E/O) | `?target=animation&cmd=mouth&word=A&seamless=y&refresh=y` |
| auto_prepare_seamless | シームレス準備設定 | `?target=animation&cmd=auto_prepare_seamless&enable=true` |
| get_auto_prepare_seamless | シームレス準備状態取得 | `?target=animation&cmd=get_auto_prepare_seamless` |
| reset_blink | 瞬きをリセット | `?target=animation&cmd=reset_blink&seamless=y` |
| reset_mouth | 口形状をリセット | `?target=animation&cmd=reset_mouth&seamless=y` |
| auto_blink | 自動瞬き設定 | `?target=animation&cmd=auto_blink&enable=true&freq=2000` |
| debug_lock_status | AGIAロック状態確認 | `?target=animation&cmd=debug_lock_status` |
| debug_force_unlock | AGIAロック強制解除 | `?target=animation&cmd=debug_force_unlock` |

主なパラメータ
- seamless: シームレス切替（`y`/`n`, デフォルト: `n`）
- refresh: 適用前に全表情リセット（`y`/`n`, デフォルト: `y`） ※shape/mouth/blinkコマンドで利用可能
  - 前回の表情状態を引き継がず、表情や口の形の破綻を防止します
  - 意図的に表情を合成したい場合は `refresh=n` を明示的に指定してください
- continue: vrma ファイルのループ再生（`y`/`n`, デフォルト: `n`）

lipSync
音声入力に同期した口形状制御。

| cmd | 概要 | 例 |
| --- | --- | --- |
| getstatus | 現在の状態を取得 | `?target=lipSync&cmd=getstatus` |
| audiosync | リップシンク開始（channel=1 で出力音声） | `?target=lipSync&cmd=audiosync&channel=1&scale=3` |
| audiosync_on | 上記と同じ（開始） | `?target=lipSync&cmd=audiosync_on&channel=1&scale=3` |
| audiosync_off | リップシンク停止 | `?target=lipSync&cmd=audiosync_off` |

Channel IDs
- 0: WavePlayback
- 1: ExternalAudio（WASAPI ループバック/Windows）
- 2: Microphone（既定）

備考（デフォルトについて）
- `audiosync` / `audiosync_on` で `channel` を省略した場合の既定は `2`（Microphone）です（後方互換のため）。

server
ウィンドウ、透過、WAVE再生統合、設定再読込など。

| cmd | 概要 | 例 |
| --- | --- | --- |
| transparent | クロマキー透過ウィンドウの有効/無効 | `?target=server&cmd=transparent&enable=true` |
| allow_drag_objects | ドラッグ操作の有効/無効 | `?target=server&cmd=allow_drag_objects&enable=true` |
| stay_on_top | 最前面化 | `?target=server&cmd=stay_on_top&enable=true` |
| pointer_events_none | マウスイベント透過 | `?target=server&cmd=pointer_events_none&enable=true` |
| getstatus | サーバ状態取得 | `?target=server&cmd=getstatus&flt=web&limit=10` |
| terminate | アプリ終了 | `?target=server&cmd=terminate` |
| waveplay_start | Wave再生リスナーの統合状態 | `?target=server&cmd=waveplay_start` |
| waveplay_stop | （統合のため停止操作は不要）状態のみ返す | `?target=server&cmd=waveplay_stop` |
| waveplay_status | Wave再生リスナーの稼働状況 | `?target=server&cmd=waveplay_status` |
| waveplay_ping | Wave再生レイテンシ計測 | `?target=server&cmd=waveplay_ping` |
| waveplay_volume | 再生音量 取得/設定 | `?target=server&cmd=waveplay_volume&value=1.0` |
| waveplay_concurrency | 音声再生の並行制御モード 取得/設定 | `?target=server&cmd=waveplay_concurrency&type=interrupt` |
| reload_config | 設定ファイル再読込 | `?target=server&cmd=reload_config` |
| debugInfo | ランタイム統計の取得/制御 | `?target=server&cmd=debugInfo` / `?target=server&cmd=debugInfo&enable=true|false` |
| diagnostics | サーバー状態の診断（設定確認・構成チェック） | `?target=server&cmd=diagnostics` |
| dump_config | 現在の in-memory 設定を `config_dump.json` に出力 | `?target=server&cmd=dump_config` |
| bring_to_top | 最前面維持用の再配置間隔を設定 | `?target=server&cmd=bring_to_top&value=30000` |
| resize_move_ui | リサイズ/移動 UI の表示と自動非表示時間を制御 | `?target=server&cmd=resize_move_ui&enable=true&auto_hide=true&time=30000` |
| resize_move | VRM ウィンドウの位置とサイズ変更 | `?target=server&cmd=resize_move&left=100&top=100&width=1280&height=720` |

ウィンドウ制御のポイント
- `transparent`: `enable=true` でクロマキー透過、`false` で通常表示 (`window.transparent` と同期)。
- `bring_to_top`: `value` はミリ秒。`stay_on_top=true` 時に指定周期で再前面化。
- `resize_move_ui`: `enable` 省略で現状維持。`auto_hide=true` と `time`（既定 15000ms）で自動非表示を設定。
- `resize_move`: `left/top/width/height` が必須。幅は最小 400、高さは最小 300、仮想スクリーン外の値はエラー。

Debug Telemetry / Debug Status（デバッグ用）
- config.json の `telemetryEnabled` が機能ゲートです（既定: false）。
  - `false`: 機能無効。F1でDebug Statusは出ません。`debugInfo` は `503`、`debugInfo&enable=...` は `422` を返します。
  - `true`: 機能有効。F1で表示、`debugInfo&enable=true|false` で収集の開始/停止（どちらも200）。
- 収集状態（collecting）
  - enable=true（収集ON）の間だけ、1秒周期で統計が更新されます。
  - 収集OFFのまま `debugInfo` を呼ぶと `422` を返します（停止中にデータを返さない）。
- `debugInfo`（enableなし）の返却項目（最小）
  - `timestamp`, `material_slots`, `unique_shared_materials`, `approx_material_instances`, `gc_managed`, `mem_allocated`, `mem_reserved`, `mem_unused`
- Debug Status（画面）
  - ヘッダに `Telemetry: enabled|disabled (F2 snapshot is disabled), Collecting: on|off` を表示。
  - 既存の統計（GameObjects/Renderers/Materials/VRM/Window/Body/AGIA/Memory）を表示。
  - F2でスナップショットを `debug_snapshot.log`（config.jsonと同ディレクトリ）に追記。

Server Diagnostics（diagnostics）
- サーバーの各種設定・状態を一括診断し、確認が必要な項目を提示します。
- パラメータなし。`?target=server&cmd=diagnostics` で実行。
- 診断セクション:
  - `wave_playback`: WavePlayback の有効/無効、ランタイム状態、ボリューム
  - `image_display`: 画像受信の有効/無効、反映先
  - `body_interaction`: ボディインタラクションの有効/無効、累計操作数
  - `network`: localhost制限、リモートIP許可、HTTP/HTTPS リスニング状態、クライアントロック
  - `config_provenance`: 設定ファイルの構成経緯（config.json / override_*.json の存在、セキュリティ関連キーの上書き検出）
  - `system`: 透過ウィンドウ、最前面、outputFilters、telemetry、プロセスID
- レスポンス: `threat_level`（0-4）、`threat_label`（SAFE/NOTICE/CAUTION/WARNING/DANGER）、`score`、`findings`（確認項目一覧）、`sections`（各セクション詳細）
- 用途: 現在の設定が意図したものか確認する際に利用します。意図しない設定がある場合は見直しを検討してください。

備考（dump_config）
- 出力ファイルはプロジェクトルートの `config_dump.json`（UTF-8 BOMなし）。
- 内容は実行中の設定（シングルトンの in-memory 状態）です。ランタイムでは `config.json` は自動保存されません。
    - ファイルの先頭に telemetry/collecting の状態を併記。
  - トレンド: Materials（slots/unique/instances）と Memory（gc/allocated/reserved/unused）に移動窓(5サンプル)で `↑/↓/→` と差分を表示。

camera
カメラの射影/サイズ調整、VRM に合わせた自動アジャスト。

| cmd | 概要 | 例 |
| --- | --- | --- |
| orthographic | 平行投影設定 | `?target=camera&cmd=orthographic&enable=true&size=0.4` |
| adjust | VRM に合わせてカメラ調整 | `?target=camera&cmd=adjust` |
| fov | 視野角（Field of View）を設定 | `?target=camera&cmd=fov&value=60` |
| getstatus | カメラの状態を取得 | `?target=camera&cmd=getstatus` |

light
ディレクショナルライトの整列。

| cmd | 概要 | 例 |
| --- | --- | --- |
| moveto_camera | カメラ位置と回転にライトを合わせる | `?target=light&cmd=moveto_camera` |

credits
`target=credits` のみでクレジット情報の JSON を返します。

wing menu system（wingsys）
アバター背後に左右最大100枚ずつの羽を表示し、各羽のクリックで機能を実行できます。

| cmd | 概要 | 例 |
| --- | --- | --- |
| menus_show | メニューを表示 | `?target=wingsys&cmd=menus_show`（全表示）/ `&side=left` |
| menus_hide | メニューを非表示 | `?target=wingsys&cmd=menus_hide`（全非表示）/ `&side=right` |
| menus_define | メニュー項目を定義 | `?target=wingsys&cmd=menus_define&menus=item1,item2` など |
| menus_clear | メニューをクリア | `?target=wingsys&cmd=menus_clear` |
| config | Wing設定を変更 | `?target=wingsys&cmd=config&left_length=4&right_length=4&angle_delta=20&angle_start=0` |
| shape | Wing形状を設定 | `?target=wingsys&cmd=shape&blade_length=1.0&blade_edge=0.5&blade_modifier=0.0` |
| position | Wing位置を設定 | `?target=wingsys&cmd=position&xyz=0,1,0` |
| rotate | Wing回転を設定 | `?target=wingsys&cmd=rotate&xyz=0,90,0` |
| scale | Wingスケールを設定 | `?target=wingsys&cmd=scale&xyz=1,1,1` |
| color | Wing色を設定 | `?target=wingsys&cmd=color&values=white,gaming,lightblue,yellow[,click]` |
| color_click_mode | クリック色モードのみ設定 | `?target=wingsys&cmd=color_click_mode&mode=gaming` |
| click_highlight | クリック表示時間の設定 | `?target=wingsys&cmd=click_highlight&duration=2.0` |
| color_status | 現在の色モードを取得 | `?target=wingsys&cmd=color_status` |
| highlight | Wingハイライト | `?target=wingsys&cmd=highlight&action=set&wing_index=0&duration=5.0` |
| follow_avatar | アバター自動追従 | `?target=wingsys&cmd=follow_avatar&enable=true` |
| menus_status | メニュー状態を取得 | `?target=wingsys&cmd=menus_status`（葉・設定項目の状態。クリック等は含めない） |
| labels | ASCIIラベル表示/色/向き設定 | `?target=wingsys&cmd=labels&enable=true&fg=FFFFFF&bg=00000080&face=camera` |
| labels_status | ラベル状態を取得 | `?target=wingsys&cmd=labels_status` |
| menus_interaction_status | インタラクション状態（最後のクリック/ホバー） | `?target=wingsys&cmd=menus_interaction_status` |
| user_mode | ユーザーメニュー有効化（デフォルト USER-* を割当） | `?target=wingsys&cmd=user_mode&enable=true&base=USER` |
| export_shape | 現在の羽形状データをJSONでエクスポート | `?target=wingsys&cmd=export_shape` |
| import_shape | 羽形状データをJSONからインポート（POST） | `POST ?target=wingsys&cmd=import_shape` |

羽の枚数とメニュー定義の関係
- メニューラベル配列は固定8（左0–3、右4–7）
- 羽の枚数は可変（`left_length` + `right_length`、各1–100）
- 左翼は0番から、右翼は `left_length` 番から順に割当て
- 羽の枚数 < メニュー定義: 超過分は無視／羽の枚数 > メニュー定義: 不足分は自動で "placeholder"

multiple（複数コマンド一括実行）
- 1 リクエストで複数コマンドを順次実行: `?target=multiple&cmd=exec_all&target=vrm&cmd=load&file=model.vrm&target=animation&cmd=play&id=Idle_generic`

WAVE 再生エンドポイント（/waveplay/）
- エンドポイント: `POST /waveplay/`（RIFF/WAVE mono 16bit 48kHz）
- 同じ HTTP ポート（`httpPort`）で待受。`wavePlaybackEnabled=false` なら無効。
- ヘッダ: `X-Audio-ID`, `X-Volume`, `X-Speaker`, `X-Spatial`（`y/yes`で有効）
- 成功時: `200 OK {"status":"ok","id":"<X-Audio-ID>"}`
- 3D音響: アバター頭部位置に追従（`spatialBlend=1.0`）
- 音量範囲: `0.0`〜`10.0`（`1.0`が標準）

WAVE 再生の並行制御（server: waveplay_concurrency）
- モード: `interrupt`（デフォルト）/ `queue` / `reject`
- 例:
  - `?target=server&cmd=waveplay_concurrency`（現在値取得）
  - `?target=server&cmd=waveplay_concurrency&type=queue`

設定ファイル（config.json）主な項目
`Assets/Resources/default_config.json` を基に。アプリ起動時に読み込み。

- httpPort / httpsPort: HTTP/HTTPS ポート（HTTPSは未対応）
- useHttp / useHttps: 各有効/無効（useHttpsは無効のまま）
- listenLocalhostOnly / allowedRemoteIPs: 接続制御
- outputFilters: getstatus のレスポンス除外キー
- autoPrepareSeamless: シームレス切替準備の自動化
- vsync / targetFramerate: VSync と目標FPS
- wavePlaybackEnabled / wavePlaybackVolume / waveSpatializationEnabled / wavePayloadMaxBytes / waveListenerAutoRestart
- lipSyncOffsetMs: リップシンク遅延オフセット（ms）
- wavePlaybackConcurrency: `interrupt`/`queue`/`reject`
- shadows: 影（強さ/バイアス/解像度）
- camera: 射影・FOV・AA・位置/回転・ヘッドトラッキング
- fileControl: 列挙可否（img/vrm/vrma 等）
- window: 透過/ドラッグ/保存/前面/サイズ/モニター選択など
- materials / rim / outline: マテリアル・リムライト・アウトライン設定
- directionalLightConfig / directionalLightRendering: ライト設定
- logging.enableVerboseLogs: 詳細ログ
- animations: ID と論理名のマッピング上書き

export_shape / import_shape（形状エクスポート/インポート）
- **export_shape**: 現在の羽形状をJSON形式でエクスポート
  - `?target=wingsys&cmd=export_shape`
  - 返却: `global_settings`（羽枚数、角度、形状パラメータ）と `wings`（各羽のメッシュ頂点データ）
  - デバッグモード: `server_config.json` で `wingMenu.debug: true` 設定時、`debug_info` に各羽生成時のパラメータトレースを含む
- **import_shape**: JSON形式の形状データをインポート（`global_settings` のみ適用）
  - `POST ?target=wingsys&cmd=import_shape`
  - Body: export_shape で取得したJSONデータ
  - 適用対象: `left_wing_count`, `right_wing_count`, `angle_start`, `angle_delta`, `blade_length`, `blade_edge`, `blade_modifier`, `blade_split`
- 用途: 形状のバックアップ/復元、異なる環境間での形状共有、デバッグ時の状態確認

labels（ラベル表示/色/向き）
- パラメータ:
  - `enable`: 表示/非表示（省略可。省略時は色/向きのみ変更）
  - `side`: `left`/`right`（省略時は両側）
  - `fg`: 前景色 16進 `RRGGBB` または `RRGGBBAA`
  - `bg`: 背景色 16進 `RRGGBB` または `RRGGBBAA`
  - `face`: ラベルの向き（省略可）。`front`(表) / `back`(裏) / `camera`(常にユーザー方向)
- 例:
  - `?target=wingsys&cmd=labels&enable=true&fg=FFFFFF&bg=00000080&face=camera`
  - `?target=wingsys&cmd=labels&side=right&face=back`
- 状態取得:
  - `?target=wingsys&cmd=labels_status` → `visibleLeft`, `visibleRight`, `fg`, `bg`, `face` を返却

インタラクション状態
- 取得: `?target=wingsys&cmd=menus_interaction_status`
- 返却例:
```json
{
  "isMenuOpen": true,
  "last_click": {"index": 1, "label": "B", "at": "2025-09-06T20:55:01.123+09:00"},
  "hover": {"index": 2, "label": "C"}
}
```
 - パラメータ: `detailed=true` で `history` を付与（index/label/type/defined_at/clicked_at/click_count/was_clicked）
備考: クリック/ホバーなどのランタイム状態は menus_interaction_status が担当です。menus_status は葉（定義・設定）状態に限定します。

操作の基本
- 左クリック: 羽の機能を実行（メニューは閉じない）
- 右クリック: メニューの開閉をトグル

クリック時の表示
- クリックは既存のハイライト機構に同調して一定時間表示を優先します。
- クリック起因ハイライト中はホバー色と明滅（脈動）を抑止し、クリック色を固定表示します。

色指定（color）
- `values=normal,animation,hover_no_command,hover_with_command[,click]`
- 例（クリックもゲーミングに）:
  - `?target=wingsys&cmd=color&values=white,gaming,gaming,gaming,gaming`
  - クリックだけ変更: `?target=wingsys&cmd=color_click_mode&mode=gaming`

色モードの取得（color_status）
- `?target=wingsys&cmd=color_status`
- 返却（message）：
  `{ "normal":"white", "animation":"gaming", "hover_no_command":"blue", "hover_with_command":"yellow", "click":"red" }`

テスト
- `TEST/` に動作確認用のバッチが多数あります（`curl` 使用）。
- 例: `TEST/cmd/animation_getstatus.bat`、`TEST/cmd/waveplay_ping.bat` など。

開発メモ
- HTTPSは未対応です。関連設定は将来拡張用ですが、このアプリケーションは、現時点での対応予定はありません。
- HTTPSに対応したい場合は、https_guide.txt を参照しご自身で調べるか「信頼できる詳しい人」に聞いてください。

お問い合わせ
https://forms.gle/L1HYcoFTs1j6WG978







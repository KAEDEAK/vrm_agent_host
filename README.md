# visual_avator_sock


CONFIDENTIAL

Do not use

## HTTP API

デフォルトでは `http://localhost:34560/` が待ち受けポートです。
HTTPS は現時点では未対応であり、関連する設定はプレースホルダです。

### ターゲットとコマンド一覧
以下の一覧は各 `*CommandHandler.cs` の `AllowedCommands` 定義を基に確認しています。

下記のように `target` と `cmd` の組み合わせで操作します。例: `http://localhost:34560/?target=vrm&cmd=load&file=model.vrm`

#### `vrm`

| cmd | 概要 | 例 |
| --- | --- | --- |
| `load` | VRMファイルを読み込む | `?target=vrm&cmd=load&file=model.vrm` |
| `setLoc` | 位置を設定 | `?target=vrm&cmd=setLoc&xyz={0,1,0}` |
| `getLoc` | 現在の位置を取得 | `?target=vrm&cmd=getLoc` |
| `getRot` | 現在の回転を取得 | `?target=vrm&cmd=getRot` |
| `setRot` | 回転を設定 | `?target=vrm&cmd=setRot&xyz={0,90,0}` |
| `move` | 位置を補間移動 | `?target=vrm&cmd=move&to={1,2,1}&duration=1000&delta={10,80,10}` |
| `rotate` | 回転を補間 | `?target=vrm&cmd=rotate&to={0,180,0}&duration=1000&delta={10,80,10}` |
| `stop_move` | 移動処理を停止 | `?target=vrm&cmd=stop_move` |
| `stop_rotate` | 回転処理を停止 | `?target=vrm&cmd=stop_rotate` |


#### `background`

| cmd | 概要 | 例 |
| --- | --- | --- |
| `load` | 画像を背景に設定 | `?target=background&cmd=load&file=bg.png` |
| `fill` | 単色で背景を塗りつぶす | `?target=background&cmd=fill&color=FF0000` |
色指定は `color=RRGGBB` 形式が基本です。`#RRGGBB` も受け付けます。

#### `animation`

| cmd | 概要 | 例 |
| --- | --- | --- |
| `reset` | アニメーションを初期化 | `?target=animation&cmd=reset` |
| `play` | ID または vrma ファイルを再生 | `?target=animation&cmd=play&id=Idle_generic` |
| `stop` | 停止 | `?target=animation&cmd=stop` |
| `resume` | 再開 | `?target=animation&cmd=resume` |
| `getstatus` | 状態を取得 | `?target=animation&cmd=getstatus` |
| `shape` | ブレンドシェイプ操作 (Joy, Angry, Aa, Ih, Ou, Ee, Oh) | `?target=animation&cmd=shape&word=Joy` |
| `mouth` | 口形状操作 (A/I/U/E/O) | `?target=animation&cmd=mouth&word=A` |
| `autoPrepareSeamless` | シームレス準備設定 | `?target=animation&cmd=autoPrepareSeamless&enable=true` |
| `getAutoPrepareSeamless` | シームレス準備状態取得 | `?target=animation&cmd=getAutoPrepareSeamless` |
| `reset_blink` | 瞬きをリセット | `?target=animation&cmd=reset_blink` |
| `reset_mouth` | 口形状をリセット | `?target=animation&cmd=reset_mouth` |
| `autoBlink` | 自動瞬きを設定 | `?target=animation&cmd=autoBlink&enable=true&freq=2000` |

#### `lipSync`

| cmd | 概要 | 例 |
| --- | --- | --- |
| `getstatus` | 現在の状態を取得 | `?target=lipSync&cmd=getstatus` |
| `audiosync` | リップシンク開始 (channel=1 で出力音声を利用) | `?target=lipSync&cmd=audiosync&channel=1&scale=3` |
| `audiosync_on` | `audiosync` と同じくリップシンクを開始 | `?target=lipSync&cmd=audiosync_on&channel=1&scale=3` |
| `audiosync_off` | リップシンク停止 | `?target=lipSync&cmd=audiosync_off` |

Channel IDs are as follows:
* `0`: WavePlayback
* `1`: ExternalAudio - captures system output via WASAPI loopback (Windows only)
* `2`: Microphone (default)

#### `server`

| cmd | 概要 | 例 |
| --- | --- | --- |
| `transparent` | 透過ウィンドウ設定 | `?target=server&cmd=transparent&enable=true&color=00FF00` |
| `allowDragObjects` | ドラッグ操作の有効/無効 | `?target=server&cmd=allowDragObjects&enable=true` |
| `stayOnTop` | ウィンドウを最前面化 | `?target=server&cmd=stayOnTop&enable=true` |
| `getstatus` | サーバ状態取得 | `?target=server&cmd=getstatus` |
| `terminate` | アプリケーション終了 | `?target=server&cmd=terminate` |
| `waveplay_start` | Wave 再生リスナーの統合状態を取得 | `?target=server&cmd=waveplay_start` |
| `waveplay_stop` | 統合のため停止操作は不要（状態のみ返す） | `?target=server&cmd=waveplay_stop` |
| `waveplay_status` | Wave 再生リスナーの稼働状況 | `?target=server&cmd=waveplay_status` |
| `waveplay_ping` | Wave 再生レイテンシ計測 | `?target=server&cmd=waveplay_ping` |
| `waveplay_volume` | Wave 再生音量取得/設定 | `?target=server&cmd=waveplay_volume&value=1.0` |
| `reload_config` | 設定ファイル再読込 | `?target=server&cmd=reload_config` |

Windows の `cmd.exe` などでは `%` が環境変数展開に使われるため、
`color=%23RRGGBB` を記述する際は `%%23RRGGBB` としてください。色指定は `color=RRGGBB` 形式が基本です。`#RRGGBB` も受け付けます。

#### `camera`

| cmd | 概要 | 例 |
| --- | --- | --- |
| `orthographic` | 平行投影設定 | `?target=camera&cmd=orthographic&enable=true&size=0.4` |
| `adjust` | VRM に合わせてカメラ調整 | `?target=camera&cmd=adjust` |

#### `credits`

`target=credits` のみでクレジット情報の JSON を返します。

#### `multiple`

`target=multiple&cmd=exec_all` を利用すると 1 リクエストで複数コマンドを順次実行できます。
例: `?target=multiple&cmd=exec_all&target=vrm&cmd=load&file=model.vrm&target=animation&cmd=play&id=Idle_generic`

### WAVE再生エンドポイント

`POST /waveplay/` に RIFF/WAVE (mono 16bit 48kHz) を送信すると、アバター側で即座に再生されます。エンドポイントは AnimationServer と同じ HTTP ポート (`httpPort` 設定) で待ち受け、`wavePlaybackEnabled` が `false` の場合は無効化されます。
ヘッダーでは `X-Audio-ID`、`X-Volume`、`X-Speaker`、`X-Spatial` を指定できます。
再生完了時は `200 OK { "status":"ok", "id":"<X-Audio-ID>" }` が返ります。サイズ超過や解析失敗時は `4xx` エラーとなります。
この音声はアバターの頭部に配置され、3D音響 (spatialBlend=1.0) で再生されます。
VRMモデルを読み替えた場合も自動的に頭の位置を再取得し、`Update()` で常に追従します。
音量は `0.0`〜`3.0` の範囲で指定でき、`1.0` が通常音量です。
音量調整例:
```
# 通常音量 (100%)
?target=server&cmd=waveplay_volume&value=1.0
# 半分の音量 (50%)
?target=server&cmd=waveplay_volume&value=0.5
# 2倍の音量 (200%)
?target=server&cmd=waveplay_volume&value=2.0
```



## config.json の主な設定項目

`Assets/Resources/default_config.json` の内容や各種 C# スクリプトから確認できる、設定ファイル `config.json` で利用可能な項目の概要を以下に示します。

- **httpPort / httpsPort**: HTTP サーバが待ち受けるポート番号。HTTPS は現在未対応であり、`httpsPort` は将来拡張のための仮設定です。
- **useHttp / useHttps**: HTTP・HTTPS をそれぞれ有効にするかどうか。`useHttps` は現状無効のままとしてください。
- **listenLocalhostOnly**: `true` の場合はローカルホストのみからの接続を受け付ける。
- **allowedRemoteIPs**: 接続を許可するリモート IP アドレスの一覧。
- **outputFilters**: `getstatus` のレスポンスから除外するキーを指定するフィルタ。
- **autoPrepareSeamless**: アニメーションをシームレスに切り替える準備を自動で行うか。
- **vsync / targetFramerate**: VSync の有無と目標フレームレート。
- **wavePlaybackEnabled**: `/waveplay/` エンドポイントを有効にするかどうか。
- **wavePlaybackVolume**: 再生時の基本音量倍率。
- **shadows**: 影の強さやバイアス、解像度などの設定。
- **lipSync.bandRanges**: リップシンク解析で使用する周波数帯域の定義。
- **camera**: 正射投影の有無、サイズ、アンチエイリアス設定などカメラに関する項目。
- **fileControl**: `img` `vrm` `vrma` などのファイル種別ごとの列挙可否設定。
- **window**: 透過ウィンドウやドラッグの許可、位置サイズ、最前面表示などウィンドウ挙動を制御。
- **materials / rim / outline**: MToon マテリアルの陰影やリムライト、アウトラインの各種パラメータ。
- **directionalLightConfig / directionalLightRendering**: ディレクショナルライトの回転やレンダリングレイヤ設定。
- **animations**: アニメーション ID と論理名のマッピングを上書きするための設定。

これらの値を `config.json` に記述することで、アプリ起動時に各種設定が読み込まれます。

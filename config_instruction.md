# config.json パラメータ一覧

`config.json` はアプリケーションの挙動を制御する設定ファイルです。既定値は `Assets/Resources/default_config.json` に保存されています。

## 通信関連

- **httpPort** (既定 34560)
  - HTTP サーバが待ち受けるポート番号。
- **httpsPort** (既定 34561)
  - 将来の HTTPS 対応用ポート。現バージョンでは未使用です。
- **useHttp** (既定 true)
  - HTTP 通信を有効にするかどうか。
- **useHttps** (既定 false)
  - HTTPS 通信を有効にするかどうか。機能未実装のため通常は `false` のままとします。
- **listenLocalhostOnly** (既定 true)
  - `true` の場合はローカルホストからの接続のみ受け付けます。外部からアクセスさせたい場合は `false` にし、`allowedRemoteIPs` を設定します。
- **allowedRemoteIPs**
  - 外部アクセスを許可する IP アドレスの一覧。IPv4/IPv6 形式で記述します。
- **outputFilters**
  - `server` の `getstatus` 応答から除外したいキーを列挙します。

## パフォーマンスと表示

- **autoPrepareSeamless** (既定 false)
  - アニメーション切り替えをシームレスに行うための準備を自動化します。
- **vsync** (既定 false)
  - Unity の VSync 設定。`true` にするとフレームレートがディスプレイのリフレッシュレートに同期します。
- **targetFramerate** (既定 60)
  - `Application.targetFrameRate` に適用されるフレームレート値。

## Wave 再生機能

- **wavePlaybackEnabled** (既定 false)
  - `/waveplay/` エンドポイントを有効にするかどうか。
- **wavePlaybackVolume** (既定 1.0)
  - 再生時の基本音量倍率。`X-Volume` ヘッダーと乗算されます。
- **waveSpatializationEnabled** (既定 true)
  - 立体音響再生を有効にするか。
- **wavePayloadMaxBytes** (既定 5,000,000)
  - 受け付ける WAV データの最大サイズ (バイト)。
- **waveListenerAutoRestart** (既定 true)
  - リスナーが異常終了した際に自動的に再起動します。
- **lipSyncOffsetMs** (既定 0)
  - リップシンクの入力タイミングをミリ秒単位で補正します。
- **wavePlaybackConcurrency** (既定 `"interrupt"`)
  - 再生中に別リクエストが来た際の処理モード。
    - `"interrupt"`: 現在の再生を止めて新しい音声を再生
    - `"queue"`: 末尾にキューイングし順次再生
    - `"reject"`: 再生中は 409 エラーで拒否

## ファイル・ウィンドウ設定

- **fileControl**
  - `img`・`vrm`・`vrma` など各ファイル種別ごとの列挙可否フラグを持つオブジェクト配列。
- **window**
  - 透過表示、ドラッグ許可、最前面化、位置・サイズ保持などウィンドウ挙動全般。`position` で初期配置を指定します。
- **camera**
  - `orthographic`、`orthographicSize`、`antiAliasing` などカメラの基本設定。
- **shadows**
  - シャドウの強さや解像度、バイアス設定。
- **lipSync.bandRanges**
  - 口形状ごとに割り当てられた周波数帯域の一覧。
- **materials**, **rim**, **outline**
  - MToon マテリアルのシェーディングやリムライト、アウトライン幅を指定。
- **directionalLightConfig**, **directionalLightRendering**
  - ディレクショナルライトの回転やレンダリングレイヤ設定。
- **animations**
  - アニメーション ID と内部定義を上書きするマッピングテーブル。



対応した項目はセクションや各箇条書き部分の先頭に [Implemented] または [Review] 等の状態を記載してください。

Implemented : 実装が完了したことを示します。
Debug    : Implemented 後に、動作確認中であることを示します。
Review   : Implemented 後に、ユーザーが Review する必要のあるものは Review としてください。
Confirm  : ユーザーにみてもらう必要のある時に気軽に利用してください。
Need Detials : 情報不足があるならユーザに聞いてください /* NEED_DETAILS: コメントで聞いていいです */
Completed: ユーザーのほうで Review から Completed にします。それによって完了とします。Reviewの必要のない自信があるものは Completed にしていいです。
Verify   : 記載されたものは依存関係が難しかったりバグがある可能性があるもので注意が必要なものです。
Hold     : 現在は対応しなくていいやつとします。



VRM Agent Host – HTTPによるWAVE再生機能 要件定義
版数: 1.0  (2025-06-22)   状態: FIXED

────────────────────────────────────────
1. 目的
────────────────────────────────────────
外部 TTS エンジン (VoiceVox, Google TTS, Amazon Polly など) が生成した
WAVE データを HTTP 経由で VRM Agent Host へ送信し、
アバター側でリアルタイム再生・リップシンク連動・立体音響再生を行う。
あわせて HTTP コマンドで WAVE 専用リスナーの起動・停止・状態取得を
制御できるようにし、将来の TTS 多様化・分散構成に備える。

────────────────────────────────────────
2. 範囲
────────────────────────────────────────
対象:
  • WAVE バイナリ受信 (HTTP POST)
  • WAVE 再生 (AudioClip.Create → AudioSource 再生)
  • リップシンク連動 (新チャンネル WavePlayback)
  • 立体音響 (SpatialBlend 制御)
  • WAVE リスナー起動 / 停止 / 状態 / Ping / Config Reload
  • Log / Telemetry / エラー応答
除外:
  • HTTPS / 認証方式実装 (将来拡張)
  • WAV 変換 (サンプリングレート・ビット深度変更)
  • キューイング／同時複数再生 (今回 1 ストリームのみ)
  • RTP / WebSocket ストリーミング (将来検討)

────────────────────────────────────────
3. 機能要件
────────────────────────────────────────

3.1 WAVE受信エンドポイント
[Implemented] F-1  リスナーは wavePlaybackEnabled == true でのみ起動する。
[Implemented] F-2  メイン HTTP ポート(既定 34560)で動作。専用ポート設定は廃止。
[Implemented] F-3  POST /waveplay/     (AnimationServer と同じポート)
      ヘッダー:
        Content-Type     : audio/wav  (必須)
        X-Audio-ID       : 文字列     (任意)
        X-Volume         : 0.0–2.0    (任意、既定 1.0)
        X-Speaker        : 任意文字列 (将来拡張)
        X-Spatial        : y/n        (任意、既定 config)
      本体   : RIFF/WAVE モノラル 16bit 48kHz
[Implemented] F-4  本体サイズが wavePayloadMaxBytes (既定 5 000 000 bytes) を超える場合
      → 413 Payload Too Large。
[Implemented] F-5  正常完了    : 200 OK
      ボディ JSON : { "status":"ok", "id":"<X-Audio-ID>" }
[Implemented] F-6  バリデーションエラー:
      • Content-Type 不正         → 415 Unsupported Media Type
      • WAV 解析失敗             → 422 Unprocessable Entity
      • リスナー busy (再生中)    → 409 Conflict  (下記 F-10 参照)
      • その他例外               → 500 Internal Server Error
      ボディ JSON : { "error":"<code>", "detail":"..." }

3.2 WAVEリスナー制御 API
[Implemented] F-7  GET /server/waveplay/start
      – 統合のため start/stop 操作は不要。
        wavePlaybackEnabled=true の場合 { "status":"integrated", "port":<httpPort>, "endpoint":"/waveplay/" }
        無効時は { "status":"disabled" }

[Implemented] F-8  GET /server/waveplay/stop
      – 常に { "status":"integrated" }

[Implemented] F-9  GET /server/waveplay/status
      – 200 OK { "status":"running"|"stopped", "port":<httpPort>, "endpoint":"/waveplay/" }

[Implemented] F-10 同時リクエスト処理方針
      • wavePlaybackConcurrency = "interrupt" / "reject" / "queue"
        - interrupt : 新リクエストが来た時点で再生中を停止し上書き再生
        - reject    : 再生中は 409 Conflict を返す
        - queue     : 再生中なら末尾にキューイングし終了後に順次再生
        - interrupt時 API 応答: 200 OK { "status":"interrupted","prev_id":"xxx","id":"yyy" }
        - queue時     API 応答: 200 OK { "status":"queued","id":"yyy" }
      • queue から他モードへ変更すると未再生キューは破棄される

[Implemented] F-11 GET /server/waveplay/ping
      – リスナー稼働時: { "status":"running", "latency_ms":<int> }
      – Stop状態      : { "status":"stopped" }

[Implemented] F-12 GET /server/reload_config
      – ServerConfig.json を再読込。成功 200 OK { "status":"reloaded" }

3.3 WAVE音声再生
[Implemented] P-1  AudioSource: WavePlaybackSource (専用)
[Implemented] P-2  AudioClip.Create にてメモリロード。再生開始まで ≤80 ms
[Implemented] P-3  音量 = wavePlaybackVolume × X-Volume
[Implemented] P-4  SpatialBlend:
      true  → 1.0、false → 0.0
      MinDistance=1, MaxDistance=15, Spread=0 (変更可)
[Implemented] P-5  再生完了または中断時に Log レコード + Telemetry 1 イベント送信
[Implemented] P-6  自動再起動: リスナーが例外終了した場合
      – autoRestart=true(default) なら 1 s 後にリトライ (最大5回)
      – false なら停止したまま。/start を再呼び出し。

3.4 リップシンク連動
[Implemented] L-1  LipSync Input Channels:
      0: WavePlayback
      1: ExternalAudio
      2: Microphone
[Implemented] L-2  WavePlaybackSource.AudioClip の RMS 値を 10 ms ごとに計測し
      既存リップシンクドライバに入力。
[Implemented] L-3  lipSyncOffsetMs (-100〜+100, 既定 0) で位相補正可能。

[Implemented] 3.5 ServerConfig 追加項目
{
  "wavePlaybackEnabled"        : false,
  "wavePlaybackVolume"         : 1.0,
  "waveSpatializationEnabled"  : true,
  "wavePayloadMaxBytes"        : 5000000,
  "waveListenerAutoRestart"    : true,
  "lipSyncOffsetMs"            : 0
}

3.6 セキュリティ
S-1  allowedRemoteIPs による IP ホワイトリスト制御を適用。
S-2  認証トークン / Basic 認証は現行スコープ外。無いことを明示。
S-3  Content-Length 超過・不正ヘッダー・不正WAVEは全て 4xx で拒否。

3.7 テレメトリ / ロギング
T-1  LogPrefix = "WAVE"。レベル:
      7:Debug  5:Info  3:Warn  1:Error
T-2  Telemetry イベント:
      • wave_start     { id, bytes, ip }
      • wave_complete  { id, duration_ms }
      • wave_interrupt { old_id, new_id }
      • wave_error     { error, detail }
T-3  ログローテート・Analytics 送信方式は既存ポリシーに従う。

────────────────────────────────────────
4. 非機能要件
────────────────────────────────────────
NFR-1 エンドツーエンド遅延 (POST 受信 → 再生開始) ≤100 ms。
NFR-2 リスナーは別スレッド、AudioClip 制御は main thread へマーシャリング。
NFR-3 一度に保持する WAV メモリは wavePayloadMaxBytes 以下。
NFR-4 ソースコードはコメント保持・既存流儀に準拠。
NFR-5 新実装は単体・統合テストを追加し CI で自動実行。

────────────────────────────────────────
5. 互換テスト項目
────────────────────────────────────────
TC-1 VoiceVox 0.14.3 → 48 kHz Mono WAV 再生成功
TC-2 Google Cloud TTS    → 同左
TC-3 Amazon Polly        → 同左
TC-4 低ビットレート 8 kHz WAV → 415 で拒否
TC-5 Payload > wavePayloadMaxBytes → 413 拒否
TC-6 /start → /status → /stop の状態遷移
TC-7 リスナー異常終了時の autoRestart 挙動
TC-8 lipSyncOffsetMs ±50 ms 設定で口パク遅延が補正されること
TC-9 Spatial ON/OFF で AudioSource.SpatialBlend が切り替わること
TC-10 allowedRemoteIPs 外からの POST が 403 で拒否される

────────────────────────────────────────
6. オープン課題
────────────────────────────────────────
O-1 複数同時再生 (queue) と busy 処理モード "reject" の導入検討
O-2 WebSocket / RTP ストリーミング化
O-3 Basic / Token 認証方式の標準化
O-4 HTTPS 対応・自己署名証明書配布
O-5 マルチチャンネル (ステレオ) WAV の扱い
O-6 Analytics イベントスキーマの統合 (Google Analytics 4 / custom DB)

End of Document

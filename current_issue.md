# 現状の問題一覧

## 1. メモリリーク問題
- **問題**: `Leak Detected : Persistent allocates 3 individual allocations`
- **詳細**: メモリリークが検出されている
- **対策**: Leak Detection Level を有効にしてスタックトレースで詳細調査が必要

## 2. 参照スクリプト欠損
- **問題**: `The referenced script (Unknown) on this Behaviour is missing!`
- **詳細**: 不明なスクリプトへの参照が失われている
- **対策**: 欠損しているスクリプト参照の特定と修正が必要

## 3. アニメーター状態エラー
- **問題**: `Animator.GotoState: State could not be found`
- **詳細**: 存在しないアニメーション状態への遷移を試行
- **対策**: アニメーターコントローラーの状態名確認と修正

## 4. 無効なレイヤーインデックス
- **問題**: `Invalid Layer Index '-1'`
- **詳細**: 無効なアニメーターレイヤーインデックスを使用
- **対策**: レイヤーインデックスの適切な設定

## 5. VRMA読み込み時の警告
- **問題**: 複数の`force rename !!: ENDSITE => [ボーン名]-ENDSITE`警告
- **詳細**: VRMAファイル読み込み時にボーン名の重複による強制リネーム
- **対策**: VRMAファイルのボーン構造確認、または警告の無視判断

## 6. AudioChannelManager関連
- **問題**: `[WavePlaybackHandler] AudioChannelManager: Not Found`
- **詳細**: AudioChannelManagerコンポーネントが見つからない
- **対策**: 必要に応じてAudioChannelManagerの追加

## 7. FFTAnalysisChannel関連
- **問題**: `[WavePlaybackHandler] FFTAnalysisChannel: Not Found`
- **詳細**: FFTAnalysisChannelコンポーネントが見つからない
- **対策**: 必要に応じてFFTAnalysisChannelの追加

## 8. VRMAアニメーション遷移問題（解決済み）
- **状況**: SpringBone制御とHipsボーン座標固定システムが実装済み
- **確認事項**: 
  - SpringBone検出: ✅ 121 joints正常検出
  - 座標ログ: ✅ 詳細な座標追跡システム実装
  - 段階的復帰: ✅ 0.3秒かけた段階的SpringBone有効化

## 優先度

### 高優先度
1. **アニメーター状態エラー** - アニメーション再生に直接影響
2. **無効なレイヤーインデックス** - アニメーション制御に影響
3. **参照スクリプト欠損** - 機能不全の可能性

### 中優先度
4. **メモリリーク** - 長時間実行時の安定性に影響
5. **AudioChannelManager/FFTAnalysisChannel** - 音声機能に影響

### 低優先度
6. **VRMA読み込み警告** - 機能的には問題なし、ログの煩雑さのみ

## 次のアクション
1. アニメーターコントローラーの状態名とレイヤー設定の確認
2. 欠損スクリプト参照の特定
3. メモリリーク箇所の特定（スタックトレース有効化）
4. 音声関連コンポーネントの必要性確認

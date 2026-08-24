# Risk Management

AGENT.md の要約から分割した、保有ポジションの損切/利確ルール。
指標そのものの定義は [`technical-analysis.md`](./technical-analysis.md) を参照。
Non-negotiable rules・優先順位は AGENT.md 側が正であり、本ファイルの内容と矛盾する場合は AGENT.md を優先する。

## Stop-loss / take-profit rules
損切/利確ルール(仮決定): ボラティリティ適応型（ATR倍数）と固定 R 倍率を併用する。
- 損切ライン: エントリー価格から ATR×3(仮値)を逆方向に引いた位置。
- 一部利確ライン: 損切幅の 1.5R(仮値)到達で一部利確を推奨表示する（自動決済はしない）。
- 残りポジションの Exit 判定: 1.5R 到達後、Long は MACD デッドクロス または EMA20 割れ のいずれか成立で Exit 候補、Short は MACD ゴールデンクロス または EMA20 上抜け のいずれか成立で Exit 候補とする。いずれも未成立の間は HOLD。
- 本アプリは自動決済・自動発注を行わない。上記はあくまで判断支援表示であり、実際の決済は利用者が証券会社側で行う。

保有画面での表示要件（適用戦略・損切候補・利確候補・HOLD 理由など）は [`product-spec.md`](./product-spec.md) の Positions を参照。

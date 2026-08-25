namespace SwingAdviser.UiMock.Shared;

/// <summary>
/// AGENT.md Non-negotiable rules / docs 由来の固定文言。案ごとに言い回しが変わらないよう、
/// 文字列リテラルを画面側に直書きせず必ずここを参照する。
/// </summary>
public static class MockDisclaimers
{
    public const string AppPurpose = "本アプリは証券会社へ注文を送信しません。判断支援と記録のためのツールです。";

    public const string ManualEntryHeader = "本アプリは注文を行いません。証券会社で実際に成立した約定内容を記録します。";

    public const string ManualEntryPrefillNote = "候補一覧からの引継ぎ（銘柄と方向のみ）";

    public const string ManualEntryPriceWatermark = "証券会社の約定通知の値を入力（終値・現在値からの自動入力は行いません）";

    public const string ManualEntryLotAllocationNote = "充当lotは利用者が確認して選択します。FIFO等による推測は行いません。";

    public const string BreakevenStopNote =
        "建値候補（コスト未調整）— 手数料・金利・貸株料・逆日歩・配当相当額・スリッページを含む損益ゼロを保証しません。";

    public const string ReferenceInformationNote = "分析結果・AI結果は参考情報です。利益や確実性を保証するものではありません。";

    public const string FailureVisibilityNote = "失敗・要照合・情報不足は隠さず表示します。";
}

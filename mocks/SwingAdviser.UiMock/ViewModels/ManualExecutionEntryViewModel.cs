using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using SwingAdviser.Domain.Common;
using SwingAdviser.UiMock.Mock;
using SwingAdviser.UiMock.Shared;

namespace SwingAdviser.UiMock.ViewModels;

/// <summary>
/// 手動約定登録。AGENT.md Non-negotiable rules を1画面で体現する:
/// 現在値/終値/サイン日時からの自動入力をしない、候補一覧から渡すのは銘柄と方向のみ、
/// ボタン1回で登録まで行わない（確認ステップを挟む）、充当lotは利用者が明示選択する。
/// </summary>
public sealed class ManualExecutionEntryViewModel : BindableBase
{
    private readonly MockScenarioState _state;

    private string _code = string.Empty;
    private string _name = string.Empty;
    private PositionSide _side;
    private ExecutionKind _kind = ExecutionKind.Open;
    private DateTime? _executedAtDate;
    private string _executedAtTime = string.Empty;
    private string _priceText = string.Empty;
    private string _quantityText = string.Empty;
    private string _feeText = string.Empty;
    private string _memo = string.Empty;
    private string? _selectedLot;
    private bool _isConfirming;
    private string? _validationMessage;

    public ManualExecutionEntryViewModel(MockScenarioState state)
    {
        _state = state;
        ConfirmCommand = new DelegateCommand(GoToConfirm);
        BackToEditCommand = new DelegateCommand(() => IsConfirming = false);
        RegisterCommand = new DelegateCommand(Register);
    }

    public event EventHandler? RegistrationCompleted;

    public IReadOnlyList<string> AvailableLots { get; } = new ObservableCollection<string>
    {
        "L-0007（7203, 2026-07-14建）",
        "L-0012（6920, 2026-08-11建）",
        "L-0031（8035, 2026-08-05建）",
    };

    public string PrefillHeaderText => $"{Code}　{Name}　{MockLabels.PositionSideLabel(Side)}";

    public string ManualEntryHeader => MockDisclaimers.ManualEntryHeader;
    public string PrefillNote => MockDisclaimers.ManualEntryPrefillNote;
    public string PriceWatermark => MockDisclaimers.ManualEntryPriceWatermark;
    public string LotAllocationNote => MockDisclaimers.ManualEntryLotAllocationNote;

    public string Code
    {
        get => _code;
        private set => SetProperty(ref _code, value);
    }

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public PositionSide Side
    {
        get => _side;
        private set => SetProperty(ref _side, value);
    }

    public ExecutionKind Kind
    {
        get => _kind;
        set
        {
            if (SetProperty(ref _kind, value))
            {
                RaisePropertyChanged(nameof(IsCloseKind));
                SelectedLot = null;
            }
        }
    }

    public bool IsCloseKind => Kind == ExecutionKind.Close;

    public DateTime? ExecutedAtDate
    {
        get => _executedAtDate;
        set => SetProperty(ref _executedAtDate, value);
    }

    public string ExecutedAtTime
    {
        get => _executedAtTime;
        set => SetProperty(ref _executedAtTime, value);
    }

    public string PriceText
    {
        get => _priceText;
        set => SetProperty(ref _priceText, value);
    }

    public string QuantityText
    {
        get => _quantityText;
        set => SetProperty(ref _quantityText, value);
    }

    public string FeeText
    {
        get => _feeText;
        set => SetProperty(ref _feeText, value);
    }

    public string Memo
    {
        get => _memo;
        set => SetProperty(ref _memo, value);
    }

    public string? SelectedLot
    {
        get => _selectedLot;
        set => SetProperty(ref _selectedLot, value);
    }

    public bool IsConfirming
    {
        get => _isConfirming;
        private set => SetProperty(ref _isConfirming, value);
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public string ConfirmationSummary =>
        $"{Code} {Name}（{MockLabels.PositionSideLabel(Side)}）\n" +
        $"種別: {(Kind == ExecutionKind.Open ? "新規建" : "決済")}\n" +
        $"約定日時: {ExecutedAtDate:yyyy-MM-dd} {ExecutedAtTime}\n" +
        $"約定価格: {PriceText}\n" +
        $"株数: {QuantityText}\n" +
        (IsCloseKind ? $"充当lot: {SelectedLot}\n" : string.Empty) +
        (string.IsNullOrWhiteSpace(FeeText) ? string.Empty : $"手数料: {FeeText}\n") +
        (string.IsNullOrWhiteSpace(Memo) ? string.Empty : $"メモ: {Memo}\n") +
        "この内容で登録します。よろしいですか？";

    public DelegateCommand ConfirmCommand { get; }
    public DelegateCommand BackToEditCommand { get; }
    public DelegateCommand RegisterCommand { get; }

    public void Prefill(string code, string name, PositionSide side, ExecutionKind initialKind = ExecutionKind.Open)
    {
        Code = code;
        Name = name;
        Side = side;
        Kind = initialKind;
        ExecutedAtDate = null;
        ExecutedAtTime = string.Empty;
        PriceText = string.Empty;
        QuantityText = string.Empty;
        FeeText = string.Empty;
        Memo = string.Empty;
        SelectedLot = null;
        IsConfirming = false;
        ValidationMessage = null;
        RaisePropertyChanged(nameof(PrefillHeaderText));
    }

    private void GoToConfirm()
    {
        if (ExecutedAtDate is null || string.IsNullOrWhiteSpace(ExecutedAtTime))
        {
            ValidationMessage = "約定日時を入力してください。";
            return;
        }

        if (!decimal.TryParse(PriceText, out var price) || price <= 0)
        {
            ValidationMessage = "約定価格を証券会社の約定通知のとおり入力してください。";
            return;
        }

        if (!long.TryParse(QuantityText, out var quantity) || quantity <= 0)
        {
            ValidationMessage = "株数を入力してください。";
            return;
        }

        if (IsCloseKind && string.IsNullOrEmpty(SelectedLot))
        {
            ValidationMessage = "充当lotを選択してください（自動推測はしません）。";
            return;
        }

        ValidationMessage = null;
        RaisePropertyChanged(nameof(ConfirmationSummary));
        IsConfirming = true;
    }

    private void Register()
    {
        if (!decimal.TryParse(PriceText, out var price) || !long.TryParse(QuantityText, out var quantity)
            || ExecutedAtDate is null || !TimeSpan.TryParse(ExecutedAtTime, out var time))
        {
            ValidationMessage = "入力内容を確認してください。";
            IsConfirming = false;
            return;
        }

        var executedAtUtc = new DateTimeOffset(ExecutedAtDate.Value.Date.Add(time), TimeSpan.Zero);
        var revision = new MockExecutionRevisionSeed(
            RevisionNumber: 1,
            ChangeKind: ExecutionChangeKind.Initial,
            ExecutedAtUtc: executedAtUtc,
            Price: price,
            Quantity: quantity,
            UserConfirmedAtUtc: executedAtUtc,
            Note: null);

        var lotNote = IsCloseKind ? $"充当lot {SelectedLot}（利用者が明示選択。FIFO等による推測は行いません）" : null;

        var seed = new MockExecutionSeed(Code, Name, Side, Kind, new[] { revision }, lotNote);
        _state.Executions.Insert(0, new TradeExecutionRowViewModel(seed));

        RegistrationCompleted?.Invoke(this, EventArgs.Empty);
    }
}

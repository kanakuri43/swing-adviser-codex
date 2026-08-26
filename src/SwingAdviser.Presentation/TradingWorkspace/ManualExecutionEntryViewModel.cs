using System.Globalization;
using Prism.Commands;
using Prism.Mvvm;
using SwingAdviser.Application.TradingWorkspace;
using SwingAdviser.Domain.Common;

namespace SwingAdviser.Presentation.TradingWorkspace;

public sealed class ManualExecutionEntryViewModel : BindableBase
{
    private readonly TradingWorkspaceService _service;
    private readonly ManualExecutionDialogRequestEventArgs _request;
    private DateTime? _executedDate;
    private string _executedTime = string.Empty;
    private string _priceText = string.Empty;
    private string _quantityText = string.Empty;
    private string _broker = string.Empty;
    private string _externalReference = string.Empty;
    private string _memo = string.Empty;
    private string _correctionReason = string.Empty;
    private bool _isConfirming;
    private bool _isBusy;
    private string? _validationMessage;

    public ManualExecutionEntryViewModel(
        TradingWorkspaceService service,
        ManualExecutionDialogRequestEventArgs request)
    {
        _service = service;
        _request = request;
        LotAllocations = request.Lots.Select(x => new ManualLotAllocationEntryViewModel(x)).ToList();
        ConfirmCommand = new DelegateCommand(GoToConfirm, () => !IsBusy);
        BackToEditCommand = new DelegateCommand(() => IsConfirming = false, () => !IsBusy);
        SaveCommand = new DelegateCommand(async () => await SaveAsync(), () => !IsBusy);

        if (request.CorrectionTarget is { } correction)
        {
            var current = correction.CurrentRevision;
            ExecutedDate = current.ExecutedAtUtc.ToLocalTime().Date;
            ExecutedTime = current.ExecutedAtUtc.ToLocalTime().ToString("HH:mm");
            PriceText = current.Price.ToString(CultureInfo.CurrentCulture);
            QuantityText = current.Quantity.ToString(CultureInfo.CurrentCulture);
            Broker = current.Broker ?? string.Empty;
            ExternalReference = current.ExternalReference ?? string.Empty;
            Memo = current.UserNote ?? string.Empty;
        }
    }

    public event EventHandler? Saved;

    public string Title => IsCorrection ? "手動約定の訂正" : "手動約定登録";
    public string InstrumentHeader => $"{_request.Code}　{_request.Name}　{TradingDisplayLabels.PositionSideLabel(_request.Side)}";
    public string KindText => TradingDisplayLabels.ExecutionKindLabel(_request.Kind);
    public string EntryBoundaryNotice => IsCorrection
        ? "保存済み約定を上書きせず、訂正revisionを追記します。訂正理由を必ず入力してください。"
        : "銘柄と方向だけを一覧から引き継ぎました。約定日時・価格・株数は証券会社の約定通知を確認して入力してください。現在値やサイン日時は自動採用しません。";
    public bool IsCorrection => _request.CorrectionTarget is not null;
    public bool IsClose => _request.Kind == ExecutionKind.Close && !IsCorrection;
    public IReadOnlyList<ManualLotAllocationEntryViewModel> LotAllocations { get; }

    public DateTime? ExecutedDate
    {
        get => _executedDate;
        set => SetProperty(ref _executedDate, value);
    }

    public string ExecutedTime
    {
        get => _executedTime;
        set => SetProperty(ref _executedTime, value);
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

    public string Broker
    {
        get => _broker;
        set => SetProperty(ref _broker, value);
    }

    public string ExternalReference
    {
        get => _externalReference;
        set => SetProperty(ref _externalReference, value);
    }

    public string Memo
    {
        get => _memo;
        set => SetProperty(ref _memo, value);
    }

    public string CorrectionReason
    {
        get => _correctionReason;
        set => SetProperty(ref _correctionReason, value);
    }

    public bool IsConfirming
    {
        get => _isConfirming;
        private set
        {
            if (SetProperty(ref _isConfirming, value))
            {
                RaisePropertyChanged(nameof(IsEditing));
            }
        }
    }

    public bool IsEditing => !IsConfirming;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ConfirmCommand.RaiseCanExecuteChanged();
                BackToEditCommand.RaiseCanExecuteChanged();
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (SetProperty(ref _validationMessage, value))
            {
                RaisePropertyChanged(nameof(HasValidationMessage));
            }
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);
    public string ConfirmationSummary =>
        $"{InstrumentHeader}\n種別: {KindText}\n約定日時: {ExecutedDate:yyyy-MM-dd} {ExecutedTime}\n" +
        $"約定価格: {PriceText}\n株数: {QuantityText}\n" +
        (IsClose ? $"充当lot:\n{AllocationSummary}" : string.Empty) +
        (IsCorrection ? $"訂正理由: {CorrectionReason}\n" : string.Empty) +
        "この内容を利用者確認済みの約定として保存します。よろしいですか？";

    public DelegateCommand ConfirmCommand { get; }
    public DelegateCommand BackToEditCommand { get; }
    public DelegateCommand SaveCommand { get; }

    private void GoToConfirm()
    {
        if (!TryReadValues(out _, out _, out var quantity, out var error))
        {
            ValidationMessage = error;
            return;
        }

        if (IsClose && !TryReadAllocations(quantity, out _, out error))
        {
            ValidationMessage = error;
            return;
        }

        if (IsCorrection && string.IsNullOrWhiteSpace(CorrectionReason))
        {
            ValidationMessage = "訂正理由を入力してください。";
            return;
        }

        ValidationMessage = null;
        RaisePropertyChanged(nameof(ConfirmationSummary));
        IsConfirming = true;
    }

    private async Task SaveAsync()
    {
        if (!TryReadValues(out var executedAtUtc, out var price, out var quantity, out var error))
        {
            ValidationMessage = error;
            IsConfirming = false;
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            if (_request.CorrectionTarget is { } correction)
            {
                await _service.CorrectManualExecutionAsync(new CorrectManualExecutionRequest(
                    correction.ExecutionId,
                    correction.CurrentRevision.RevisionId,
                    executedAtUtc,
                    price,
                    quantity,
                    "JPY",
                    DateTimeOffset.UtcNow,
                    true,
                    CorrectionReason,
                    NullIfWhiteSpace(Broker),
                    NullIfWhiteSpace(ExternalReference),
                    NullIfWhiteSpace(Memo)));
            }
            else
            {
                IReadOnlyList<ManualLotAllocation> allocations = [];
                if (IsClose && !TryReadAllocations(quantity, out allocations, out var allocationError))
                {
                    ValidationMessage = allocationError;
                    IsConfirming = false;
                    return;
                }
                await _service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
                    _request.InstrumentId,
                    _request.PositionId,
                    _request.CandidateId,
                    _request.Side,
                    _request.Kind,
                    executedAtUtc,
                    price,
                    quantity,
                    "JPY",
                    DateTimeOffset.UtcNow,
                    true,
                    allocations,
                    NullIfWhiteSpace(Broker),
                    NullIfWhiteSpace(ExternalReference),
                    NullIfWhiteSpace(Memo)));
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            ValidationMessage = $"保存できませんでした: {exception.Message}";
            IsConfirming = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryReadValues(
        out DateTimeOffset executedAtUtc,
        out decimal price,
        out long quantity,
        out string? error)
    {
        executedAtUtc = default;
        price = default;
        quantity = default;
        if (ExecutedDate is null || !TimeSpan.TryParse(ExecutedTime, CultureInfo.CurrentCulture, out var time))
        {
            error = "約定日と時刻を入力してください（時刻例: 09:15）。";
            return false;
        }

        var local = DateTime.SpecifyKind(ExecutedDate.Value.Date.Add(time), DateTimeKind.Unspecified);
        if (TimeZoneInfo.Local.IsInvalidTime(local))
        {
            error = "存在しないローカル時刻です。約定時刻を確認してください。";
            return false;
        }

        executedAtUtc = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime();
        if (!decimal.TryParse(PriceText, NumberStyles.Number, CultureInfo.CurrentCulture, out price) || price <= 0)
        {
            error = "約定価格を正の数で入力してください。";
            return false;
        }

        if (!long.TryParse(QuantityText, NumberStyles.Integer, CultureInfo.CurrentCulture, out quantity) || quantity <= 0)
        {
            error = "株数を正の整数で入力してください。";
            return false;
        }

        error = null;
        return true;
    }

    private string AllocationSummary => string.Join(
        "\n",
        LotAllocations.Where(x => !string.IsNullOrWhiteSpace(x.QuantityText))
            .Select(x => $"  {x.DisplayLabel}: {x.QuantityText}株")) + "\n";

    private bool TryReadAllocations(
        long executionQuantity,
        out IReadOnlyList<ManualLotAllocation> allocations,
        out string? error)
    {
        var result = new List<ManualLotAllocation>();
        foreach (var entry in LotAllocations)
        {
            if (string.IsNullOrWhiteSpace(entry.QuantityText))
            {
                continue;
            }

            if (!long.TryParse(entry.QuantityText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var quantity) || quantity <= 0)
            {
                allocations = [];
                error = $"{entry.DisplayLabel} の充当株数を正の整数で入力してください。";
                return false;
            }

            if (quantity > entry.RemainingQuantity)
            {
                allocations = [];
                error = $"{entry.DisplayLabel} の残数量（{entry.RemainingQuantity:#,0.####}株）を超えています。";
                return false;
            }

            result.Add(new ManualLotAllocation(entry.MarginLotId, quantity));
        }

        if (result.Count == 0 || result.Sum(x => x.Quantity) != executionQuantity)
        {
            allocations = [];
            error = "充当lotごとの株数を入力し、合計を約定株数と一致させてください（自動配分はしません）。";
            return false;
        }

        allocations = result;
        error = null;
        return true;
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ManualLotAllocationEntryViewModel(MarginLotListItem lot) : BindableBase
{
    private string _quantityText = string.Empty;

    public Guid MarginLotId => lot.MarginLotId;
    public string DisplayLabel => lot.DisplayLabel;
    public decimal RemainingQuantity => lot.RemainingQuantity;

    public string QuantityText
    {
        get => _quantityText;
        set => SetProperty(ref _quantityText, value);
    }
}

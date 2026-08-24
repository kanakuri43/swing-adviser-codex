namespace SwingAdviser.Domain.Common;

public enum PositionSide { Long, Short }
public enum SignalPurpose { Entry, Exit }
public enum ExitDecision { Hold, TakeProfit, StopLoss, Exit }
public enum ConfidenceLevel { High, Medium, Low }
public enum PointInTimeStatus { Verified, Unverified }
public enum RecordDisposition { Effective, Voided }
public enum SecurityType { DomesticCommonStock, ETF, ETN, REIT, Preferred, Foreign, Other, Unknown }
public enum ListingStatus { Listed, DelistingScheduled, Delisted, Unknown }
public enum ScanEligibility { Eligible, Excluded, Unknown }
public enum BarStatus { Provisional, Confirmed, Corrected, Invalid }
public enum CorporateActionType { Split, Consolidation, CashDividend, Unsupported }
public enum CorporateActionStatus { Announced, Confirmed, Corrected, Cancelled }
public enum EligibilityStatus { Eligible, Ineligible, Restricted, Unknown }
public enum OpenPermissionStatus { Allowed, Prohibited, Restricted, Unknown }
public enum AmountStatus { KnownAmount, KnownZero, NotOccurred, Unpublished, FetchFailed, Unknown, NotApplicable }
public enum AnalysisRunMode { Daily, Manual, Backtest }
public enum AnalysisRunStatus { Queued, Running, Succeeded, PartiallySucceeded, Failed, Cancelled }
public enum HistoryStatus { Complete, InsufficientHistory, HistoryIncomplete, Invalid }
public enum TechnicalAnalysisOutcome { Candidate, NotCandidate, InsufficientHistory, HistoryIncomplete, InvalidData, PointInTimeUnverified, ReconciliationRequired, Failed }
public enum PositionStatus { Open, Closed, Archived }
public enum ReconciliationStatus { Clear, Required, InProgress, Resolved }
public enum ExecutionKind { Open, Close }
public enum ExecutionOrigin { UserConfirmed }
public enum ExecutionChangeKind { Initial, Correction, Void }
public enum MarginType { Standardized, General, Unknown }
public enum MarginTermType { FixedDate, NoFixedTerm, Unknown }
public enum ContractChangeKind { Initial, ContractAmendment, InputCorrection }
public enum MarginCostType { BuyerInterest, StockLendingFee, Backwardation, DividendEquivalent, BrokerSpecific, Other }
public enum CostValuationKind { Estimate, Confirmed }
public enum CostDirection { Charge, Credit }
public enum CostSourceKind { ApplicationEstimate, PublishedMarketData, BrokerStatement, UserEntry }
public enum PartialExitStatus { NotApplicable, Candidate, NotFeasible }
public enum RiskPlanReason { Initial, PartialExitBreakeven, CorporateActionConversion, UserCorrection }
public enum PositionAdjustmentStatus { Applied, ReconciliationRequired, Resolved, Reversed }
public enum AiRequestOrigin { User, Automatic }
public enum AiAttemptKind { Initial, Retry, Recheck }
public enum AiAttemptStatus { Queued, Running, Succeeded, Failed, TimedOut, InsufficientInformation, Cancelled }
public enum AiVerdict { Bullish, Neutral, Bearish }

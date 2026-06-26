namespace Kantonal.Domain;

/// <summary>
/// The nine HRM2 financial key figures for a municipality-year, as published by the
/// Kanton Thurgau open-data portal (dataset sk-stat-4). A value object — value equality,
/// no identity. Each property documents its exact source field; all are nullable because
/// the source may omit any of them.
/// </summary>
public sealed record FinanceIndicators(
    /// <summary>Source: <c>selbstfinanzierungsgrad_in</c>.</summary>
    decimal? SelfFinancingRatio,
    /// <summary>Source: <c>selbstfinanzierungsanteil_in</c>.</summary>
    decimal? SelfFinancingShare,
    /// <summary>Source: <c>zinsbelastungsanteil_in</c>.</summary>
    decimal? InterestBurdenShare,
    /// <summary>Source: <c>kapitaldienstanteil_in</c>.</summary>
    decimal? CapitalServiceShare,
    /// <summary>Source: <c>investitionsanteil_in</c>.</summary>
    decimal? InvestmentShare,
    /// <summary>Source: <c>bruttoverschuldungsanteil_in</c>.</summary>
    decimal? GrossDebtShare,
    /// <summary>Source: <c>nettoschuld_nettovermogen_pro_einwohner_in_chf</c>.</summary>
    decimal? NetDebtPerCapitaChf,
    /// <summary>Source: <c>nettoverschuldungsquotient_in</c>.</summary>
    decimal? NetDebtQuotient,
    /// <summary>Source: <c>bilanzuberschussquotient_in</c>.</summary>
    decimal? BalanceSheetSurplusQuotient);

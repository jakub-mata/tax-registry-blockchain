using System;

namespace TaxChain.core;

public struct Transaction
{
    public TaxType Type { get; set; }
    public string TaxpayerId { get; set; }          // Tax ID or SSN
    public decimal Amount { get; set; }             // Tax amount
    public decimal? TaxableBase { get; set; }        // Income/amount subject to tax
    public decimal? TaxRate { get; set; }            // Applicable tax rate
    public DateTime TaxPeriodStart { get; set; }    // Period covered
    public DateTime TaxPeriodEnd { get; set; }
    public DateTime DueDate { get; set; }           // Payment deadline
    public DateTime PaymentDate { get; set; }      // When paid
    public string Jurisdiction { get; set; }        // Tax authority
    public TaxStatus Status { get; set; }           // Filed/Paid/Delinquent
    public string? Notes { get; set; }               // Additional information

    public Transaction(
        string taxpayerId,
        decimal amount, 
        decimal taxableBase,
        decimal taxRate,
        DateTime taxPeriodStart,
        DateTime taxPeriodEnd,
        DateTime dueDate,
        string jurisdiction,
        TaxStatus status,
        string notes
    )
    {
        TaxpayerId = taxpayerId;
        Amount = amount;
        TaxableBase = taxableBase;
        TaxRate = taxRate;
        TaxPeriodStart = taxPeriodStart;
        TaxPeriodEnd = taxPeriodEnd;
        DueDate = dueDate;
        PaymentDate = DateTime.Now;
        Jurisdiction = jurisdiction;
        Status = status;
        Notes = notes;
    }
    public Transaction(
        string taxpayerId,
        decimal amount,
        decimal? taxableBase,
        decimal? taxRate,
        DateTime taxPeriodStart,
        DateTime taxPeriodEnd,
        DateTime dueDate,
        DateTime paymentDate,
        string jurisdiction,
        TaxStatus status,
        string? notes
    )
    {
        TaxpayerId = taxpayerId;
        Amount = amount;
        TaxableBase = taxableBase;
        TaxRate = taxRate;
        TaxPeriodStart = taxPeriodStart;
        TaxPeriodEnd = taxPeriodEnd;
        DueDate = dueDate;
        PaymentDate = paymentDate;
        Jurisdiction = jurisdiction;
        Status = status;
        Notes = notes;
    }
    public enum TaxType
    {
        // Income Taxes
        PersonalIncomeTax,           // Individual income tax
        CorporateIncomeTax,          // Business profit tax
        CapitalGainsTax,            // Tax on asset sales
        DividendTax,                // Tax on dividend income
        InterestIncomeTax,          // Tax on interest earnings

        // Property Taxes
        PropertyTax,                // Real estate tax
        VehicleTax,                 // Registration/maintenance tax
        LuxuryTax,                  // Tax on luxury goods

        // Wealth Taxes
        WealthTax,                  // Tax on net worth
        InheritanceTax,             // Tax on inherited assets
        GiftTax,                    // Tax on large gifts

        // Value Added & Sales Taxes
        VAT,                        // Value Added Tax
        SalesTax,                   // Retail sales tax
        ExciseTax,                  // Specific goods tax (fuel, tobacco, alcohol)
        ServiceTax,                 // Tax on services

        // Consumption Taxes
        SinTax,                     // Tax on tobacco, alcohol
        FuelTax,                    // Gasoline/diesel tax
        CarbonTax,                  // Environmental tax on carbon emissions
        SugarTax,                   // Tax on sugary drinks

        // Employment Related
        PayrollTax,                 // Employer portion of social security
        SocialSecurityTax,          // Social security contributions
        MedicareTax,                // Healthcare insurance tax
        UnemploymentTax,            // Unemployment insurance
        WorkersCompensation,        // Workplace injury insurance
    }
    public enum TaxStatus
    {
        Filed,                      // Tax return filed
        Paid,                       // Tax fully paid
        PartiallyPaid,              // Partial payment made
        Delinquent,                 // Past due
        UnderAudit,                 // Currently being audited
        Disputed,                   // Taxpayer dispute
        Refunded                    // Overpayment refunded
    }
}
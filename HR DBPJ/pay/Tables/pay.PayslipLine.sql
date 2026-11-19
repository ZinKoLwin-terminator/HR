-- Earning and deduction lines of a payslip (Payslip.earnings / Payslip.deductions), as snapshots.
CREATE TABLE [pay].[PayslipLine]
(
    [Id]        int            IDENTITY (1, 1) NOT NULL,
    [PayslipId] int            NOT NULL,
    [LineType]  varchar(30)    NOT NULL,
    [SortOrder] smallint       NOT NULL,
    -- Component code, or a calculated line such as OT (overtime) or UL (unpaid leave).
    [Code]      nvarchar(20)   NOT NULL,
    [Name]      nvarchar(60)   NOT NULL,
    [Amount]    decimal(18, 2) NOT NULL,
    -- How the amount was worked out, e.g. "10% of basic".
    [Basis]     nvarchar(100)  NOT NULL,
    CONSTRAINT [PK_PayslipLine] PRIMARY KEY CLUSTERED ([Id]),
    -- Also the FK index.
    CONSTRAINT [UQ_PayslipLine_PayslipId_LineType_SortOrder] UNIQUE ([PayslipId], [LineType], [SortOrder]),
    -- Owned by the payslip: removed with it.
    CONSTRAINT [FK_PayslipLine_Payslip] FOREIGN KEY ([PayslipId]) REFERENCES [pay].[Payslip] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_PayslipLine_LineType] CHECK ([LineType] IN ('Earning', 'Deduction')),
    CONSTRAINT [CK_PayslipLine_Amount] CHECK ([Amount] >= 0)
);

-- HR event log shown on the employee profile (Joined, Promotion, Transfer, ...). Rows are append-only.
CREATE TABLE [hr].[EmploymentHistory]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [EmployeeId]       int            NOT NULL,
    [Date]             date           NOT NULL,
    [Type]             varchar(30)    NOT NULL,
    [Title]            nvarchar(200)  NOT NULL,
    [Details]          nvarchar(1000) NOT NULL,
    -- Display name of the user at the time of recording (kept even if the user is later removed).
    [RecordedBy]       nvarchar(130)  NOT NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_EmploymentHistory_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_EmploymentHistory_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_EmploymentHistory] PRIMARY KEY CLUSTERED ([Id]),
    -- History entries are removed with their employee (EmployeeMockService.delete).
    CONSTRAINT [FK_EmploymentHistory_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employee] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_EmploymentHistory_Type] CHECK ([Type] IN ('Joined', 'Confirmation', 'Promotion', 'Transfer', 'Salary Revision', 'Contract Change', 'Status Change', 'Exit'))
);
GO

CREATE NONCLUSTERED INDEX [IX_EmploymentHistory_EmployeeId_Date] ON [hr].[EmploymentHistory] ([EmployeeId], [Date]);

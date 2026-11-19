-- Document metadata only; file contents belong in file storage (the demo stores the file name).
CREATE TABLE [hr].[EmployeeDocument]
(
    [Id]               int            IDENTITY (1, 1) NOT NULL,
    [EmployeeId]       int            NOT NULL,
    [Type]             varchar(30)    NOT NULL,
    [Title]            nvarchar(80)   NOT NULL,
    [FileName]         nvarchar(260)  NOT NULL,
    [UploadedOn]       date           NOT NULL,
    [ExpiryDate]       date           NULL,
    [CreatedAtUtc]     datetime2(3)   NOT NULL CONSTRAINT [DF_EmployeeDocument_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]  int            NULL,
    [ModifiedAtUtc]    datetime2(3)   NOT NULL CONSTRAINT [DF_EmployeeDocument_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId] int            NULL,
    [RowVersion]       rowversion     NOT NULL,
    CONSTRAINT [PK_EmployeeDocument] PRIMARY KEY CLUSTERED ([Id]),
    -- Documents are removed with their employee (EmployeeMockService.delete).
    CONSTRAINT [FK_EmployeeDocument_Employee] FOREIGN KEY ([EmployeeId]) REFERENCES [hr].[Employee] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_EmployeeDocument_Type] CHECK ([Type] IN ('National ID', 'Passport', 'Resume', 'Offer Letter', 'Employment Contract', 'Certificate', 'Work Permit', 'Medical Report', 'Other'))
);
GO

CREATE NONCLUSTERED INDEX [IX_EmployeeDocument_EmployeeId] ON [hr].[EmployeeDocument] ([EmployeeId]);
GO

-- Supports "documents expiring soon" lookups (work permits, passports).
CREATE NONCLUSTERED INDEX [IX_EmployeeDocument_ExpiryDate]
    ON [hr].[EmployeeDocument] ([ExpiryDate])
    INCLUDE ([EmployeeId], [Type])
    WHERE [ExpiryDate] IS NOT NULL;

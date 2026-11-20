CREATE TABLE [hr].[Employee]
(
    [Id]                           int            IDENTITY (1, 1) NOT NULL,
    [EmployeeCode]                 nvarchar(12)   NOT NULL,
    [FirstName]                    nvarchar(60)   NOT NULL,
    [LastName]                     nvarchar(60)   NOT NULL,
    [Gender]                       varchar(30)    NOT NULL,
    [DateOfBirth]                  date           NOT NULL,
    [MaritalStatus]                varchar(30)    NOT NULL,
    [Nationality]                  nvarchar(60)   NOT NULL,
    [NationalId]                   nvarchar(40)   NOT NULL,
    [WorkEmail]                    nvarchar(120)  NOT NULL,
    [PersonalEmail]                nvarchar(120)  NULL,
    [Phone]                        nvarchar(25)   NOT NULL,
    [AlternatePhone]               nvarchar(25)   NULL,
    [AddressLine1]                 nvarchar(120)  NOT NULL,
    [AddressLine2]                 nvarchar(120)  NULL,
    [AddressCity]                  nvarchar(60)   NOT NULL,
    [AddressState]                 nvarchar(60)   NULL,
    [AddressPostalCode]            nvarchar(12)   NULL,
    [AddressCountry]               nvarchar(60)   NOT NULL,
    [EmergencyContactName]         nvarchar(80)   NOT NULL,
    [EmergencyContactRelationship] varchar(30)    NOT NULL,
    [EmergencyContactPhone]        nvarchar(25)   NOT NULL,
    [CompanyId]                    int            NOT NULL,
    [BranchId]                     int            NOT NULL,
    [DepartmentId]                 int            NOT NULL,
    [SectionId]                    int            NULL,
    [PositionId]                   int            NOT NULL,
    [JobGradeId]                   int            NOT NULL,
    [ManagerEmployeeId]            int            NULL,
    [ShiftId]                      int            NULL,
    [EmploymentType]               varchar(30)    NOT NULL,
    [EmploymentStatus]             varchar(30)    NOT NULL,
    [JoiningDate]                  date           NOT NULL,
    [ProbationEndDate]             date           NULL,
    [ExitDate]                     date           NULL,
    -- Path or URI of the photo in file storage; image bytes are not stored in this table.
    [PhotoUrl]                     nvarchar(400)  NULL,
    [CreatedAtUtc]                 datetime2(3)   NOT NULL CONSTRAINT [DF_Employee_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]              int            NULL,
    [ModifiedAtUtc]                datetime2(3)   NOT NULL CONSTRAINT [DF_Employee_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [ModifiedByUserId]             int            NULL,
    [RowVersion]                   rowversion     NOT NULL,
    [ValidFromUtc]                 datetime2(7)   GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    [ValidToUtc]                   datetime2(7)   GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME ([ValidFromUtc], [ValidToUtc]),
    CONSTRAINT [PK_Employee] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Employee_EmployeeCode] UNIQUE ([EmployeeCode]),
    CONSTRAINT [UQ_Employee_WorkEmail] UNIQUE ([WorkEmail]),
    -- Composite FKs enforce company > branch > department > section/position consistency.
    CONSTRAINT [FK_Employee_Branch] FOREIGN KEY ([BranchId], [CompanyId]) REFERENCES [org].[Branch] ([Id], [CompanyId]),
    CONSTRAINT [FK_Employee_Department] FOREIGN KEY ([DepartmentId], [BranchId]) REFERENCES [org].[Department] ([Id], [BranchId]),
    CONSTRAINT [FK_Employee_Section] FOREIGN KEY ([SectionId], [DepartmentId]) REFERENCES [org].[Section] ([Id], [DepartmentId]),
    CONSTRAINT [FK_Employee_Position] FOREIGN KEY ([PositionId], [DepartmentId]) REFERENCES [org].[Position] ([Id], [DepartmentId]),
    CONSTRAINT [FK_Employee_JobGrade] FOREIGN KEY ([JobGradeId]) REFERENCES [org].[JobGrade] ([Id]),
    CONSTRAINT [FK_Employee_Employee_ManagerEmployeeId] FOREIGN KEY ([ManagerEmployeeId]) REFERENCES [hr].[Employee] ([Id]),
    CONSTRAINT [FK_Employee_Shift] FOREIGN KEY ([ShiftId]) REFERENCES [att].[Shift] ([Id]),
    CONSTRAINT [CK_Employee_EmployeeCode] CHECK ([EmployeeCode] LIKE N'[A-Z0-9]%' COLLATE Latin1_General_BIN2 AND [EmployeeCode] NOT LIKE N'%[^A-Z0-9-]%' COLLATE Latin1_General_BIN2),
    CONSTRAINT [CK_Employee_Gender] CHECK ([Gender] IN ('Male', 'Female', 'Other')),
    CONSTRAINT [CK_Employee_MaritalStatus] CHECK ([MaritalStatus] IN ('Single', 'Married', 'Divorced', 'Widowed')),
    CONSTRAINT [CK_Employee_EmergencyContactRelationship] CHECK ([EmergencyContactRelationship] IN ('Spouse', 'Father', 'Mother', 'Sibling', 'Child', 'Friend', 'Other')),
    CONSTRAINT [CK_Employee_EmploymentType] CHECK ([EmploymentType] IN ('Full-time', 'Part-time', 'Contract', 'Intern')),
    CONSTRAINT [CK_Employee_EmploymentStatus] CHECK ([EmploymentStatus] IN ('Active', 'Probation', 'On Leave', 'Suspended', 'Resigned', 'Terminated')),
    CONSTRAINT [CK_Employee_ProbationEndDate] CHECK ([ProbationEndDate] >= [JoiningDate]),
    CONSTRAINT [CK_Employee_ExitDate] CHECK ([ExitDate] >= [JoiningDate]),
    CONSTRAINT [CK_Employee_ExitDateRequired] CHECK ([EmploymentStatus] NOT IN ('Resigned', 'Terminated') OR [ExitDate] IS NOT NULL),
    CONSTRAINT [CK_Employee_ManagerNotSelf] CHECK ([ManagerEmployeeId] <> [Id])
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [hr].[EmployeeHistory], DATA_CONSISTENCY_CHECK = ON));
GO

CREATE NONCLUSTERED INDEX [IX_Employee_BranchId_CompanyId] ON [hr].[Employee] ([BranchId], [CompanyId]);
GO

CREATE NONCLUSTERED INDEX [IX_Employee_DepartmentId_BranchId] ON [hr].[Employee] ([DepartmentId], [BranchId]);
GO

CREATE NONCLUSTERED INDEX [IX_Employee_SectionId_DepartmentId] ON [hr].[Employee] ([SectionId], [DepartmentId]);
GO

CREATE NONCLUSTERED INDEX [IX_Employee_PositionId_DepartmentId] ON [hr].[Employee] ([PositionId], [DepartmentId]);
GO

CREATE NONCLUSTERED INDEX [IX_Employee_JobGradeId] ON [hr].[Employee] ([JobGradeId]);
GO

CREATE NONCLUSTERED INDEX [IX_Employee_ManagerEmployeeId] ON [hr].[Employee] ([ManagerEmployeeId]);
GO

CREATE NONCLUSTERED INDEX [IX_Employee_ShiftId] ON [hr].[Employee] ([ShiftId]);
GO

-- Employee list filters: status and joining-date range.
CREATE NONCLUSTERED INDEX [IX_Employee_EmploymentStatus] ON [hr].[Employee] ([EmploymentStatus]);
GO

CREATE NONCLUSTERED INDEX [IX_Employee_JoiningDate] ON [hr].[Employee] ([JoiningDate]);

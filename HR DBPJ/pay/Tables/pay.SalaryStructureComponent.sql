-- Components of a structure (SalaryStructure.componentIds), in display order.
CREATE TABLE [pay].[SalaryStructureComponent]
(
    [SalaryStructureId] int      NOT NULL,
    [SalaryComponentId] int      NOT NULL,
    [SortOrder]         smallint NOT NULL,
    CONSTRAINT [PK_SalaryStructureComponent] PRIMARY KEY CLUSTERED ([SalaryStructureId], [SalaryComponentId]),
    CONSTRAINT [UQ_SalaryStructureComponent_SortOrder] UNIQUE ([SalaryStructureId], [SortOrder]),
    -- Owned by the structure: removed with it.
    CONSTRAINT [FK_SalaryStructureComponent_SalaryStructure] FOREIGN KEY ([SalaryStructureId]) REFERENCES [pay].[SalaryStructure] ([Id]) ON DELETE CASCADE,
    -- A component used by a structure cannot be deleted (SalaryComponentMockService.beforeDelete).
    CONSTRAINT [FK_SalaryStructureComponent_SalaryComponent] FOREIGN KEY ([SalaryComponentId]) REFERENCES [pay].[SalaryComponent] ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_SalaryStructureComponent_SalaryComponentId] ON [pay].[SalaryStructureComponent] ([SalaryComponentId]);

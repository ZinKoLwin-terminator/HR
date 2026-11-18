using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>Checks the grievance part of the EF model against the grv tables of the SSDT project. No connection is opened.</summary>
public sealed class GrievanceSchemaMappingTests : IDisposable
{
    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private IEntityType Entity<T>() => context.Model.FindEntityType(typeof(T))!;

    private static string Type(IEntityType entity, string property) => entity.FindProperty(property)!.GetColumnType();

    [Theory]
    [InlineData(typeof(GrievanceCategory), "Category")]
    [InlineData(typeof(GrievanceCase), "Case")]
    [InlineData(typeof(GrievanceAssignment), "Assignment")]
    [InlineData(typeof(GrievanceStatusHistoryEntry), "StatusHistory")]
    [InlineData(typeof(GrievanceInvestigationNote), "InvestigationNote")]
    [InlineData(typeof(GrievanceCommunication), "Communication")]
    [InlineData(typeof(GrievanceAttachment), "Attachment")]
    [InlineData(typeof(GrievanceCorrectiveAction), "CorrectiveAction")]
    [InlineData(typeof(GrievanceResolutionProposal), "ResolutionProposal")]
    [InlineData(typeof(GrievanceEscalation), "Escalation")]
    [InlineData(typeof(GrievanceAuditEntry), "AuditLog")]
    public void Tables_with_an_identity_map_to_the_grv_schema_and_name(Type entity, string table)
    {
        var type = context.Model.FindEntityType(entity)!;

        Assert.Equal("grv", type.GetSchema());
        Assert.Equal(table, type.GetTableName());
        Assert.Equal($"PK_{table}", type.FindPrimaryKey()!.GetName());
        Assert.Equal(ValueGenerated.OnAdd, type.FindProperty("Id")!.ValueGenerated);
    }

    [Fact]
    public void The_audit_log_has_a_bigint_key()
    {
        Assert.Equal("bigint", Type(Entity<GrievanceAuditEntry>(), "Id"));
    }

    [Fact]
    public void The_submitter_row_is_keyed_by_its_case_and_cascades_with_a_deleted_draft()
    {
        var submitter = Entity<GrievanceCaseSubmitter>();

        Assert.Equal("grv", submitter.GetSchema());
        Assert.Equal("CaseSubmitter", submitter.GetTableName());
        Assert.Equal(["CaseId"], submitter.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("PK_CaseSubmitter", submitter.FindPrimaryKey()!.GetName());
        Assert.Equal(ValueGenerated.Never, submitter.FindProperty("CaseId")!.ValueGenerated);
        Assert.Equal("binary(32)", Type(submitter, "SubmitterPseudonym"));
        Assert.Equal("datetime2(3)", Type(submitter, "SubmitterSeenAtUtc"));
        Assert.Null(submitter.FindProperty("IsAnonymous"));
        var toCase = submitter.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(GrievanceCase));
        Assert.Equal("FK_CaseSubmitter_Case", toCase.GetConstraintName());
        Assert.Equal(DeleteBehavior.Cascade, toCase.DeleteBehavior);
    }

    [Fact]
    public void The_my_cases_indexes_are_filtered_so_each_lookup_is_a_seek()
    {
        var indexes = Entity<GrievanceCaseSubmitter>().GetIndexes().ToDictionary(index => index.GetDatabaseName()!);

        Assert.Equal("[EmployeeId] IS NOT NULL", indexes["IX_CaseSubmitter_EmployeeId"].GetFilter());
        Assert.Equal("[SubmitterPseudonym] IS NOT NULL", indexes["IX_CaseSubmitter_SubmitterPseudonym"].GetFilter());
    }

    [Fact]
    public void Submitter_written_tables_have_times_and_a_row_version_but_no_user_columns()
    {
        foreach (var type in new[] { Entity<GrievanceCase>(), Entity<GrievanceAttachment>() })
        {
            var rowVersion = type.FindProperty("RowVersion")!;
            Assert.True(rowVersion.IsConcurrencyToken, type.Name);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
            Assert.Equal("datetime2(3)", Type(type, "CreatedAtUtc"));
            Assert.Equal("datetime2(3)", Type(type, "ModifiedAtUtc"));
            Assert.Null(type.FindProperty("CreatedByUserId"));
            Assert.Null(type.FindProperty("ModifiedByUserId"));
        }

        foreach (var type in new[] { Entity<GrievanceCategory>(), Entity<GrievanceCorrectiveAction>() })
        {
            Assert.True(type.FindProperty("RowVersion")!.IsConcurrencyToken, type.Name);
            Assert.NotNull(type.FindProperty("CreatedByUserId"));
        }

        foreach (var type in new[]
        {
            Entity<GrievanceStatusHistoryEntry>(), Entity<GrievanceInvestigationNote>(), Entity<GrievanceCommunication>(),
            Entity<GrievanceAssignment>(), Entity<GrievanceResolutionProposal>(), Entity<GrievanceEscalation>(), Entity<GrievanceAuditEntry>(),
        })
        {
            Assert.Null(type.FindProperty("RowVersion"));
        }
    }

    [Fact]
    public void The_case_is_not_temporal_and_has_no_period_columns()
    {
        var grievance = Entity<GrievanceCase>();
        var names = grievance.GetProperties().Select(property => property.Name).ToList();

        Assert.DoesNotContain("ValidFromUtc", names);
        Assert.DoesNotContain("ValidToUtc", names);
        Assert.False(grievance.IsTemporal());
        Assert.DoesNotContain(context.Model.GetEntityTypes(), type => type.GetSchema() == "grv" && type.GetTableName()!.EndsWith("History", StringComparison.Ordinal) && type.ClrType != typeof(GrievanceStatusHistoryEntry));
    }

    [Fact]
    public void Case_columns_use_the_ssdt_types_and_lengths()
    {
        var grievance = Entity<GrievanceCase>();

        Assert.Equal("varchar(20)", Type(grievance, "CaseNo"));
        Assert.Equal(200, grievance.FindProperty("Subject")!.GetMaxLength());
        Assert.Equal(4000, grievance.FindProperty("Description")!.GetMaxLength());
        Assert.Equal(200, grievance.FindProperty("Location")!.GetMaxLength());
        Assert.Equal(200, grievance.FindProperty("InvolvedPersonText")!.GetMaxLength());
        Assert.Equal("varchar(30)", Type(grievance, "Status"));
        Assert.Equal("varchar(10)", Type(grievance, "Priority"));
        Assert.Equal("varchar(30)", Type(grievance, "ReturnStatus"));
        Assert.Equal(4000, grievance.FindProperty("ResolutionSummary")!.GetMaxLength());
        Assert.Equal(1000, grievance.FindProperty("RejectionReason")!.GetMaxLength());
        foreach (var date in new[] { "IncidentDate", "TargetResolutionDate", "RetentionUntil" })
        {
            Assert.Equal("date", Type(grievance, date));
        }

        foreach (var time in new[] { "DeclarationAcceptedAtUtc", "SubmittedAtUtc", "AcknowledgedAtUtc", "ResolvedAtUtc", "ClosedAtUtc", "ArchivedAtUtc", "LastSubmitterUpdateAtUtc" })
        {
            Assert.Equal("datetime2(3)", Type(grievance, time));
        }

        Assert.Equal("bit", Type(grievance, "IsAnonymous"));
        Assert.Equal("bit", Type(grievance, "LegalHold"));
        Assert.Null(grievance.FindProperty("IsArchived"));
    }

    [Fact]
    public void Other_columns_use_the_ssdt_types_and_lengths()
    {
        var category = Entity<GrievanceCategory>();
        Assert.Equal(20, category.FindProperty("Code")!.GetMaxLength());
        Assert.Equal(100, category.FindProperty("Name")!.GetMaxLength());
        Assert.Equal(300, category.FindProperty("Description")!.GetMaxLength());
        Assert.Equal("smallint", Type(category, "DefaultTargetDays"));
        Assert.Equal("smallint", Type(category, "SortOrder"));
        Assert.Equal("varchar(30)", Type(category, "Status"));

        var history = Entity<GrievanceStatusHistoryEntry>();
        Assert.Equal("varchar(30)", Type(history, "FromStatus"));
        Assert.False(history.FindProperty("FromStatus")!.IsNullable);
        Assert.Equal("varchar(30)", Type(history, "Action"));
        Assert.Equal("varchar(20)", Type(history, "ActorRole"));
        Assert.Equal(130, history.FindProperty("ActorName")!.GetMaxLength());
        Assert.Equal(1000, history.FindProperty("Reason")!.GetMaxLength());

        var message = Entity<GrievanceCommunication>();
        Assert.Equal("varchar(20)", Type(message, "Direction"));
        Assert.Equal("varchar(20)", Type(message, "Kind"));
        Assert.Equal(4000, message.FindProperty("Body")!.GetMaxLength());

        var attachment = Entity<GrievanceAttachment>();
        Assert.Equal("varchar(20)", Type(attachment, "Visibility"));
        Assert.Equal("varchar(100)", Type(attachment, "ContentType"));
        Assert.Equal("char(32)", Type(attachment, "StorageKey"));
        Assert.Equal("binary(32)", Type(attachment, "Sha256"));
        Assert.Equal("bigint", Type(attachment, "SizeBytes"));

        Assert.Equal("varchar(10)", Type(Entity<GrievanceAssignment>(), "Role"));
        Assert.Equal("varchar(20)", Type(Entity<GrievanceInvestigationNote>(), "Kind"));
        Assert.Equal("varchar(20)", Type(Entity<GrievanceResolutionProposal>(), "Decision"));
        Assert.Equal("varchar(20)", Type(Entity<GrievanceCorrectiveAction>(), "Status"));
        Assert.Equal("varchar(40)", Type(Entity<GrievanceAuditEntry>(), "Action"));
        Assert.Equal(500, Entity<GrievanceAuditEntry>().FindProperty("Detail")!.GetMaxLength());
    }

    [Fact]
    public void Unique_indexes_and_foreign_keys_match_ssdt_names()
    {
        static IEnumerable<string> Unique(IEntityType entity) =>
            entity.GetIndexes().Where(index => index.IsUnique).Select(index => index.GetDatabaseName()!).Order(StringComparer.Ordinal);

        Assert.Equal(["UQ_Category_Code", "UQ_Category_Name"], Unique(Entity<GrievanceCategory>()));
        Assert.Equal(["UQ_Case_CaseNo"], Unique(Entity<GrievanceCase>()));
        Assert.Equal(["UQ_Assignment_CaseId_ActiveLead", "UQ_Assignment_CaseId_InvestigatorEmployeeId_Active"], Unique(Entity<GrievanceAssignment>()));
        Assert.Equal(["UQ_Attachment_StorageKey"], Unique(Entity<GrievanceAttachment>()));
        Assert.Equal(["UQ_ResolutionProposal_CaseId_Pending"], Unique(Entity<GrievanceResolutionProposal>()));
        Assert.Equal(["UQ_Escalation_CaseId_Open"], Unique(Entity<GrievanceEscalation>()));
        Assert.Equal("[CaseNo] IS NOT NULL", Entity<GrievanceCase>().GetIndexes().Single(index => index.IsUnique).GetFilter());

        static IEnumerable<string> Keys(IEntityType entity) =>
            entity.GetForeignKeys().Select(key => key.GetConstraintName()!).Order(StringComparer.Ordinal);

        Assert.Equal(["FK_Case_Category", "FK_Case_Department_InvolvedDepartmentId", "FK_Case_Employee_SubjectEmployeeId"], Keys(Entity<GrievanceCase>()));
        Assert.Equal(["FK_Assignment_Case", "FK_Assignment_Employee_InvestigatorEmployeeId"], Keys(Entity<GrievanceAssignment>()));
        Assert.Equal(["FK_StatusHistory_Case", "FK_StatusHistory_Employee_ActorEmployeeId"], Keys(Entity<GrievanceStatusHistoryEntry>()));
        Assert.Equal(["FK_Communication_Case", "FK_Communication_Employee_AuthorEmployeeId"], Keys(Entity<GrievanceCommunication>()));
        Assert.Equal(["FK_Attachment_Case"], Keys(Entity<GrievanceAttachment>()));
        Assert.Equal(["FK_CorrectiveAction_Case"], Keys(Entity<GrievanceCorrectiveAction>()));
        Assert.Empty(Keys(Entity<GrievanceAuditEntry>()));
    }

    [Fact]
    public void Texts_are_stored_as_the_exact_values_the_database_checks()
    {
        var status = Entity<GrievanceCase>().FindProperty("Status")!.GetValueConverter()!;
        var priority = Entity<GrievanceCase>().FindProperty("Priority")!.GetValueConverter()!;
        var action = Entity<GrievanceStatusHistoryEntry>().FindProperty("Action")!.GetValueConverter()!;
        var visibility = Entity<GrievanceAttachment>().FindProperty("Visibility")!.GetValueConverter()!;
        var audit = Entity<GrievanceAuditEntry>().FindProperty("Action")!.GetValueConverter()!;

        Assert.Equal("AwaitingEmployeeResponse", status.ConvertToProvider(GrievanceStatus.AwaitingEmployeeResponse));
        Assert.Equal("Critical", priority.ConvertToProvider(GrievancePriority.Critical));
        Assert.Equal("RejectResolution", action.ConvertToProvider(GrievanceHistoryAction.RejectResolution));
        Assert.Equal("CaseTeam", visibility.ConvertToProvider(GrievanceAttachmentVisibility.CaseTeam));
        Assert.Equal("AccessRefused", audit.ConvertToProvider(GrievanceAuditAction.AccessRefused));
        Assert.Equal(GrievanceStatus.UnderReview, status.ConvertFromProvider("UnderReview"));
    }

    [Fact]
    public void The_closed_and_submitted_triggers_and_the_grievance_constraints_are_conflicts()
    {
        Assert.Equal(Domain.Common.ErrorType.Conflict, SqlConflicts.Translate(51101, "A closed grievance case is final.")!.Type);
        Assert.DoesNotContain("closed grievance case is final", SqlConflicts.Translate(51101, "A closed grievance case is final.")!.Message, StringComparison.Ordinal);
        Assert.Equal(
            Domain.Common.ErrorType.Conflict,
            SqlConflicts.Translate(2601, "Cannot insert duplicate key row in object 'grv.Escalation' with unique index 'UQ_Escalation_CaseId_Open'.")!.Type);
        Assert.Equal(
            Domain.Common.ErrorType.Conflict,
            SqlConflicts.Translate(2601, "Cannot insert duplicate key row in object 'grv.Assignment' with unique index 'UQ_Assignment_CaseId_ActiveLead'.")!.Type);
    }
}

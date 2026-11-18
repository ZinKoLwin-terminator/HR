using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apex.Hrms.Infrastructure.Persistence.Configurations;

// Maps the grv schema. CHECK constraints, defaults and the triggers TR_Case_ClosedIsFinal and TR_Case_SubmittedIsFixed stay in the
// SSDT project; the application always supplies the values and the database refuses anything inconsistent (SqlConflicts turns the
// refusal into a 400 or 409, the triggers' THROW 51101 (case) and 51102 (attachment) into a 409). grv.Case is deliberately NOT system-versioned (draft revisions
// and deleted drafts leave no trace), so it has no period columns. Composite foreign keys (the case's placement company > branch >
// department > position; a file's message or note on the same case; a reply on the same case) are not modeled, as for the
// organization and KPI tables: the database enforces them and the application only ever links rows of one case.
// The submitter-written tables (Case, Attachment) are ConfidentialEntity rows: times and a row version, no user audit columns.

internal static class GrievanceMappingExtensions
{
    /// <summary>The times and the rowversion concurrency token of a confidential table (no user columns).</summary>
    public static void ConfigureConfidential<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : ConfidentialEntity
    {
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.ModifiedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.RowVersion).IsRowVersion();
    }
}

/// <summary>Maps grv.Category.</summary>
internal sealed class GrievanceCategoryConfiguration : IEntityTypeConfiguration<GrievanceCategory>
{
    public void Configure(EntityTypeBuilder<GrievanceCategory> builder)
    {
        builder.ToTable("Category", "grv");
        builder.HasKey(category => category.Id).HasName("PK_Category");
        builder.Property(category => category.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(category => category.Code).HasMaxLength(GrievanceCategory.CodeMaxLength);
        builder.HasIndex(category => category.Code).IsUnique().HasDatabaseName("UQ_Category_Code");
        builder.Property(category => category.Name).HasMaxLength(GrievanceCategory.NameMaxLength);
        builder.HasIndex(category => category.Name).IsUnique().HasDatabaseName("UQ_Category_Name");
        builder.Property(category => category.Description).HasMaxLength(GrievanceCategory.DescriptionMaxLength);
        builder.Property(category => category.DefaultTargetDays).HasColumnType("smallint");
        builder.Property(category => category.SortOrder).HasColumnType("smallint");
        builder.Property(category => category.Status).HasConversion<string>().HasMaxLength(30).IsUnicode(false);
    }
}

/// <summary>Maps grv.Case (not temporal: see the note above).</summary>
internal sealed class GrievanceCaseConfiguration : IEntityTypeConfiguration<GrievanceCase>
{
    public void Configure(EntityTypeBuilder<GrievanceCase> builder)
    {
        // Both triggers are declared so EF does not use OUTPUT without INTO for this table (SQL Server refuses that with an enabled trigger).
        builder.ToTable("Case", "grv", table =>
        {
            table.HasTrigger("TR_Case_ClosedIsFinal");
            table.HasTrigger("TR_Case_SubmittedIsFixed");
        });
        builder.HasKey(grievance => grievance.Id).HasName("PK_Case");
        builder.Property(grievance => grievance.Id).UseIdentityColumn();
        builder.ConfigureConfidential();
        builder.Ignore(grievance => grievance.IsArchived);
        builder.Ignore(grievance => grievance.AwaitsSubmitterResponse);

        builder.Property(grievance => grievance.CaseNo).HasMaxLength(20).IsUnicode(false);
        builder.Property(grievance => grievance.Subject).HasMaxLength(GrievanceCase.SubjectMaxLength);
        builder.Property(grievance => grievance.Description).HasMaxLength(GrievanceCase.DescriptionMaxLength);
        builder.Property(grievance => grievance.IncidentDate).HasColumnType("date");
        builder.Property(grievance => grievance.Location).HasMaxLength(GrievanceCase.ShortTextMaxLength);
        builder.Property(grievance => grievance.InvolvedPersonText).HasMaxLength(GrievanceCase.ShortTextMaxLength);
        builder.Property(grievance => grievance.Status).HasTextConversion(GrievanceText.Status);
        builder.Property(grievance => grievance.Priority).HasTextConversion(GrievanceText.Priority, 10);
        builder.Property(grievance => grievance.TargetResolutionDate).HasColumnType("date");
        builder.Property(grievance => grievance.ReturnStatus).HasTextConversion(GrievanceText.Status);
        foreach (var time in new[]
        {
            nameof(GrievanceCase.DeclarationAcceptedAtUtc), nameof(GrievanceCase.SubmittedAtUtc), nameof(GrievanceCase.AcknowledgedAtUtc),
            nameof(GrievanceCase.ResolvedAtUtc), nameof(GrievanceCase.ClosedAtUtc), nameof(GrievanceCase.ArchivedAtUtc),
            nameof(GrievanceCase.LastSubmitterUpdateAtUtc),
        })
        {
            builder.Property(time).HasColumnType("datetime2(3)");
        }

        builder.Property(grievance => grievance.ResolutionSummary).HasMaxLength(GrievanceCase.ResolutionSummaryMaxLength);
        builder.Property(grievance => grievance.RejectionReason).HasMaxLength(GrievanceCase.ReasonMaxLength);
        builder.Property(grievance => grievance.RetentionUntil).HasColumnType("date");

        // Case numbers are unique once given (drafts have none).
        builder.HasIndex(grievance => grievance.CaseNo)
            .IsUnique()
            .HasFilter("[CaseNo] IS NOT NULL")
            .HasDatabaseName("UQ_Case_CaseNo");
        builder.HasOne<GrievanceCategory>().WithMany()
            .HasForeignKey(grievance => grievance.CategoryId)
            .HasConstraintName("FK_Case_Category")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Department>().WithMany()
            .HasForeignKey(grievance => grievance.InvolvedDepartmentId)
            .HasConstraintName("FK_Case_Department_InvolvedDepartmentId")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(grievance => grievance.SubjectEmployeeId)
            .HasConstraintName("FK_Case_Employee_SubjectEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(grievance => new { grievance.Status, grievance.Priority }).HasDatabaseName("IX_Case_Status_Priority");
        builder.HasIndex(grievance => new { grievance.CategoryId, grievance.SubmittedAtUtc }).HasDatabaseName("IX_Case_CategoryId_SubmittedAtUtc");
        builder.HasIndex(grievance => new { grievance.DepartmentId, grievance.SubmittedAtUtc }).HasDatabaseName("IX_Case_DepartmentId_SubmittedAtUtc");
        builder.HasIndex(grievance => grievance.TargetResolutionDate).HasDatabaseName("IX_Case_TargetResolutionDate_Open");
        builder.HasIndex(grievance => grievance.SubjectEmployeeId).HasDatabaseName("IX_Case_SubjectEmployeeId");
        builder.HasIndex(grievance => grievance.InvolvedDepartmentId).HasDatabaseName("IX_Case_InvolvedDepartmentId");
        builder.HasIndex(grievance => new { grievance.BranchId, grievance.CompanyId }).HasDatabaseName("IX_Case_BranchId_CompanyId");
        builder.HasIndex(grievance => new { grievance.PositionId, grievance.DepartmentId }).HasDatabaseName("IX_Case_PositionId_DepartmentId");
    }
}

/// <summary>
/// Maps grv.CaseSubmitter: keyed by the case (no identity), removed with a deleted draft (ON DELETE CASCADE in the database). The
/// pseudonym is exactly 32 bytes.
/// </summary>
internal sealed class GrievanceCaseSubmitterConfiguration : IEntityTypeConfiguration<GrievanceCaseSubmitter>
{
    public void Configure(EntityTypeBuilder<GrievanceCaseSubmitter> builder)
    {
        builder.ToTable("CaseSubmitter", "grv");
        builder.HasKey(submitter => submitter.CaseId).HasName("PK_CaseSubmitter");
        builder.Property(submitter => submitter.CaseId).ValueGeneratedNever();
        builder.Ignore(submitter => submitter.IsAnonymous);

        builder.Property(submitter => submitter.SubmitterPseudonym)
            .HasColumnType($"binary({GrievanceCaseSubmitter.PseudonymLength})")
            .HasMaxLength(GrievanceCaseSubmitter.PseudonymLength)
            .IsFixedLength();
        builder.Property(submitter => submitter.SubmitterSeenAtUtc).HasColumnType("datetime2(3)");

        builder.HasOne<GrievanceCase>().WithOne()
            .HasForeignKey<GrievanceCaseSubmitter>(submitter => submitter.CaseId)
            .HasConstraintName("FK_CaseSubmitter_Case")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(submitter => submitter.EmployeeId)
            .HasConstraintName("FK_CaseSubmitter_Employee")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(submitter => submitter.EmployeeId)
            .HasFilter("[EmployeeId] IS NOT NULL")
            .HasDatabaseName("IX_CaseSubmitter_EmployeeId");
        builder.HasIndex(submitter => submitter.SubmitterPseudonym)
            .HasFilter("[SubmitterPseudonym] IS NOT NULL")
            .HasDatabaseName("IX_CaseSubmitter_SubmitterPseudonym");
    }
}

/// <summary>Maps grv.Assignment. Unassigning ends a row; nothing deletes one.</summary>
internal sealed class GrievanceAssignmentConfiguration : IEntityTypeConfiguration<GrievanceAssignment>
{
    public void Configure(EntityTypeBuilder<GrievanceAssignment> builder)
    {
        builder.ToTable("Assignment", "grv");
        builder.HasKey(assignment => assignment.Id).HasName("PK_Assignment");
        builder.Property(assignment => assignment.Id).UseIdentityColumn();
        builder.Ignore(assignment => assignment.IsActive);

        builder.Property(assignment => assignment.Role).HasTextConversion(GrievanceText.AssignmentRole, 10);
        builder.Property(assignment => assignment.AssignedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(assignment => assignment.UnassignedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(assignment => assignment.Reason).HasMaxLength(GrievanceAssignment.ReasonMaxLength);

        builder.HasOne<GrievanceCase>().WithMany()
            .HasForeignKey(assignment => assignment.CaseId)
            .HasConstraintName("FK_Assignment_Case")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(assignment => assignment.InvestigatorEmployeeId)
            .HasConstraintName("FK_Assignment_Employee_InvestigatorEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        // One active Lead per case; an investigator is assigned once at a time.
        builder.HasIndex(assignment => assignment.CaseId, "UQ_Assignment_CaseId_ActiveLead")
            .IsUnique()
            .HasFilter("[Role] = 'Lead' AND [UnassignedAtUtc] IS NULL");
        builder.HasIndex(assignment => new { assignment.CaseId, assignment.InvestigatorEmployeeId }, "UQ_Assignment_CaseId_InvestigatorEmployeeId_Active")
            .IsUnique()
            .HasFilter("[UnassignedAtUtc] IS NULL");
        builder.HasIndex(assignment => new { assignment.InvestigatorEmployeeId, assignment.CaseId }, "IX_Assignment_InvestigatorEmployeeId_CaseId");
        builder.HasIndex(assignment => assignment.CaseId, "IX_Assignment_CaseId");
    }
}

/// <summary>Maps grv.StatusHistory: append-only (no UPDATE or DELETE for hrms_api), no row version.</summary>
internal sealed class GrievanceStatusHistoryConfiguration : IEntityTypeConfiguration<GrievanceStatusHistoryEntry>
{
    public void Configure(EntityTypeBuilder<GrievanceStatusHistoryEntry> builder)
    {
        builder.ToTable("StatusHistory", "grv");
        builder.HasKey(entry => entry.Id).HasName("PK_StatusHistory");
        builder.Property(entry => entry.Id).UseIdentityColumn();

        builder.Property(entry => entry.FromStatus).HasTextConversion(GrievanceText.Status);
        builder.Property(entry => entry.ToStatus).HasTextConversion(GrievanceText.Status);
        builder.Property(entry => entry.Action).HasTextConversion(GrievanceText.HistoryAction);
        builder.Property(entry => entry.ActorName).HasMaxLength(GrievanceStatusHistoryEntry.ActorNameMaxLength);
        builder.Property(entry => entry.ActorRole).HasTextConversion(GrievanceText.ActorRole, 20);
        builder.Property(entry => entry.Reason).HasMaxLength(GrievanceStatusHistoryEntry.ReasonMaxLength);
        builder.Property(entry => entry.OccurredAtUtc).HasColumnType("datetime2(3)");

        builder.HasOne<GrievanceCase>().WithMany()
            .HasForeignKey(entry => entry.CaseId)
            .HasConstraintName("FK_StatusHistory_Case")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(entry => entry.ActorEmployeeId)
            .HasConstraintName("FK_StatusHistory_Employee_ActorEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(entry => new { entry.CaseId, entry.OccurredAtUtc }).HasDatabaseName("IX_StatusHistory_CaseId_OccurredAtUtc");
        builder.HasIndex(entry => entry.ActorEmployeeId).HasDatabaseName("IX_StatusHistory_ActorEmployeeId");
    }
}

/// <summary>Maps grv.InvestigationNote: append-only, no row version.</summary>
internal sealed class GrievanceInvestigationNoteConfiguration : IEntityTypeConfiguration<GrievanceInvestigationNote>
{
    public void Configure(EntityTypeBuilder<GrievanceInvestigationNote> builder)
    {
        builder.ToTable("InvestigationNote", "grv");
        builder.HasKey(note => note.Id).HasName("PK_InvestigationNote");
        builder.Property(note => note.Id).UseIdentityColumn();
        builder.Ignore(note => note.IsVisibleToReviewers);

        builder.Property(note => note.Kind).HasTextConversion(GrievanceText.NoteKind, 20);
        builder.Property(note => note.Body).HasMaxLength(GrievanceInvestigationNote.BodyMaxLength);
        builder.Property(note => note.InterviewDate).HasColumnType("date");
        builder.Property(note => note.IntervieweeText).HasMaxLength(GrievanceInvestigationNote.IntervieweeMaxLength);
        builder.Property(note => note.AuthorName).HasMaxLength(GrievanceStatusHistoryEntry.ActorNameMaxLength);
        builder.Property(note => note.CreatedAtUtc).HasColumnType("datetime2(3)");

        builder.HasOne<GrievanceCase>().WithMany()
            .HasForeignKey(note => note.CaseId)
            .HasConstraintName("FK_InvestigationNote_Case")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(note => note.AuthorEmployeeId)
            .HasConstraintName("FK_InvestigationNote_Employee_AuthorEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(note => new { note.CaseId, note.CreatedAtUtc }).HasDatabaseName("IX_InvestigationNote_CaseId_CreatedAtUtc");
        builder.HasIndex(note => note.AuthorEmployeeId).HasDatabaseName("IX_InvestigationNote_AuthorEmployeeId");
    }
}

/// <summary>Maps grv.Communication: append-only, no row version. The reply link (InReplyToId, CaseId) is a composite key in the database.</summary>
internal sealed class GrievanceCommunicationConfiguration : IEntityTypeConfiguration<GrievanceCommunication>
{
    public void Configure(EntityTypeBuilder<GrievanceCommunication> builder)
    {
        builder.ToTable("Communication", "grv");
        builder.HasKey(message => message.Id).HasName("PK_Communication");
        builder.Property(message => message.Id).UseIdentityColumn();

        builder.Property(message => message.Direction).HasTextConversion(GrievanceText.MessageDirection, 20);
        builder.Property(message => message.Kind).HasTextConversion(GrievanceText.MessageKind, 20);
        builder.Property(message => message.Body).HasMaxLength(GrievanceCommunication.BodyMaxLength);
        builder.Property(message => message.AuthorRole).HasTextConversion(GrievanceText.ActorRole, 20);
        builder.Property(message => message.AuthorName).HasMaxLength(GrievanceStatusHistoryEntry.ActorNameMaxLength);
        builder.Property(message => message.CreatedAtUtc).HasColumnType("datetime2(3)");

        builder.HasOne<GrievanceCase>().WithMany()
            .HasForeignKey(message => message.CaseId)
            .HasConstraintName("FK_Communication_Case")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(message => message.AuthorEmployeeId)
            .HasConstraintName("FK_Communication_Employee_AuthorEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(message => new { message.CaseId, message.CreatedAtUtc }).HasDatabaseName("IX_Communication_CaseId_CreatedAtUtc");
        builder.HasIndex(message => new { message.InReplyToId, message.CaseId }).HasDatabaseName("IX_Communication_InReplyToId_CaseId");
        builder.HasIndex(message => message.AuthorEmployeeId).HasDatabaseName("IX_Communication_AuthorEmployeeId");
    }
}

/// <summary>Maps grv.Attachment: file metadata only, the file lives in the grievance file storage under the storage key.</summary>
internal sealed class GrievanceAttachmentConfiguration : IEntityTypeConfiguration<GrievanceAttachment>
{
    public void Configure(EntityTypeBuilder<GrievanceAttachment> builder)
    {
        builder.ToTable("Attachment", "grv", table => table.HasTrigger("TR_Attachment_Fixed"));
        builder.HasKey(attachment => attachment.Id).HasName("PK_Attachment");
        builder.Property(attachment => attachment.Id).UseIdentityColumn();
        builder.ConfigureConfidential();
        builder.Ignore(attachment => attachment.IsRemoved);

        builder.Property(attachment => attachment.Visibility).HasTextConversion(GrievanceText.Visibility, 20);
        builder.Property(attachment => attachment.UploadedByRole).HasTextConversion(GrievanceText.ActorRole, 20);
        builder.Property(attachment => attachment.FileName).HasMaxLength(GrievanceAttachment.FileNameMaxLength);
        builder.Property(attachment => attachment.ContentType).HasMaxLength(GrievanceAttachment.ContentTypeMaxLength).IsUnicode(false);
        builder.Property(attachment => attachment.SizeBytes).HasColumnType("bigint");
        builder.Property(attachment => attachment.StorageKey).HasColumnType("char(32)").HasMaxLength(GrievanceAttachment.StorageKeyLength).IsFixedLength().IsUnicode(false);
        builder.Property(attachment => attachment.Sha256)
            .HasColumnType($"binary({GrievanceAttachment.Sha256Length})")
            .HasMaxLength(GrievanceAttachment.Sha256Length)
            .IsFixedLength();
        builder.Property(attachment => attachment.UploadedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(attachment => attachment.RemovedAtUtc).HasColumnType("datetime2(3)");

        builder.HasIndex(attachment => attachment.StorageKey).IsUnique().HasDatabaseName("UQ_Attachment_StorageKey");
        builder.HasOne<GrievanceCase>().WithMany()
            .HasForeignKey(attachment => attachment.CaseId)
            .HasConstraintName("FK_Attachment_Case")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(attachment => attachment.CaseId).HasDatabaseName("IX_Attachment_CaseId");
        builder.HasIndex(attachment => new { attachment.CommunicationId, attachment.CaseId }).HasDatabaseName("IX_Attachment_CommunicationId_CaseId");
        builder.HasIndex(attachment => new { attachment.NoteId, attachment.CaseId }).HasDatabaseName("IX_Attachment_NoteId_CaseId");
    }
}

/// <summary>Maps grv.CorrectiveAction (audited: HR only, never the submitter).</summary>
internal sealed class GrievanceCorrectiveActionConfiguration : IEntityTypeConfiguration<GrievanceCorrectiveAction>
{
    public void Configure(EntityTypeBuilder<GrievanceCorrectiveAction> builder)
    {
        builder.ToTable("CorrectiveAction", "grv");
        builder.HasKey(action => action.Id).HasName("PK_CorrectiveAction");
        builder.Property(action => action.Id).UseIdentityColumn();
        builder.ConfigureAudit();

        builder.Property(action => action.ActionType).HasMaxLength(GrievanceCorrectiveAction.ActionTypeMaxLength);
        builder.Property(action => action.Description).HasMaxLength(GrievanceCorrectiveAction.DescriptionMaxLength);
        builder.Property(action => action.ResponsibleText).HasMaxLength(GrievanceCorrectiveAction.ResponsibleMaxLength);
        builder.Property(action => action.DueDate).HasColumnType("date");
        builder.Property(action => action.CompletedOn).HasColumnType("date");
        builder.Property(action => action.Status).HasTextConversion(GrievanceText.CorrectiveActionStatus, 20);

        builder.HasOne<GrievanceCase>().WithMany()
            .HasForeignKey(action => action.CaseId)
            .HasConstraintName("FK_CorrectiveAction_Case")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(action => action.CaseId).HasDatabaseName("IX_CorrectiveAction_CaseId");
    }
}

/// <summary>Maps grv.ResolutionProposal. One pending proposal per case; nobody decides their own (CHECK in the database).</summary>
internal sealed class GrievanceResolutionProposalConfiguration : IEntityTypeConfiguration<GrievanceResolutionProposal>
{
    public void Configure(EntityTypeBuilder<GrievanceResolutionProposal> builder)
    {
        builder.ToTable("ResolutionProposal", "grv");
        builder.HasKey(proposal => proposal.Id).HasName("PK_ResolutionProposal");
        builder.Property(proposal => proposal.Id).UseIdentityColumn();
        builder.Ignore(proposal => proposal.IsPending);

        builder.Property(proposal => proposal.Summary).HasMaxLength(GrievanceResolutionProposal.SummaryMaxLength);
        builder.Property(proposal => proposal.ProposedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(proposal => proposal.Decision).HasTextConversion(GrievanceText.Decision, 20);
        builder.Property(proposal => proposal.DecidedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(proposal => proposal.DecisionComment).HasMaxLength(GrievanceResolutionProposal.CommentMaxLength);

        builder.HasOne<GrievanceCase>().WithMany()
            .HasForeignKey(proposal => proposal.CaseId)
            .HasConstraintName("FK_ResolutionProposal_Case")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Employee>().WithMany()
            .HasForeignKey(proposal => proposal.ProposedByEmployeeId)
            .HasConstraintName("FK_ResolutionProposal_Employee_ProposedByEmployeeId")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(proposal => proposal.CaseId, "UQ_ResolutionProposal_CaseId_Pending")
            .IsUnique()
            .HasFilter("[Decision] = 'Pending'");
        builder.HasIndex(proposal => proposal.CaseId, "IX_ResolutionProposal_CaseId");
        builder.HasIndex(proposal => proposal.DecidedByUserId, "IX_ResolutionProposal_DecidedByUserId");
        builder.HasIndex(proposal => proposal.ProposedByEmployeeId, "IX_ResolutionProposal_ProposedByEmployeeId");
    }
}

/// <summary>Maps grv.Escalation. One open (not cleared) escalation per case; escalations are not cleared in v1.</summary>
internal sealed class GrievanceEscalationConfiguration : IEntityTypeConfiguration<GrievanceEscalation>
{
    public void Configure(EntityTypeBuilder<GrievanceEscalation> builder)
    {
        builder.ToTable("Escalation", "grv");
        builder.HasKey(escalation => escalation.Id).HasName("PK_Escalation");
        builder.Property(escalation => escalation.Id).UseIdentityColumn();

        builder.Property(escalation => escalation.Reason).HasMaxLength(GrievanceEscalation.ReasonMaxLength);
        builder.Property(escalation => escalation.EscalatedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(escalation => escalation.ClearedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(escalation => escalation.Outcome).HasMaxLength(1000);

        builder.HasOne<GrievanceCase>().WithMany()
            .HasForeignKey(escalation => escalation.CaseId)
            .HasConstraintName("FK_Escalation_Case")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(escalation => escalation.CaseId, "UQ_Escalation_CaseId_Open")
            .IsUnique()
            .HasFilter("[ClearedAtUtc] IS NULL");
        builder.HasIndex(escalation => escalation.CaseId, "IX_Escalation_CaseId");
    }
}

/// <summary>Maps grv.AuditLog: bigint key, append-only, CaseId deliberately not a foreign key.</summary>
internal sealed class GrievanceAuditEntryConfiguration : IEntityTypeConfiguration<GrievanceAuditEntry>
{
    public void Configure(EntityTypeBuilder<GrievanceAuditEntry> builder)
    {
        builder.ToTable("AuditLog", "grv");
        builder.HasKey(entry => entry.Id).HasName("PK_AuditLog");
        builder.Property(entry => entry.Id).UseIdentityColumn();

        builder.Property(entry => entry.Action).HasTextConversion(GrievanceText.AuditAction, 40);
        builder.Property(entry => entry.ActorRole).HasTextConversion(GrievanceText.ActorRole, 20);
        builder.Property(entry => entry.OccurredAtUtc).HasColumnType("datetime2(3)");
        builder.Property(entry => entry.Detail).HasMaxLength(GrievanceAuditEntry.DetailMaxLength);

        builder.HasIndex(entry => new { entry.CaseId, entry.OccurredAtUtc }).HasDatabaseName("IX_AuditLog_CaseId_OccurredAtUtc");
        builder.HasIndex(entry => new { entry.ActorUserId, entry.OccurredAtUtc }).HasDatabaseName("IX_AuditLog_ActorUserId_OccurredAtUtc");
    }
}

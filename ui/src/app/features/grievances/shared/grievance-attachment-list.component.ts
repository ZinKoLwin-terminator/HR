import { Component, computed, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import {
  GRIEVANCE_ATTACHMENT_ALLOWED_EXTENSIONS,
  GRIEVANCE_ATTACHMENT_VISIBILITY_LABELS,
  GrievanceAttachmentVisibility,
} from '../../../core/models/grievance.models';
import {
  GRIEVANCE_FILES_MAX,
  GrievanceUploader,
  grievanceFileHint,
  grievanceFileRefusal,
  grievanceFileSize,
} from '../../../core/utilities/grievance-files';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

/** One file as the list shows it. `id` null: chosen but not uploaded yet (a new draft). */
export interface GrievanceFileItem {
  key: string;
  id: number | null;
  fileName: string;
  sizeBytes: number;
  uploadedAtUtc: string | null;
  /** "You", "HR team", or a role (case team). */
  uploadedBy: string | null;
  /** Shown to the case team only. */
  visibility?: GrievanceAttachmentVisibility;
}

/**
 * The files of a grievance: name, size, date, a download button (saved, never shown inline), and
 * with `canAdd` an "Add file" button that opens the picker. Name, type, size and count are checked
 * here first (`grievanceFileRefusal`); the parent uploads and reports the server's answer.
 */
@Component({
  selector: 'app-grievance-attachment-list',
  imports: [AppDatePipe, MatButtonModule, MatIconModule],
  template: `
    <div class="files" role="group" [attr.aria-label]="label()">
      @if (files().length === 0) {
        <p class="muted">No files attached.</p>
      } @else {
        <ul class="list">
          @for (file of files(); track file.key) {
            <li>
              @if (file.id !== null) {
                <button
                  mat-button
                  type="button"
                  class="name"
                  [attr.aria-label]="'Download ' + file.fileName"
                  (click)="download.emit(file)"
                >
                  <mat-icon>download</mat-icon>{{ file.fileName }}
                </button>
              } @else {
                <span class="name pending"
                  ><mat-icon aria-hidden="true">schedule</mat-icon>{{ file.fileName }}</span
                >
              }
              <span class="meta">
                {{ size(file.sizeBytes) }}
                @if (file.uploadedAtUtc) {
                  · {{ file.uploadedAtUtc | appDate }}
                } @else {
                  · uploaded when you save
                }
                @if (file.uploadedBy) {
                  · {{ file.uploadedBy }}
                }
                @if (file.visibility) {
                  · {{ visibilityLabels[file.visibility] }}
                }
              </span>
              @if (file.id === null && canAdd()) {
                <button
                  mat-icon-button
                  type="button"
                  [attr.aria-label]="'Remove ' + file.fileName"
                  (click)="removePending.emit(file)"
                >
                  <mat-icon>close</mat-icon>
                </button>
              }
            </li>
          }
        </ul>
      }
      @if (canAdd()) {
        @if (warning()) {
          <p class="warning" role="note">{{ warning() }}</p>
        }
        <div class="add">
          <input
            #picker
            type="file"
            hidden
            [accept]="accept"
            aria-label="Choose a file to attach"
            data-testid="grievance-file-input"
            (change)="picked($event)"
          />
          <button
            mat-stroked-button
            type="button"
            aria-describedby="grievance-file-hint"
            [disabled]="busy() || full()"
            (click)="picker.click()"
          >
            <mat-icon>attach_file</mat-icon>Add file
          </button>
          <span class="muted" id="grievance-file-hint">{{ hint() }}</span>
        </div>
      }
      @if (problem() || error()) {
        <p class="error" role="alert">{{ problem() || error() }}</p>
      }
    </div>
  `,
  styles: `
    .list {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .list li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0 8px;
    }
    .name {
      max-width: 100%;
      overflow-wrap: anywhere;
      text-align: start;
    }
    .pending {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 0 12px;
    }
    .muted,
    .meta {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .muted {
      margin: 0;
    }
    .add {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px 12px;
      margin-top: 8px;
    }
    .warning {
      margin: 8px 0 0;
      padding: 8px 12px;
      border-radius: var(--app-radius-md);
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
      font: var(--mat-sys-body-small);
    }
    .error {
      margin: 6px 0 0;
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class GrievanceAttachmentListComponent {
  readonly files = input.required<GrievanceFileItem[]>();
  readonly canAdd = input(false);
  readonly busy = input(false);
  /** The server's refusal of the last upload, shown under the list. */
  readonly error = input<string | null>(null);
  readonly label = input('Attachments');
  /** Whose quota applies to new files: the submitter's or the case team's (separate per case). */
  readonly uploader = input<GrievanceUploader>('submitter');
  /** Files this side already added (its quota); defaults to every listed file. */
  readonly ownCount = input<number | null>(null);
  /** A caution shown above the Add button (for example the anonymous-case metadata warning). */
  readonly warning = input<string | null>(null);
  readonly add = output<File>();
  readonly download = output<GrievanceFileItem>();
  readonly removePending = output<GrievanceFileItem>();

  protected readonly hint = computed(() => grievanceFileHint(this.uploader()));
  protected readonly accept = GRIEVANCE_ATTACHMENT_ALLOWED_EXTENSIONS.join(',');
  protected readonly visibilityLabels = GRIEVANCE_ATTACHMENT_VISIBILITY_LABELS;
  protected readonly size = grievanceFileSize;
  protected readonly problem = signal<string | null>(null);
  private readonly used = computed(() => this.ownCount() ?? this.files().length);
  protected readonly full = computed(() => this.used() >= GRIEVANCE_FILES_MAX[this.uploader()]);

  protected picked(event: Event): void {
    const element = event.target as HTMLInputElement;
    const file = element.files?.[0];
    // Cleared so that choosing the same file again fires another change.
    element.value = '';
    if (file) {
      this.offer(file);
    }
  }

  /** Checks a chosen file and emits it when it may be added. */
  offer(file: File): void {
    const refusal = grievanceFileRefusal(file, this.used(), this.uploader());
    this.problem.set(refusal);
    if (refusal === null) {
      this.add.emit(file);
    }
  }
}

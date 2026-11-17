import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import {
  EVIDENCE_ALLOWED_EXTENSIONS,
  EVIDENCE_MAX_PER_KPI,
  KpiAttachment,
  ScorecardItem,
} from '../../../core/models/kpi.models';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { fileSize } from './scorecard-rules';

/**
 * The evidence files of one KPI: name, size and upload date, each with a download button (the
 * file is saved, never shown inline). With `editable` (the owner, while results can be entered) it
 * also offers "Add evidence" (a keyboard-reachable button that opens the file picker) and Remove.
 * The parent does the checks and the calls, so it can thread the scorecard's row version.
 */
@Component({
  selector: 'app-evidence-list',
  imports: [AppDatePipe, MatButtonModule, MatIconModule],
  template: `
    @let code = item().kpiCode;
    <div class="evidence" role="group" [attr.aria-label]="'Evidence for ' + code">
      <h4 class="title">
        Evidence
        <span class="count">({{ attachments().length }} of {{ max }})</span>
      </h4>
      @if (attachments().length === 0) {
        <p class="muted">No evidence attached.</p>
      } @else {
        <ul class="files">
          @for (file of attachments(); track file.id) {
            <li>
              <button
                mat-button
                type="button"
                class="file"
                [attr.aria-label]="'Download ' + file.fileName"
                (click)="download.emit(file)"
              >
                <mat-icon>download</mat-icon>{{ file.fileName }}
              </button>
              <span class="meta">{{ size(file.sizeBytes) }} · {{ file.uploadedOn | appDate }}</span>
              @if (editable()) {
                <button
                  mat-icon-button
                  type="button"
                  [attr.aria-label]="'Remove ' + file.fileName"
                  [disabled]="disabled()"
                  (click)="remove.emit(file)"
                >
                  <mat-icon>delete</mat-icon>
                </button>
              }
            </li>
          }
        </ul>
      }
      @if (editable()) {
        <div class="add">
          <input
            #picker
            type="file"
            hidden
            [accept]="accept"
            [attr.aria-label]="'Choose an evidence file for ' + code"
            [attr.data-testid]="'evidence-input-' + code"
            (change)="picked($event)"
          />
          <button
            mat-stroked-button
            type="button"
            [attr.aria-label]="'Add evidence to ' + code"
            [attr.aria-describedby]="'evidence-hint-' + item().id"
            [disabled]="disabled() || full()"
            (click)="picker.click()"
          >
            <mat-icon>attach_file</mat-icon>Add evidence
          </button>
          <span class="hint" [id]="'evidence-hint-' + item().id">
            @if (full()) {
              This KPI has {{ max }} files, the most allowed. Remove one to add another.
            } @else {
              PDF, PNG, JPEG, Word (.docx) or Excel (.xlsx), up to 10 MB.
            }
          </span>
        </div>
      }
      @if (error()) {
        <p class="error" role="alert">{{ error() }}</p>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .title {
      margin: 0 0 4px;
      font: var(--mat-sys-title-small);
    }
    .count,
    .muted,
    .meta,
    .hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .muted {
      margin: 0;
    }
    .files {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .files li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0 8px;
    }
    .file {
      max-width: 100%;
      overflow-wrap: anywhere;
      text-align: start;
    }
    .add {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px 12px;
      margin-top: 6px;
    }
    .error {
      margin: 6px 0 0;
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class EvidenceListComponent {
  readonly item = input.required<ScorecardItem>();
  /** Offers Add evidence and Remove (the owner, while the scorecard is Draft or Returned). */
  readonly editable = input(false);
  /** Disables Add and Remove while another write runs. */
  readonly disabled = input(false);
  /** A refused file (the client check or the server's answer), shown under the list. */
  readonly error = input<string | null>(null);
  readonly add = output<File>();
  readonly remove = output<KpiAttachment>();
  readonly download = output<KpiAttachment>();

  protected readonly max = EVIDENCE_MAX_PER_KPI;
  protected readonly accept = EVIDENCE_ALLOWED_EXTENSIONS.join(',');
  protected readonly size = fileSize;
  protected readonly attachments = computed(() => this.item().attachments ?? []);
  protected readonly full = computed(() => this.attachments().length >= EVIDENCE_MAX_PER_KPI);

  protected picked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    // Cleared so that choosing the same file again fires another change.
    input.value = '';
    if (file) this.add.emit(file);
  }
}

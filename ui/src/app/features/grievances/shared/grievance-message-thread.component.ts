import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import {
  GRIEVANCE_COMMUNICATION_KIND_LABELS,
  GrievanceCommunicationDirection,
  GrievanceCommunicationKind,
  GrievanceMessage,
  MyGrievanceMessage,
} from '../../../core/models/grievance.models';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { RelativeTimePipe } from '../../../shared/pipes/relative-time.pipe';

/** One message as the thread shows it. `mine` aligns it on the viewer's side. */
export interface GrievanceThreadMessage {
  id: number;
  direction: GrievanceCommunicationDirection;
  kind: GrievanceCommunicationKind;
  body: string;
  author: string;
  mine: boolean;
  createdAtUtc: string;
}

/** The submitter's thread: their own messages are "You", HR's are "HR team". */
export function myThreadMessages(
  messages: readonly MyGrievanceMessage[],
): GrievanceThreadMessage[] {
  return messages.map((m) => ({
    id: m.id,
    direction: m.direction,
    kind: m.kind,
    body: m.body,
    author: m.actor,
    mine: m.direction === 'FromSubmitter',
    createdAtUtc: m.createdAtUtc,
  }));
}

/** The case team's thread (the anonymous submitter is "Anonymous submitter"). */
export function teamThreadMessages(
  messages: readonly GrievanceMessage[],
): GrievanceThreadMessage[] {
  return messages.map((m) => ({
    id: m.id,
    direction: m.direction,
    kind: m.kind,
    body: m.body,
    author: m.authorName,
    mine: m.direction === 'ToSubmitter',
    createdAtUtc: m.createdAtUtc,
  }));
}

/**
 * The messages of a case, oldest first, with an optional reply box. In respond mode (the case awaits
 * the submitter) the box asks for the answer to HR's request. The parent sends and clears.
 */
@Component({
  selector: 'app-grievance-message-thread',
  imports: [
    AppDatePipe,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    RelativeTimePipe,
  ],
  template: `
    @if (messages().length === 0) {
      <p class="muted">No messages yet.</p>
    } @else {
      <ul class="thread" aria-label="Messages">
        @for (m of messages(); track m.id) {
          <li class="message" [class.mine]="m.mine" [attr.data-kind]="m.kind">
            <p class="head">
              <strong>{{ m.author }}</strong>
              @if (m.kind !== 'Message') {
                <span class="kind">{{ kindLabels[m.kind] }}</span>
              }
              <time [attr.datetime]="m.createdAtUtc" [title]="m.createdAtUtc | appDate">{{
                m.createdAtUtc | relativeTime
              }}</time>
            </p>
            <p class="body">{{ m.body }}</p>
          </li>
        }
      </ul>
    }

    @if (canReply()) {
      <form class="reply" (ngSubmit)="submit()">
        @if (respondMode()) {
          <p class="request" role="note">
            <mat-icon aria-hidden="true">help</mat-icon>
            HR has asked you for more information. Your answer moves the grievance forward again.
          </p>
        }
        <mat-form-field class="field" subscriptSizing="dynamic">
          <mat-label>{{ respondMode() ? "Your answer to HR's request" : replyLabel() }}</mat-label>
          <textarea
            matInput
            name="reply"
            rows="3"
            [maxlength]="maxLength()"
            [ngModel]="draft()"
            (ngModelChange)="draft.set($event)"
            [disabled]="busy()"
          ></textarea>
          <mat-hint align="end">{{ draft().length }} / {{ maxLength() }}</mat-hint>
        </mat-form-field>
        <div class="actions">
          <button mat-flat-button type="submit" [disabled]="busy() || blank()">
            <mat-icon>send</mat-icon>{{ respondMode() ? "Respond to HR's request" : 'Send' }}
          </button>
        </div>
      </form>
    }
  `,
  styles: `
    .thread {
      display: flex;
      flex-direction: column;
      gap: 10px;
      margin: 0 0 12px;
      padding: 0;
      list-style: none;
    }
    .message {
      max-width: 80%;
      padding: 8px 12px;
      border-radius: var(--app-radius-md);
      background: var(--mat-sys-surface-container);
    }
    .message.mine {
      align-self: flex-end;
      background: var(--mat-sys-secondary-container);
    }
    .message[data-kind='InfoRequest'] {
      box-shadow: inset 3px 0 0 var(--app-color-warning);
    }
    .head,
    .body {
      margin: 0;
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 8px;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .head strong {
      color: var(--mat-sys-on-surface);
    }
    .kind {
      font-weight: 600;
    }
    .body {
      margin-top: 2px;
      white-space: pre-line;
      overflow-wrap: anywhere;
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
    .reply {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .request {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0;
      padding: 8px 12px;
      border-radius: var(--app-radius-md);
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
    }
    .field {
      width: 100%;
    }
    .actions {
      display: flex;
      justify-content: flex-end;
    }
  `,
})
export class GrievanceMessageThreadComponent {
  readonly messages = input.required<GrievanceThreadMessage[]>();
  readonly canReply = input(false);
  readonly respondMode = input(false);
  readonly busy = input(false);
  readonly replyLabel = input('Message');
  readonly maxLength = input(4000);
  readonly send = output<string>();

  protected readonly kindLabels = GRIEVANCE_COMMUNICATION_KIND_LABELS;
  protected readonly draft = signal('');
  protected readonly blank = computed(() => this.draft().trim() === '');

  protected submit(): void {
    const text = this.draft().trim();
    if (text !== '' && !this.busy()) {
      this.send.emit(text);
    }
  }

  /** Clears the box after the parent sent the message. */
  clear(): void {
    this.draft.set('');
  }
}

import { Component, computed, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import {
  MAP_EDGES,
  MAP_NODES,
  MapEdge,
  MapNode,
  WORKFLOWS,
} from '../../core/constants/workflow-guide';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { HelpLanguageService, HelpTabsComponent } from './help-tabs.component';

const NODE_W = 160;
const NODE_H = 56;

const LABELS = {
  en: {
    description: 'How the modules connect, and the step-by-step flow of everyday HR work.',
    mapTitle: 'How the modules connect',
    mapHint:
      'Select a module to see what it receives and what it gives. Dashed arrow = done by hand.',
    none: 'Select a module in the diagram.',
    receives: 'Gets data from',
    gives: 'Gives data to',
    nothing: 'Nothing — it is a starting point.',
    manual: 'by hand',
    open: 'Open',
    flows: 'Step-by-step workflows',
    step: 'Step',
    back: 'Module guide: what each screen does',
  },
  my: {
    description: 'Module များ မည်သို့ ဆက်စပ်သည်နှင့် နေ့စဉ် HR လုပ်ငန်းများ၏ အဆင့်ဆင့်လုပ်ငန်းစဉ်။',
    mapTitle: 'Module များ ဆက်စပ်ပုံ',
    mapHint:
      'Module တစ်ခုကို ရွေးပါ — ဘာတွေရပြီး ဘာတွေပေးသည်ကို မြင်ရမည်။ မျဉ်းပြတ်မြားသည် လက်ဖြင့်လုပ်ရသည်ကို ဆိုလိုသည်။',
    none: 'ပုံထဲက Module တစ်ခုကို ရွေးပါ။',
    receives: 'ဒေတာရယူသည့်နေရာ',
    gives: 'ဒေတာပေးသည့်နေရာ',
    nothing: 'မရှိ — စတင်သည့်နေရာဖြစ်သည်။',
    manual: 'လက်ဖြင့်',
    open: 'ဖွင့်ရန်',
    flows: 'အဆင့်ဆင့် Workflow များ',
    step: 'အဆင့်',
    back: 'Module Guide — Screen တစ်ခုချင်းစီ၏ လုပ်ဆောင်ချက်',
  },
} as const;

interface Line {
  edge: MapEdge;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
}

const byId = new Map<string, MapNode>(MAP_NODES.map((n) => [n.id, n]));

/** Point where the segment from the centre of `a` towards `b` leaves a's box (plus a small gap). */
function exitPoint(a: MapNode, b: MapNode): [number, number] {
  const cx = a.x + NODE_W / 2;
  const cy = a.y + NODE_H / 2;
  const dx = b.x + NODE_W / 2 - cx;
  const dy = b.y + NODE_H / 2 - cy;
  const gap = 6;
  const scale = Math.min(
    dx === 0 ? Infinity : (NODE_W / 2 + gap) / Math.abs(dx),
    dy === 0 ? Infinity : (NODE_H / 2 + gap) / Math.abs(dy),
  );
  return [cx + dx * scale, cy + dy * scale];
}

const LINES: readonly Line[] = MAP_EDGES.map((edge) => {
  const a = byId.get(edge.from)!;
  const b = byId.get(edge.to)!;
  const [x1, y1] = exitPoint(a, b);
  const [x2, y2] = exitPoint(b, a);
  return { edge, x1, y1, x2, y2 };
});

/** Interactive system map plus the end-to-end workflows. */
@Component({
  selector: 'app-workflow-guide',
  imports: [RouterLink, MatIconModule, PageHeaderComponent, HelpTabsComponent],
  template: `
    <app-page-header title="Help" [description]="t().description">
      <app-help-tabs pageActions />
    </app-page-header>

    <a class="back-link" routerLink="/help/modules">
      <mat-icon aria-hidden="true">arrow_back</mat-icon>{{ t().back }}
    </a>

    <section class="card" aria-labelledby="map-heading">
      <h2 id="map-heading">{{ t().mapTitle }}</h2>
      <p class="hint">{{ t().mapHint }}</p>

      <div class="map-layout">
        <svg
          class="map"
          viewBox="0 0 940 520"
          role="group"
          [attr.aria-label]="t().mapTitle"
          preserveAspectRatio="xMidYMid meet"
        >
          <defs>
            <marker
              id="arrow"
              viewBox="0 0 10 10"
              refX="9"
              refY="5"
              markerWidth="7"
              markerHeight="7"
              orient="auto-start-reverse"
            >
              <path d="M0,0 L10,5 L0,10 z" fill="currentColor" />
            </marker>
          </defs>

          @for (l of lines; track $index) {
            <line
              class="edge"
              [class.edge--on]="isOn(l.edge)"
              [class.edge--dim]="selected() && !isOn(l.edge)"
              [class.edge--manual]="l.edge.manual"
              [attr.x1]="l.x1"
              [attr.y1]="l.y1"
              [attr.x2]="l.x2"
              [attr.y2]="l.y2"
              marker-end="url(#arrow)"
            />
          }

          @for (n of nodes; track n.id) {
            <g
              class="node"
              role="button"
              tabindex="0"
              [class.node--on]="selected() === n.id"
              [class.node--dim]="selected() && !isRelated(n.id)"
              [attr.aria-pressed]="selected() === n.id"
              [attr.aria-label]="n.label"
              (click)="select(n.id)"
              (keydown.enter)="select(n.id)"
              (keydown.space)="select(n.id); $event.preventDefault()"
            >
              <rect [attr.x]="n.x" [attr.y]="n.y" width="160" height="56" rx="14" />
              <text [attr.x]="n.x + 80" [attr.y]="n.y + 33" text-anchor="middle">
                {{ n.label }}
              </text>
            </g>
          }
        </svg>

        <aside class="detail" aria-live="polite">
          @if (current(); as n) {
            <h3>
              <mat-icon aria-hidden="true">{{ n.icon }}</mat-icon
              >{{ n.label }}
            </h3>
            <p>{{ n.summary[lang()] }}</p>

            <h4>{{ t().receives }}</h4>
            <ul>
              @for (e of incoming(); track $index) {
                <li>
                  <strong>{{ label(e.from) }}</strong>
                  @if (e.manual) {
                    <span class="tag">{{ t().manual }}</span>
                  }
                  <span>{{ e.label[lang()] }}</span>
                </li>
              } @empty {
                <li class="empty">{{ t().nothing }}</li>
              }
            </ul>

            <h4>{{ t().gives }}</h4>
            <ul>
              @for (e of outgoing(); track $index) {
                <li>
                  <strong>{{ label(e.to) }}</strong>
                  @if (e.manual) {
                    <span class="tag">{{ t().manual }}</span>
                  }
                  <span>{{ e.label[lang()] }}</span>
                </li>
              } @empty {
                <li class="empty">{{ t().nothing }}</li>
              }
            </ul>

            @if (n.route) {
              <a class="open" [routerLink]="n.route">
                {{ t().open }} {{ n.label }}<mat-icon aria-hidden="true">arrow_forward</mat-icon>
              </a>
            }
          } @else {
            <p class="empty">{{ t().none }}</p>
          }
        </aside>
      </div>
    </section>

    <h2 class="flows-title">{{ t().flows }}</h2>
    <div class="picker" role="tablist" [attr.aria-label]="t().flows">
      @for (w of workflows; track w.id) {
        <button
          type="button"
          role="tab"
          [attr.aria-selected]="flow().id === w.id"
          [class.on]="flow().id === w.id"
          (click)="flowId.set(w.id)"
        >
          {{ w.title[lang()] }}
        </button>
      }
    </div>

    <section class="card" role="tabpanel">
      <p class="hint">{{ flow().summary[lang()] }}</p>
      <ol class="steps">
        @for (s of flow().steps; track $index; let i = $index) {
          <li>
            <span class="num" aria-hidden="true">{{ i + 1 }}</span>
            <div class="body">
              <div class="head">
                <strong>{{ s.title[lang()] }}</strong>
                <span class="module">{{ s.module }}</span>
              </div>
              <p>{{ s.text[lang()] }}</p>
              @if (s.route) {
                <a [routerLink]="s.route"
                  >{{ t().open }}<mat-icon aria-hidden="true">arrow_forward</mat-icon></a
                >
              }
            </div>
          </li>
        }
      </ol>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      margin-bottom: var(--app-spacing-md, 12px);
      color: var(--mat-sys-primary);
      font-weight: 500;
      text-decoration: none;
    }
    .back-link:hover,
    .back-link:focus-visible {
      text-decoration: underline;
    }
    .card {
      padding: var(--app-spacing-lg, 20px);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 16px;
      background: var(--mat-sys-surface-container-lowest, var(--mat-sys-surface));
      margin-bottom: var(--app-spacing-lg, 20px);
    }
    h2 {
      margin: 0 0 4px;
      font: var(--mat-sys-title-medium);
    }
    .flows-title {
      margin: 8px 0 12px;
    }
    .hint {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
      line-height: 1.7;
    }
    .map-layout {
      display: grid;
      grid-template-columns: minmax(0, 1.6fr) minmax(260px, 1fr);
      gap: 20px;
      align-items: start;
    }
    .map {
      width: 100%;
      height: auto;
      color: var(--mat-sys-outline);
    }
    .edge {
      stroke: currentColor;
      stroke-width: 1.5;
      transition: opacity 120ms ease;
    }
    .edge--manual {
      stroke-dasharray: 6 5;
    }
    .edge--on {
      color: var(--mat-sys-primary);
      stroke-width: 2.5;
    }
    .edge--dim {
      opacity: 0.18;
    }
    .node {
      cursor: pointer;
      outline: none;
    }
    .node rect {
      fill: var(--mat-sys-surface-container-high);
      stroke: var(--mat-sys-outline-variant);
      stroke-width: 1.5;
    }
    .node text {
      fill: var(--mat-sys-on-surface);
      font: 600 15px var(--mat-sys-body-large-font, system-ui, sans-serif);
      pointer-events: none;
    }
    .node:hover rect,
    .node:focus-visible rect {
      stroke: var(--mat-sys-primary);
      stroke-width: 2.5;
    }
    .node--on rect {
      fill: var(--mat-sys-primary-container);
      stroke: var(--mat-sys-primary);
      stroke-width: 2.5;
    }
    .node--on text {
      fill: var(--mat-sys-on-primary-container);
    }
    .node--dim {
      opacity: 0.35;
    }
    .detail {
      padding: 16px;
      border-radius: 14px;
      background: var(--mat-sys-surface-container);
      min-height: 160px;
    }
    .detail h3 {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 6px;
      font: var(--mat-sys-title-medium);
    }
    .detail h4 {
      margin: 14px 0 6px;
      font: var(--mat-sys-title-small);
    }
    .detail p {
      margin: 0;
      line-height: 1.7;
    }
    .detail ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .detail li {
      display: flex;
      flex-direction: column;
      gap: 2px;
      padding: 6px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      line-height: 1.7;
    }
    .empty {
      color: var(--mat-sys-on-surface-variant);
    }
    .tag {
      align-self: flex-start;
      padding: 0 8px;
      border-radius: 999px;
      background: var(--app-warning-container);
      color: var(--app-on-warning-container);
      font: var(--mat-sys-label-small);
    }
    a.open,
    .steps a {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      margin-top: 12px;
      color: var(--mat-sys-primary);
      font-weight: 500;
      text-decoration: none;
    }
    .steps a {
      margin-top: 4px;
    }
    a mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .picker {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-bottom: 12px;
    }
    .picker button {
      padding: 8px 14px;
      border-radius: 999px;
      border: 1px solid var(--mat-sys-outline-variant);
      background: transparent;
      color: var(--mat-sys-on-surface);
      font: var(--mat-sys-label-large);
      cursor: pointer;
    }
    .picker button:hover,
    .picker button:focus-visible {
      background: var(--mat-sys-surface-container-high);
    }
    .picker button.on {
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
      border-color: transparent;
    }
    .steps {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .steps li {
      display: grid;
      grid-template-columns: 36px 1fr;
      gap: 14px;
      position: relative;
      padding-bottom: 18px;
    }
    .steps li:not(:last-child)::before {
      content: '';
      position: absolute;
      left: 17px;
      top: 36px;
      bottom: 0;
      width: 2px;
      background: var(--mat-sys-outline-variant);
    }
    .num {
      display: grid;
      place-items: center;
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
      font-weight: 600;
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .module {
      padding: 0 10px;
      border-radius: 999px;
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      font: var(--mat-sys-label-small);
    }
    .body p {
      margin: 4px 0 0;
      line-height: 1.7;
    }
    @media (max-width: 900px) {
      .map-layout {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class WorkflowGuideComponent {
  protected readonly lang = inject(HelpLanguageService).lang;
  protected readonly t = computed(() => LABELS[this.lang()]);

  protected readonly nodes = MAP_NODES;
  protected readonly lines = LINES;
  protected readonly workflows = WORKFLOWS;

  protected readonly selected = signal<string | null>(null);
  protected readonly flowId = signal(WORKFLOWS[0].id);

  protected readonly current = computed(() =>
    this.selected() ? byId.get(this.selected()!) : null,
  );
  protected readonly incoming = computed(() => MAP_EDGES.filter((e) => e.to === this.selected()));
  protected readonly outgoing = computed(() => MAP_EDGES.filter((e) => e.from === this.selected()));
  protected readonly flow = computed(
    () => WORKFLOWS.find((w) => w.id === this.flowId()) ?? WORKFLOWS[0],
  );

  protected select(id: string): void {
    this.selected.update((cur) => (cur === id ? null : id));
  }

  protected label(id: string): string {
    return byId.get(id)?.label ?? id;
  }

  protected isOn(e: MapEdge): boolean {
    const s = this.selected();
    return !!s && (e.from === s || e.to === s);
  }

  protected isRelated(id: string): boolean {
    const s = this.selected();
    return (
      id === s ||
      MAP_EDGES.some((e) => (e.from === s && e.to === id) || (e.to === s && e.from === id))
    );
  }
}

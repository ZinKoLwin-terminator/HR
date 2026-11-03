import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  CURRENT_EMPLOYMENT_STATUSES,
  Employee,
  fullName,
} from '../../../core/models/employee.models';
import { errorMessage } from '../../../core/services/ui-notification.service';
import { AvatarComponent } from '../../../shared/components/avatar/avatar.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SearchFieldComponent } from '../../../shared/components/search-field/search-field.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { OrganizationLookupService, nameMap } from '../organization-lookups';

interface OrgNode {
  employee: Employee;
  name: string;
  title: string;
  department: string;
  reports: OrgNode[];
  /** All direct and indirect reports. */
  total: number;
}

function pruneTree(nodes: OrgNode[], term: string): OrgNode[] {
  const result: OrgNode[] = [];
  for (const node of nodes) {
    const reports = pruneTree(node.reports, term);
    const matches = `${node.name} ${node.employee.employeeCode} ${node.title} ${node.department}`
      .toLowerCase()
      .includes(term);
    if (matches || reports.length) {
      result.push({ ...node, reports: matches ? node.reports : reports });
    }
  }
  return result;
}

/** Organization chart derived from each employee's manager (read-only). */
@Component({
  selector: 'app-reporting-structure',
  imports: [
    NgTemplateOutlet,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeaderComponent,
    SearchFieldComponent,
    StatCardComponent,
    AvatarComponent,
    EmptyStateComponent,
  ],
  templateUrl: './reporting-structure.component.html',
  styleUrl: './reporting-structure.component.scss',
})
export class ReportingStructureComponent {
  private readonly lookupService = inject(OrganizationLookupService);
  protected readonly data = rxResource({ stream: () => this.lookupService.load() });
  protected readonly errorText = computed(() =>
    this.data.error() ? errorMessage(this.data.error()) : '',
  );

  protected readonly search = signal('');
  private readonly expanded = signal<ReadonlySet<number>>(new Set());
  private readonly initialised = signal(false);

  /** Complete tree of current employees. */
  protected readonly roots = computed<OrgNode[]>(() => {
    const l = this.data.value();
    if (!l) return [];
    const positionTitle = nameMap(l.positions, (p) => p.title);
    const departmentName = nameMap(l.departments, (d) => d.name);
    const people = l.employees.filter((e) =>
      CURRENT_EMPLOYMENT_STATUSES.includes(e.employmentStatus),
    );
    const ids = new Set(people.map((e) => e.id));

    const byManager = new Map<number | null, Employee[]>();
    for (const e of people) {
      // Employees whose manager has left (or none) become top-level.
      const key = e.managerId !== null && ids.has(e.managerId) ? e.managerId : null;
      byManager.set(key, [...(byManager.get(key) ?? []), e]);
    }
    const build = (e: Employee): OrgNode => {
      const reports = (byManager.get(e.id) ?? [])
        .map(build)
        .sort((a, b) => b.total - a.total || a.name.localeCompare(b.name));
      return {
        employee: e,
        name: fullName(e),
        title: positionTitle(e.positionId),
        department: departmentName(e.departmentId),
        reports,
        total: reports.reduce((sum, r) => sum + 1 + r.total, 0),
      };
    };
    return (byManager.get(null) ?? []).map(build).sort((a, b) => b.total - a.total);
  });

  protected readonly term = computed(() => this.search().trim().toLowerCase());
  protected readonly visibleRoots = computed(() => {
    const term = this.term();
    return term ? pruneTree(this.roots(), term) : this.roots();
  });

  protected readonly stats = computed(() => {
    const all: OrgNode[] = [];
    let depth = 0;
    const walk = (nodes: OrgNode[], level: number) => {
      depth = Math.max(depth, level);
      for (const n of nodes) {
        all.push(n);
        walk(n.reports, level + 1);
      }
    };
    walk(this.roots(), 1);
    const managers = all.filter((n) => n.reports.length > 0);
    return {
      people: all.length,
      managers: managers.length,
      levels: depth,
      span: managers.length
        ? Math.round((managers.reduce((s, m) => s + m.reports.length, 0) / managers.length) * 10) /
          10
        : 0,
    };
  });

  protected isOpen(node: OrgNode): boolean {
    if (this.term()) {
      return true; // searching reveals matches
    }
    if (!this.initialised()) {
      return this.defaultOpen(node);
    }
    return this.expanded().has(node.employee.id);
  }

  protected toggle(node: OrgNode): void {
    this.ensureInitialised();
    this.expanded.update((current) => {
      const next = new Set(current);
      if (next.has(node.employee.id)) next.delete(node.employee.id);
      else next.add(node.employee.id);
      return next;
    });
  }

  protected expandAll(): void {
    this.initialised.set(true);
    const ids = new Set<number>();
    const walk = (nodes: OrgNode[]) =>
      nodes.forEach((n) => {
        if (n.reports.length) ids.add(n.employee.id);
        walk(n.reports);
      });
    walk(this.roots());
    this.expanded.set(ids);
  }

  protected collapseAll(): void {
    this.initialised.set(true);
    this.expanded.set(new Set());
  }

  private ensureInitialised(): void {
    if (!this.initialised()) {
      const ids = new Set<number>();
      const walk = (nodes: OrgNode[]) =>
        nodes.forEach((n) => {
          if (this.defaultOpen(n)) ids.add(n.employee.id);
          walk(n.reports);
        });
      walk(this.roots());
      this.expanded.set(ids);
      this.initialised.set(true);
    }
  }

  /** Before any interaction: roots and their direct reports are open. */
  private defaultOpen(node: OrgNode): boolean {
    return (
      this.roots().some((r) => r === node || r.reports.includes(node)) && node.reports.length > 0
    );
  }
}

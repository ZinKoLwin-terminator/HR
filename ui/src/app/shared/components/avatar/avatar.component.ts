import { Component, computed, input } from '@angular/core';

const PALETTE = [
  '#3f6fd8',
  '#0f9d76',
  '#8a4fd1',
  '#c2408f',
  '#c77700',
  '#0277bd',
  '#5d7a1f',
  '#b4532a',
];

/** Profile photo with an initials fallback (deterministic color per name). */
@Component({
  selector: 'app-avatar',
  template: `
    @if (photoUrl()) {
      <img
        [src]="photoUrl()"
        [alt]="decorative() ? '' : name()"
        [style.width.px]="size()"
        [style.height.px]="size()"
      />
    } @else {
      <span
        class="initials"
        [style.width.px]="size()"
        [style.height.px]="size()"
        [style.font-size.px]="size() * 0.4"
        [style.background]="color()"
        [attr.role]="decorative() ? null : 'img'"
        [attr.aria-label]="decorative() ? null : name()"
        [attr.aria-hidden]="decorative() ? 'true' : null"
        >{{ initials() }}</span
      >
    }
  `,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
    }
    img,
    .initials {
      border-radius: 50%;
      object-fit: cover;
    }
    .initials {
      display: inline-grid;
      place-items: center;
      color: #fff;
      font-weight: 600;
      letter-spacing: 0.02em;
      user-select: none;
    }
  `,
})
export class AvatarComponent {
  readonly name = input.required<string>();
  readonly photoUrl = input<string | null>(null);
  readonly size = input(36);
  /** Set when the name is already shown next to the avatar. */
  readonly decorative = input(true);

  protected readonly initials = computed(() =>
    this.name()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join(''),
  );

  protected readonly color = computed(() => {
    let hash = 0;
    for (const char of this.name()) {
      hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
    }
    return PALETTE[hash % PALETTE.length];
  });
}

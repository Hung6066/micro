import {
  ChangeDetectionStrategy,
  Component,
  input,
  signal,
} from "@angular/core";

export interface HisHopeTreeNode {
  id: string;
  label: string;
  children?: readonly HisHopeTreeNode[];
}

@Component({
  selector: "hh-tree",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ul role="tree">
    @for (node of nodes(); track node.id) {
      <li role="treeitem">
        <button
          type="button"
          (click)="toggle(node.id)"
          [attr.aria-expanded]="expanded().has(node.id)"
        >
          {{
            node.children?.length ? (expanded().has(node.id) ? "-" : "+") : "-"
          }}
          {{ node.label }}
        </button>
        @if (node.children?.length && expanded().has(node.id)) {
          <hh-tree [nodes]="node.children" />
        }
      </li>
    }
  </ul>`,
  styles: [
    `
      :host {
        display: block;
      }
      ul {
        list-style: none;
        margin: 0;
        padding-left: 18px;
      }
      button {
        border: 0;
        background: transparent;
        color: var(--text-primary);
        cursor: pointer;
        padding: 4px;
      }
    `,
  ],
})
export class HisHopeTreeComponent {
  readonly nodes = input<readonly HisHopeTreeNode[]>([]);
  readonly expanded = signal(new Set<string>());
  toggle(id: string): void {
    this.expanded.update((current) => {
      const next = new Set(current);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }
}

import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  ViewChild,
  computed,
  input,
  output,
  signal,
} from '@angular/core';

export interface HisHopeFileUploadRejection {
  file: File;
  reason: 'type' | 'size';
}

/**
 * Drag-and-drop + click-to-browse file picker with client-side mime/size
 * validation. Rejected files are surfaced via `rejected` and never added to
 * `files`; the server remains the authority for real content validation.
 */
@Component({
  selector: 'hh-file-upload',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'hh-file-upload' },
  template: `
    <div
      class="hh-file-upload__dropzone"
      [class.hh-file-upload__dropzone--active]="dragActive()"
      role="button"
      tabindex="0"
      [attr.aria-label]="label()"
      (click)="browse()"
      (keydown.enter)="browse()"
      (keydown.space)="onSpace($event)"
      (dragover)="onDragOver($event)"
      (dragleave)="onDragLeave()"
      (drop)="onDrop($event)"
    >
      <span class="hh-file-upload__icon material-icons" aria-hidden="true">upload_file</span>
      <p class="hh-file-upload__hint">{{ hint() }}</p>
      <input
        #fileInput
        type="file"
        class="hh-file-upload__input"
        [multiple]="multiple()"
        [accept]="accept()"
        (change)="onFileInputChange($event)"
      />
    </div>
    @if (files().length) {
      <ul class="hh-file-upload__list">
        @for (file of files(); track file.name + file.size; let i = $index) {
          <li class="hh-file-upload__item">
            <span>{{ file.name }}</span>
            <button
              type="button"
              class="hh-file-upload__remove"
              [attr.aria-label]="removeLabel() + ' ' + file.name"
              (click)="removeFile(i)"
            >
              &times;
            </button>
          </li>
        }
      </ul>
    }
    @if (rejectionMessage()) {
      <p class="hh-file-upload__error" role="alert">{{ rejectionMessage() }}</p>
    }
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-file-upload__dropzone {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: var(--space-2xs);
        padding: var(--space-2xl) var(--space-lg);
        border: 2px dashed var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        color: var(--text-secondary);
        text-align: center;
        cursor: pointer;
      }
      .hh-file-upload__dropzone:focus-visible {
        border-color: var(--color-primary);
        outline: var(--focus-ring-width-strong) solid color-mix(in srgb, var(--color-primary) 20%, transparent);
      }
      .hh-file-upload__dropzone--active {
        border-color: var(--color-primary);
        background: var(--surface-hover);
      }
      .hh-file-upload__icon {
        font-size: var(--font-size-display);
        color: var(--color-primary);
      }
      .hh-file-upload__hint {
        margin: 0;
        font-size: var(--font-size-caption);
      }
      .hh-file-upload__input {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        opacity: 0;
        clip: rect(0 0 0 0);
      }
      .hh-file-upload__list {
        display: grid;
        gap: var(--space-xs);
        margin: var(--space-md) 0 0;
        padding: 0;
        list-style: none;
      }
      .hh-file-upload__item {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-sm);
        padding: var(--space-xs) var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--text-primary);
        font-size: var(--font-size-caption);
      }
      .hh-file-upload__remove {
        display: grid;
        place-items: center;
        width: var(--size-config-nav-indicator);
        height: var(--size-config-nav-indicator);
        border: 0;
        border-radius: var(--radius-full);
        background: transparent;
        color: var(--text-secondary);
        font-size: var(--font-size-input);
        cursor: pointer;
      }
      .hh-file-upload__error {
        margin: var(--space-sm) 0 0;
        color: var(--color-danger, #b91c1c);
        font-size: var(--font-size-caption);
      }
    `,
  ],
})
export class HisHopeFileUploadComponent {
  readonly label = input('Upload files');
  readonly hint = input('Drag files here or click to browse');
  readonly accept = input('');
  readonly multiple = input(false);
  readonly maxSizeBytes = input<number | null>(null);
  readonly removeLabel = input('Remove');
  readonly typeRejectedMessage = input('unsupported file type');
  readonly sizeRejectedMessage = input('file too large');
  readonly filesChange = output<File[]>();
  readonly rejected = output<HisHopeFileUploadRejection[]>();

  @ViewChild('fileInput') private readonly fileInput!: ElementRef<HTMLInputElement>;

  readonly files = signal<File[]>([]);
  readonly dragActive = signal(false);
  private readonly rejections = signal<HisHopeFileUploadRejection[]>([]);

  readonly rejectionMessage = computed(() =>
    this.rejections()
      .map(
        (rejection) =>
          `${rejection.file.name}: ${
            rejection.reason === 'type' ? this.typeRejectedMessage() : this.sizeRejectedMessage()
          }`,
      )
      .join(', '),
  );

  browse(): void {
    this.fileInput.nativeElement.click();
  }

  onSpace(event: Event): void {
    event.preventDefault();
    this.browse();
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragActive.set(true);
  }

  onDragLeave(): void {
    this.dragActive.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragActive.set(false);
    this.handleFiles(Array.from(event.dataTransfer?.files ?? []));
  }

  onFileInputChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.handleFiles(Array.from(input.files ?? []));
    input.value = '';
  }

  removeFile(index: number): void {
    const next = this.files().filter((_, i) => i !== index);
    this.files.set(next);
    this.filesChange.emit(next);
  }

  private handleFiles(candidates: File[]): void {
    const accepted: File[] = [];
    const rejected: HisHopeFileUploadRejection[] = [];
    for (const file of candidates) {
      if (!this.isTypeAccepted(file)) {
        rejected.push({ file, reason: 'type' });
        continue;
      }
      const maxSize = this.maxSizeBytes();
      if (maxSize !== null && file.size > maxSize) {
        rejected.push({ file, reason: 'size' });
        continue;
      }
      accepted.push(file);
    }
    const next = this.multiple() ? [...this.files(), ...accepted] : accepted.slice(0, 1);
    this.files.set(next);
    this.rejections.set(rejected);
    this.filesChange.emit(next);
    if (rejected.length) this.rejected.emit(rejected);
  }

  private isTypeAccepted(file: File): boolean {
    const accept = this.accept().trim();
    if (!accept) return true;
    const patterns = accept.split(',').map((pattern) => pattern.trim().toLowerCase());
    return patterns.some((pattern) => {
      if (pattern.startsWith('.')) return file.name.toLowerCase().endsWith(pattern);
      if (pattern.endsWith('/*')) return file.type.toLowerCase().startsWith(pattern.slice(0, -1));
      return file.type.toLowerCase() === pattern;
    });
  }
}

import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HisHopeFileUploadComponent } from './his-hope-file-upload.component';

@Component({
  standalone: true,
  imports: [HisHopeFileUploadComponent],
  template: `<hh-file-upload accept=".pdf,.png" [maxSizeBytes]="1024" [multiple]="true" />`,
})
class HostComponent {}

function fileList(files: File[]): FileList {
  return {
    length: files.length,
    item: (index: number) => files[index] ?? null,
    [Symbol.iterator]: function* () {
      yield* files;
    },
  } as unknown as FileList;
}

describe('HisHopeFileUploadComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('accepts a file matching the accept pattern and within the size limit', () => {
    const upload = fixture.debugElement.children[0].componentInstance as HisHopeFileUploadComponent;
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type="file"]');
    const file = new File(['x'], 'report.pdf', { type: 'application/pdf' });
    Object.defineProperty(input, 'files', { value: fileList([file]), configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(upload.files().map((f) => f.name)).toEqual(['report.pdf']);
  });

  it('rejects a file with a disallowed extension', () => {
    const upload = fixture.debugElement.children[0].componentInstance as HisHopeFileUploadComponent;
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type="file"]');
    const file = new File(['x'], 'malware.exe', { type: 'application/octet-stream' });
    Object.defineProperty(input, 'files', { value: fileList([file]), configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(upload.files().length).toBe(0);
    expect(fixture.nativeElement.querySelector('.hh-file-upload__error')?.textContent).toContain(
      'unsupported file type',
    );
  });

  it('rejects a file exceeding the max size', () => {
    const upload = fixture.debugElement.children[0].componentInstance as HisHopeFileUploadComponent;
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type="file"]');
    const file = new File(['x'.repeat(2048)], 'big.pdf', { type: 'application/pdf' });
    Object.defineProperty(input, 'files', { value: fileList([file]), configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(upload.files().length).toBe(0);
    expect(fixture.nativeElement.querySelector('.hh-file-upload__error')?.textContent).toContain(
      'file too large',
    );
  });

  it('removes a previously accepted file', () => {
    const upload = fixture.debugElement.children[0].componentInstance as HisHopeFileUploadComponent;
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type="file"]');
    const file = new File(['x'], 'report.pdf', { type: 'application/pdf' });
    Object.defineProperty(input, 'files', { value: fileList([file]), configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    const removeButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '.hh-file-upload__remove',
    );
    removeButton.click();
    fixture.detectChanges();
    expect(upload.files().length).toBe(0);
  });
});

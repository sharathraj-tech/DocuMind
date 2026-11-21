import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { ConfirmationService, MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { Tag } from 'primeng/tag';

import { DocumentDto, DocumentStatus } from '../../core/api-models';
import { openDocument } from '../../core/open-document';
import { formatBytes, problemMessage } from '../../core/utils';
import { DocumentsStore } from './documents.store';

const MAX_UPLOAD_BYTES = 25 * 1024 * 1024; // matches the API limit
const ACCEPTED = ['.pdf', '.docx', '.md', '.txt', '.html', '.htm'];

@Component({
  selector: 'app-documents-page',
  imports: [TableModule, Tag, Button, DatePipe],
  template: `
    <div class="h-full overflow-y-auto p-4 sm:p-6">
      <div class="mx-auto max-w-5xl">
        <div class="mb-5 flex items-end justify-between">
          <div>
            <h1 class="text-2xl font-semibold tracking-tight">Documents</h1>
            <p class="mt-1 text-sm text-surface-500">Upload and manage the sources used by DocuMind.</p>
          </div>
        </div>

        <div
          class="mb-6 flex cursor-pointer flex-col items-center gap-2 rounded-2xl border-2 border-dashed bg-surface-0 p-8 text-center transition"
          [class.border-primary]="dragging()"
          [class.bg-primary-50]="dragging()"
          [class.border-surface-300]="!dragging()"
          role="button"
          tabindex="0"
          aria-label="Upload documents"
          (click)="picker.click()"
          (keydown.enter)="picker.click()"
          (dragover)="onDragOver($event)"
          (dragleave)="dragging.set(false)"
          (drop)="onDrop($event)"
        >
          <i class="pi pi-cloud-upload text-3xl text-surface-500"></i>
          <div class="font-medium">Drop files here, or click to choose</div>
          <div class="text-sm text-surface-500">PDF, DOCX, Markdown, text or HTML, up to 25 MB each</div>
          @if (busy()) {
            <div class="text-sm text-primary"><i class="pi pi-spin pi-spinner"></i> Uploading…</div>
          }
          <input
            #picker
            type="file"
            multiple
            class="hidden"
            [accept]="accepted"
            (change)="onPick($event)"
          />
        </div>

        <p-table [value]="store.docs()" dataKey="id" [tableStyle]="{ 'min-width': '44rem' }">
          <ng-template #header>
            <tr>
              <th>Name</th>
              <th>Size</th>
              <th>Pages</th>
              <th>Status</th>
              <th>Uploaded</th>
              <th class="w-32"></th>
            </tr>
          </ng-template>

          <ng-template #body let-d>
            <tr>
              <td>
                <div class="font-medium break-words">{{ d.fileName }}</div>
                @if (d.error) {
                  <div class="text-xs text-red-600">{{ d.error }}</div>
                }
              </td>
              <td>{{ size(d.sizeBytes) }}</td>
              <td>{{ d.pageCount ?? '–' }}</td>
              <td>
                <p-tag
                  [value]="d.status"
                  [severity]="severity(d.status)"
                  [icon]="d.status === 'Processing' ? 'pi pi-spin pi-spinner' : ''"
                />
              </td>
              <td>{{ d.createdAt | date: 'medium' }}</td>
              <td>
                <div class="flex justify-end gap-1">
                  <p-button
                    icon="pi pi-download"
                    severity="secondary"
                    [text]="true"
                    [rounded]="true"
                    ariaLabel="Open document"
                    (onClick)="open(d)"
                  />
                  <p-button
                    icon="pi pi-refresh"
                    severity="secondary"
                    [text]="true"
                    [rounded]="true"
                    ariaLabel="Reindex document"
                    [disabled]="d.status === 'Pending' || d.status === 'Processing'"
                    (onClick)="reindex(d)"
                  />
                  <p-button
                    icon="pi pi-trash"
                    severity="danger"
                    [text]="true"
                    [rounded]="true"
                    ariaLabel="Delete document"
                    (onClick)="confirmDelete(d)"
                  />
                </div>
              </td>
            </tr>
          </ng-template>

          <ng-template #emptymessage>
            <tr>
              <td colspan="6" class="py-8 text-center text-surface-500">
                {{ store.loaded() ? 'No documents yet. Upload your first file above.' : 'Loading…' }}
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  `,
})
export class DocumentsPage {
  protected readonly store = inject(DocumentsStore);
  private readonly http = inject(HttpClient);
  private readonly messages = inject(MessageService);
  private readonly confirm = inject(ConfirmationService);

  protected readonly accepted = ACCEPTED.join(',');
  protected readonly dragging = signal(false);
  protected readonly busy = signal(false);

  constructor() {
    void this.store.load();
    inject(DestroyRef).onDestroy(() => this.store.stop());
  }

  protected size = formatBytes;

  protected severity(status: DocumentStatus): 'success' | 'info' | 'warn' | 'danger' {
    switch (status) {
      case 'Ready':
        return 'success';
      case 'Processing':
        return 'warn';
      case 'Failed':
        return 'danger';
      default:
        return 'info';
    }
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(true);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    if (event.dataTransfer?.files) void this.upload(event.dataTransfer.files);
  }

  protected onPick(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files) void this.upload(input.files);
    input.value = ''; // allow choosing the same file again
  }

  private async upload(files: FileList): Promise<void> {
    this.busy.set(true);
    let uploaded = 0;

    for (const file of Array.from(files)) {
      const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
      if (!ACCEPTED.includes(extension)) {
        this.fail(file.name, 'Unsupported file type.');
        continue;
      }
      if (file.size > MAX_UPLOAD_BYTES) {
        this.fail(file.name, 'The file is larger than 25 MB.');
        continue;
      }

      try {
        await this.store.upload(file);
        uploaded++;
      } catch (error) {
        this.fail(file.name, problemMessage(error, 'Upload failed.'));
      }
    }

    if (uploaded > 0) {
      this.messages.add({
        severity: 'success',
        summary: 'Uploaded',
        detail: `${uploaded} file${uploaded > 1 ? 's' : ''} uploaded. Processing…`,
      });
    }
    this.busy.set(false);
  }

  protected async open(doc: DocumentDto): Promise<void> {
    try {
      await openDocument(this.http, doc.id, doc.fileName);
    } catch (error) {
      this.fail(doc.fileName, problemMessage(error, 'Could not open the file.'));
    }
  }

  protected async reindex(doc: DocumentDto): Promise<void> {
    try {
      await this.store.reindex(doc.id);
    } catch (error) {
      this.fail(doc.fileName, problemMessage(error, 'Could not reindex.'));
    }
  }

  protected confirmDelete(doc: DocumentDto): void {
    this.confirm.confirm({
      header: 'Delete document',
      message: `Delete "${doc.fileName}"? Its text and embeddings are removed too. Old answers that cited it keep their quotes.`,
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: 'Delete', severity: 'danger' },
      rejectButtonProps: { label: 'Cancel', severity: 'secondary', text: true },
      accept: async () => {
        try {
          await this.store.remove(doc.id);
        } catch (error) {
          this.fail(doc.fileName, problemMessage(error, 'Could not delete.'));
        }
      },
    });
  }

  private fail(name: string, detail: string): void {
    this.messages.add({ severity: 'error', summary: name, detail });
  }
}
import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { DocumentDto } from '../../core/api-models';

const POLL_MS = 2500;

@Injectable({ providedIn: 'root' })
export class DocumentsStore {
  private readonly http = inject(HttpClient);

  private readonly _docs = signal<DocumentDto[]>([]);
  readonly docs = this._docs.asReadonly();
  readonly loaded = signal(false);
  readonly hasActive = computed(() => this._docs().some((d) => d.status === 'Pending' || d.status === 'Processing'));

  private timer: ReturnType<typeof setTimeout> | null = null;

  /** Loads the list, and keeps polling while anything is still being processed. */
  async load(): Promise<void> {
    this.stop();
    try {
      this._docs.set(await firstValueFrom(this.http.get<DocumentDto[]>('/api/documents?take=100')));
      this.loaded.set(true);
    } catch {
      // keep the previous list; a poll will try again
    }
    if (this.hasActive()) this.timer = setTimeout(() => void this.load(), POLL_MS);
  }

  stop(): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = null;
  }

  async upload(file: File): Promise<void> {
    const form = new FormData();
    form.append('file', file); // the field name must be "file"
    await firstValueFrom(this.http.post<DocumentDto>('/api/documents', form));
    await this.load();
  }

  async reindex(id: string): Promise<void> {
    await firstValueFrom(this.http.post(`/api/documents/${id}/reindex`, null));
    await this.load();
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(`/api/documents/${id}`));
    await this.load();
  }
}
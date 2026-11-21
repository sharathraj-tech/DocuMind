import { Component, computed, input, output } from '@angular/core';
import { MarkdownComponent } from 'ngx-markdown';
import { Button } from 'primeng/button';

import { linkCitations } from './citations';
import { UiMessage, UiSource } from './chat.models';

@Component({
  selector: 'app-message-bubble',
  imports: [MarkdownComponent, Button],
  template: `
    @if (message().role === 'User') {
      <div class="flex justify-end">
        <div class="max-w-[85%] whitespace-pre-wrap rounded-2xl rounded-br-md bg-primary px-4 py-3 text-sm leading-6 text-primary-contrast shadow-[0_8px_24px_-14px_rgba(20,184,166,.8)]">
          {{ message().content }}
        </div>
      </div>
    } @else {
      <div class="group flex gap-3">
        <div class="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary ring-1 ring-primary-100">
          <i class="pi pi-sparkles text-sm"></i>
        </div>

        <div class="min-w-0 flex-1 rounded-2xl rounded-tl-md border border-surface-200/70 bg-surface-0 px-4 py-3 shadow-sm">
          @if (message().content === '' && message().streaming) {
            <span class="text-surface-500"><i class="pi pi-spin pi-spinner"></i> Searching your documents…</span>
          } @else {
            <markdown class="chat-md" [data]="rendered()" (click)="onClick($event)" />
          }

          @if (cited().length > 0) {
            <div class="mt-2 flex flex-wrap gap-2" aria-label="Sources">
              @for (source of cited(); track source.index) {
                <button
                  type="button"
                  class="inline-flex items-center gap-1 rounded-full border border-surface-200 bg-surface-0 px-3 py-1 text-xs hover:bg-surface-100"
                  (click)="sourceSelected.emit(source)"
                >
                  <span class="font-semibold text-primary">{{ source.index }}</span>
                  <span class="max-w-48 truncate">{{ source.documentName }}</span>
                  @if (source.pageNumber) {
                    <span class="text-surface-500">p.{{ source.pageNumber }}</span>
                  }
                </button>
              }
            </div>
          }

          @if (message().persisted) {
            <div class="mt-1 flex gap-1">
              <p-button
                icon="pi pi-thumbs-up"
                severity="secondary"
                [text]="true"
                [rounded]="true"
                size="small"
                ariaLabel="Helpful"
                [outlined]="message().helpful === true"
                (onClick)="rated.emit(true)"
              />
              <p-button
                icon="pi pi-thumbs-down"
                severity="secondary"
                [text]="true"
                [rounded]="true"
                size="small"
                ariaLabel="Not helpful"
                [outlined]="message().helpful === false"
                (onClick)="rated.emit(false)"
              />
            </div>
          }
        </div>
      </div>
    }
  `,
})
export class MessageBubble {
  readonly message = input.required<UiMessage>();
  readonly sourceSelected = output<UiSource>();
  readonly rated = output<boolean>();

  protected readonly rendered = computed(() =>
    linkCitations(
      this.message().content,
      this.message().sources.map((s) => s.index),
    ),
  );

  // Only the sources the answer really used are shown as chips under it.
  protected readonly cited = computed(() => {
    const { cited, sources } = this.message();
    return cited.flatMap((index) => {
      const source = sources.find((s) => s.index === index);
      return source ? [source] : [];
    });
  });

  protected onClick(event: MouseEvent): void {
    const link = (event.target as HTMLElement).closest('a');
    const match = /^#cite-(\d+)$/.exec(link?.getAttribute('href') ?? '');
    if (!match) return; // an ordinary link: let the browser handle it

    event.preventDefault();
    const source = this.message().sources.find((s) => s.index === Number(match[1]));
    if (source) this.sourceSelected.emit(source);
  }
}
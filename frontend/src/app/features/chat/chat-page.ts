import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, ElementRef, afterRenderEffect, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Drawer } from 'primeng/drawer';
import { Textarea } from 'primeng/textarea';

import { ConversationSummary } from '../../core/api-models';
import { openDocument } from '../../core/open-document';
import { problemMessage } from '../../core/utils';
import { UiMessage, UiSource } from './chat.models';
import { ChatStore } from './chat.store';
import { MessageBubble } from './message-bubble';

@Component({
  selector: 'app-chat-page',
  host: {
    class: 'block h-full min-h-0 overflow-hidden',
  },
  imports: [Button, Drawer, Textarea, MessageBubble, DecimalPipe],
  template: `
    <div class="flex h-full min-h-0">
      @if (conversationPanelOpen()) {
        <aside class="hidden w-72 shrink-0 flex-col border-r border-surface-200/80 bg-surface-0 lg:flex">
          <div class="flex h-16 items-center justify-between border-b border-surface-200/70 px-4">
            <h2 class="text-sm font-semibold text-surface-700">Conversations</h2>
            <p-button
              icon="pi pi-chevron-left"
              severity="secondary"
              [text]="true"
              [rounded]="true"
              ariaLabel="Hide conversations"
              (onClick)="conversationPanelOpen.set(false)"
            />
          </div>
          <div class="p-3">
            <p-button label="New chat" icon="pi pi-plus" [fluid]="true" (onClick)="newChat()" />
          </div>

          <nav class="flex-1 space-y-1 overflow-y-auto px-2 pb-2" aria-label="Conversations">
            @for (c of store.conversations(); track c.id) {
              <div
                class="group flex cursor-pointer items-center gap-2 rounded-xl px-3 py-2.5 text-surface-600 transition hover:bg-surface-100"
                [class.bg-primary-50]="c.id === store.activeId()"
                [class.text-primary]="c.id === store.activeId()"
                (click)="openConversation(c.id)"
              >
                <span class="flex-1 truncate text-sm">{{ c.title }}</span>
                <button
                  type="button"
                  class="flex h-7 w-7 items-center justify-center rounded-md text-surface-400 opacity-0 hover:bg-red-50 hover:text-red-600 focus:opacity-100 group-hover:opacity-100"
                  aria-label="Delete conversation"
                  (click)="confirmDelete(c, $event)"
                >
                  <i class="pi pi-trash"></i>
                </button>
              </div>
            } @empty {
              <p class="px-2 py-4 text-sm text-surface-500">No conversations yet.</p>
            }
          </nav>
        </aside>
      }

      <section class="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <header class="flex h-16 shrink-0 items-center justify-between border-b border-surface-200/70 bg-surface-0/95 px-4 backdrop-blur sm:px-6">
          <div class="flex min-w-0 items-center gap-2">
            <p-button
              icon="pi pi-bars"
              severity="secondary"
              [text]="true"
              [rounded]="true"
              ariaLabel="Toggle conversations"
              styleClass="hidden lg:inline-flex"
              (onClick)="conversationPanelOpen.update((open) => !open)"
            />
            <p-button
              icon="pi pi-bars"
              severity="secondary"
              [text]="true"
              [rounded]="true"
              ariaLabel="Open conversations"
              styleClass="lg:hidden"
              (onClick)="conversationDrawerOpen.set(true)"
            />
            <div class="min-w-0">
              <div class="truncate text-sm font-semibold text-surface-800">New conversation</div>
              <div class="mt-0.5 flex items-center gap-1.5 text-xs text-surface-500">
                <span class="h-1.5 w-1.5 rounded-full bg-green-500"></span>
                Ready
              </div>
            </div>
          </div>
          <p-button
            icon="pi pi-plus"
            label="New chat"
            severity="secondary"
            [outlined]="true"
            size="small"
            styleClass="lg:hidden"
            (onClick)="newChat()"
          />
        </header>

        <div #scroller class="min-h-0 flex-1 overflow-y-auto bg-surface-50/60 px-4 py-6 sm:px-6">
          <div class="mx-auto flex max-w-3xl flex-col gap-5">
            @if (store.messages().length === 0 && !store.loadingConversation()) {
              <div class="mx-auto mt-[min(12vh,5rem)] max-w-2xl text-center">
                <span class="mx-auto mb-5 flex h-16 w-16 items-center justify-center rounded-2xl bg-primary-50 text-primary shadow-sm ring-1 ring-primary-100">
                  <i class="pi pi-sparkles text-2xl"></i>
                </span>
                <h1 class="text-2xl font-semibold tracking-tight text-surface-800">What would you like to know?</h1>
                <p class="mx-auto mt-2 max-w-md text-sm leading-6 text-surface-500">
                  Search across your uploaded documents and get answers with clear sources.
                </p>
                <div class="mt-7 grid gap-2 sm:grid-cols-3">
                  @for (prompt of suggestedPrompts; track prompt) {
                    <button
                      type="button"
                      class="rounded-xl border border-surface-200 bg-surface-0 px-3 py-3 text-left text-sm text-surface-600 shadow-sm transition hover:-translate-y-0.5 hover:border-primary-200 hover:text-primary hover:shadow-md"
                      (click)="draft.set(prompt)"
                    >
                      {{ prompt }}
                    </button>
                  }
                </div>
              </div>
            }

            @for (message of store.messages(); track message.key) {
              <app-message-bubble
                [message]="message"
                (sourceSelected)="selectedSource.set($event)"
                (rated)="store.rate(message, $event)"
              />
            }
          </div>
        </div>

        @if (store.error(); as error) {
          <div class="mx-auto w-full max-w-3xl px-4" role="alert">
            <div class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{{ error }}</div>
          </div>
        }

        <form class="sticky bottom-0 z-10 shrink-0 border-t border-surface-200/70 bg-surface-0/95 px-3 py-3 backdrop-blur sm:px-5 sm:py-4" (submit)="submit($event)">
          <div class="mx-auto flex max-w-3xl items-end gap-2 rounded-2xl border border-surface-200 bg-surface-0 p-2 shadow-[0_8px_30px_-18px_rgba(15,23,42,.35)] focus-within:border-primary-300 focus-within:ring-2 focus-within:ring-primary-100">
            <textarea
              pTextarea
              rows="2"
              class="!border-0 !shadow-none !bg-transparent w-full resize-none px-2 py-2"
              placeholder="Ask about your documents..."
              aria-label="Your question"
              [value]="draft()"
              (input)="draft.set($any($event.target).value)"
              (keydown.enter)="onEnter($any($event))"
            ></textarea>

            @if (store.streaming()) {
              <p-button label="Stop" icon="pi pi-stop-circle" severity="secondary" (onClick)="store.stop()" />
            } @else {
              <p-button
                type="submit"
                icon="pi pi-arrow-up"
                ariaLabel="Send"
                [rounded]="true"
                [disabled]="draft().trim() === ''"
              />
            }
          </div>
          <div class="mx-auto mt-2 max-w-3xl text-center text-[11px] text-surface-400">
            Enter to send · Shift+Enter for a new line
          </div>
        </form>
      </section>
    </div>

    <p-drawer
      [visible]="selectedSource() !== null"
      (onHide)="selectedSource.set(null)"
      position="right"
      header="Source"
      styleClass="!w-full md:!w-[28rem]"
    >
      @if (selectedSource(); as source) {
        <div class="space-y-4">
          <div>
            <div class="text-xs uppercase tracking-wide text-surface-500">Document</div>
            <div class="break-words font-medium">{{ source.documentName }}</div>
            <div class="text-sm text-surface-500">
              @if (source.pageNumber) {
                Page {{ source.pageNumber }} ·
              }
              Relevance {{ source.score | number: '1.2-2' }}
            </div>
          </div>

          <blockquote class="whitespace-pre-wrap rounded-lg border-l-4 border-primary bg-surface-100 p-3 text-sm">
            {{ source.snippet }}
          </blockquote>

          <p-button
            label="Open document"
            icon="pi pi-external-link"
            [disabled]="!source.documentId"
            (onClick)="openSource(source)"
          />
        </div>
      }
    </p-drawer>

    <p-drawer
      [visible]="conversationDrawerOpen()"
      (onHide)="conversationDrawerOpen.set(false)"
      position="left"
      header="Conversations"
      styleClass="!w-[18rem]"
    >
      <div class="mb-3">
        <p-button label="New chat" icon="pi pi-plus" [fluid]="true" (onClick)="newChat(); conversationDrawerOpen.set(false)" />
      </div>
      <nav class="space-y-1" aria-label="Mobile conversations">
        @for (c of store.conversations(); track c.id) {
          <button
            type="button"
            class="flex w-full items-center rounded-xl px-3 py-3 text-left text-sm text-surface-600 hover:bg-surface-100"
            [class.bg-primary-50]="c.id === store.activeId()"
            [class.text-primary]="c.id === store.activeId()"
            (click)="openConversation(c.id); conversationDrawerOpen.set(false)"
          >
            <span class="truncate">{{ c.title }}</span>
          </button>
        } @empty {
          <p class="py-4 text-sm text-surface-500">No conversations yet.</p>
        }
      </nav>
    </p-drawer>
  `,
})
export class ChatPage {
  protected readonly store = inject(ChatStore);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);
  private readonly confirm = inject(ConfirmationService);

  protected readonly draft = signal('');
  protected readonly selectedSource = signal<UiSource | null>(null);
  protected readonly conversationPanelOpen = signal(true);
  protected readonly conversationDrawerOpen = signal(false);
  protected readonly suggestedPrompts = [
    'Summarize the key findings',
    'What are the most important topics?',
    'Compare the uploaded documents',
  ];

  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');
  private readonly routeId = toSignal(inject(ActivatedRoute).paramMap.pipe(map((p) => p.get('id'))), {
    initialValue: null,
  });

  constructor() {
    void this.store.loadConversations();

    // The URL is the source of truth when it names a conversation. If the store already holds that
    // conversation (we just created it while streaming), leave it alone.
    effect(() => {
      const id = this.routeId();
      untracked(() => {
        if (id) {
          if (id !== this.store.activeId()) void this.store.open(id);
        } else if (!this.store.streaming()) {
          this.store.newChat();
        }
      });
    });

    afterRenderEffect(() => {
      this.store.messages();
      const element = this.scroller()?.nativeElement;
      if (element) element.scrollTop = element.scrollHeight;
    });
  }

  protected submit(event: Event): void {
    event.preventDefault();
    const text = this.draft();
    if (text.trim() === '' || this.store.streaming()) return;

    this.draft.set('');
    void this.store.send(text);
  }

  protected onEnter(event: KeyboardEvent): void {
    if (event.shiftKey) return; // Shift+Enter inserts a new line
    event.preventDefault();
    this.submit(event);
  }

  protected newChat(): void {
    this.store.newChat();
    void this.router.navigate(['/chat']);
  }

  protected openConversation(id: string): void {
    void this.router.navigate(['/chat', id]);
  }

  protected confirmDelete(conversation: ConversationSummary, event: Event): void {
    event.stopPropagation(); // don't also open the conversation
    this.confirm.confirm({
      header: 'Delete conversation',
      message: `Delete "${conversation.title}"? This can't be undone.`,
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: 'Delete', severity: 'danger' },
      rejectButtonProps: { label: 'Cancel', severity: 'secondary', text: true },
      accept: async () => {
        try {
          await this.store.remove(conversation.id);
        } catch (error) {
          this.messages.add({ severity: 'error', summary: 'Delete failed', detail: problemMessage(error) });
        }
      },
    });
  }

  protected async openSource(source: UiSource): Promise<void> {
    if (!source.documentId) return;
    try {
      await openDocument(this.http, source.documentId, source.documentName, source.pageNumber);
    } catch {
      this.messages.add({
        severity: 'error',
        summary: source.documentName,
        detail: 'Could not open the document. It may have been deleted.',
      });
    }
  }
}
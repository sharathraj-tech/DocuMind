import { Location } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ConversationDetail, ConversationSummary, MessageDto } from '../../core/api-models';
import { AuthService } from '../../core/auth/auth.service';
import { problemMessage } from '../../core/utils';
import { StreamHttpError, streamChat } from './chat-stream';
import { UiMessage, UiSource } from './chat.models';

const NO_ANSWER = "I couldn't find that in your documents.";

interface SourcePayload {
  index: number;
  documentId: string;
  documentName: string;
  pageNumber: number | null;
  snippet: string;
  score: number;
}

@Injectable({ providedIn: 'root' })
export class ChatStore {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly location = inject(Location);

  private readonly _conversations = signal<ConversationSummary[]>([]);
  private readonly _messages = signal<UiMessage[]>([]);
  private readonly _activeId = signal<string | null>(null);

  readonly conversations = this._conversations.asReadonly();
  readonly messages = this._messages.asReadonly();
  readonly activeId = this._activeId.asReadonly();
  readonly streaming = signal(false);
  readonly loadingConversation = signal(false);
  readonly error = signal<string | null>(null);

  private abort: AbortController | null = null;

  async loadConversations(): Promise<void> {
    try {
      this._conversations.set(await firstValueFrom(this.http.get<ConversationSummary[]>('/api/conversations')));
    } catch {
      // the sidebar is optional; chat still works
    }
  }

  async open(id: string): Promise<void> {
    if (this.streaming()) return;

    this._activeId.set(id);
    this.error.set(null);
    this.loadingConversation.set(true);
    try {
      const detail = await firstValueFrom(this.http.get<ConversationDetail>(`/api/conversations/${id}`));
      this._messages.set(detail.messages.filter((m) => m.role !== 'System').map(toUiMessage));
    } catch (error) {
      const message =
        error instanceof HttpErrorResponse && error.status === 404
          ? 'That conversation no longer exists.'
          : problemMessage(error);
      this.newChat();
      this.error.set(message);
    } finally {
      this.loadingConversation.set(false);
    }
  }

  newChat(): void {
    if (this.streaming()) this.stop();
    this._activeId.set(null);
    this._messages.set([]);
    this.error.set(null);
    this.location.replaceState('/chat');
  }

  async send(text: string): Promise<void> {
    const message = text.trim();
    if (!message || this.streaming()) return;

    const assistantKey = crypto.randomUUID();
    this._messages.update((list) => [
      ...list,
      blank(crypto.randomUUID(), 'User', message),
      { ...blank(assistantKey, 'Assistant', ''), streaming: true },
    ]);

    this.error.set(null);
    this.streaming.set(true);
    this.abort = new AbortController();

    try {
      await this.runStream({ conversationId: this._activeId(), message }, assistantKey);
    } catch (error) {
      if (!this.abort?.signal.aborted) {
        this.error.set(
          error instanceof StreamHttpError ? error.message : 'The connection was interrupted. Please try again.',
        );
      }
    } finally {
      this.abort = null;
      this.streaming.set(false);
      this._messages.update((list) =>
        list
          .filter((m) => !(m.role === 'Assistant' && m.content === '')) // drop an answer that never started
          .map((m) => (m.streaming ? { ...m, streaming: false } : m)),
      );
      void this.loadConversations(); // new title, new ordering
    }
  }

  stop(): void {
    this.abort?.abort();
  }

  async rate(message: UiMessage, helpful: boolean): Promise<void> {
    if (!message.persisted) return;

    const previous = message.helpful;
    this.patch(message.key, (m) => ({ ...m, helpful }));
    try {
      await firstValueFrom(
        this.http.post(`/api/messages/${message.id}/feedback`, { isHelpful: helpful, comment: null }),
      );
    } catch {
      this.patch(message.key, (m) => ({ ...m, helpful: previous }));
      this.error.set('Could not save your feedback.');
    }
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(`/api/conversations/${id}`));
    this._conversations.update((list) => list.filter((c) => c.id !== id));
    if (this._activeId() === id) this.newChat();
  }

  // One retry if the access token was rejected mid-flight.
  private async runStream(body: { conversationId: string | null; message: string }, assistantKey: string): Promise<void> {
    for (let attempt = 0; attempt < 2; attempt++) {
      const token = await this.auth.getAccessToken();
      if (!token) {
        this.auth.endSession();
        return;
      }

      try {
        await streamChat(body, token, this.abort!.signal, (name, data) => this.apply(name, data, assistantKey));
        return;
      } catch (error) {
        const expired = error instanceof StreamHttpError && error.status === 401 && attempt === 0;
        if (expired && (await this.auth.refresh())) continue;
        throw error;
      }
    }
  }

  private apply(name: string, data: unknown, key: string): void {
    switch (name) {
      case 'conversation': {
        const { conversationId } = data as { conversationId: string };
        if (!this._activeId()) {
          this._activeId.set(conversationId);
          // Update the address bar without a router navigation, which would re-create the page mid-stream.
          this.location.replaceState(`/chat/${conversationId}`);
        }
        break;
      }
      case 'sources': {
        const { sources } = data as { sources: SourcePayload[] };
        this.patch(key, (m) => ({ ...m, sources: sources.map(toUiSource) }));
        break;
      }
      case 'token': {
        const { text } = data as { text: string };
        this.patch(key, (m) => ({ ...m, content: m.content + text }));
        break;
      }
      case 'done': {
        const done = data as { messageId: string; answered: boolean; citations: { index: number }[] };
        this.patch(key, (m) => ({
          ...m,
          id: done.messageId,
          streaming: false,
          persisted: true,
          answered: done.answered,
          cited: done.citations.map((c) => c.index),
        }));
        break;
      }
      case 'error': {
        this.error.set((data as { message: string }).message);
        break;
      }
    }
  }

  private patch(key: string, change: (message: UiMessage) => UiMessage): void {
    this._messages.update((list) => list.map((m) => (m.key === key ? change(m) : m)));
  }
}

function blank(key: string, role: 'User' | 'Assistant', content: string): UiMessage {
  return {
    key,
    id: key,
    role,
    content,
    sources: [],
    cited: [],
    streaming: false,
    answered: true,
    persisted: false,
    helpful: null,
  };
}

function toUiSource(s: SourcePayload): UiSource {
  return {
    index: s.index,
    documentId: s.documentId,
    documentName: s.documentName,
    pageNumber: s.pageNumber,
    snippet: s.snippet,
    score: s.score,
  };
}

// Saved conversations only keep the sources an answer cited, which is all the UI needs to show.
function toUiMessage(m: MessageDto): UiMessage {
  const sources: UiSource[] = m.citations.map((c) => ({
    index: c.sourceIndex,
    documentId: c.documentId,
    documentName: c.documentName,
    pageNumber: c.pageNumber,
    snippet: c.snippet,
    score: c.score,
  }));

  return {
    key: m.id,
    id: m.id,
    role: m.role === 'User' ? 'User' : 'Assistant',
    content: m.content,
    sources,
    cited: sources.map((s) => s.index),
    streaming: false,
    answered: !m.content.startsWith(NO_ANSWER),
    persisted: true,
    helpful: null,
  };
}
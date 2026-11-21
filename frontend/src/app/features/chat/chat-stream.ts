import { fetchEventSource } from '@microsoft/fetch-event-source';

export class StreamHttpError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

// POST + SSE. The browser's EventSource can't send a body or an Authorization header, so we use fetch.
export async function streamChat(
  body: { conversationId: string | null; message: string },
  token: string,
  signal: AbortSignal,
  onEvent: (name: string, data: unknown) => void,
): Promise<void> {
  await fetchEventSource('/api/chat/stream', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(body),
    signal,

    // Without this the library re-sends the request whenever the tab comes back from the background,
    // which would ask the same question twice.
    openWhenHidden: true,

    async onopen(response) {
      if (response.ok && (response.headers.get('content-type') ?? '').startsWith('text/event-stream')) return;

      let detail = '';
      try {
        detail = ((await response.json()) as { title?: string }).title ?? '';
      } catch {
        // not JSON
      }
      throw new StreamHttpError(response.status, detail || `Request failed (${response.status}).`);
    },

    onmessage(event) {
      if (event.event && event.data) onEvent(event.event, JSON.parse(event.data));
    },

    // Never retry automatically: a retry would send the question again.
    onerror(error) {
      throw error;
    },
  });
}
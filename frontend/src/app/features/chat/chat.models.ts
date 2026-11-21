// A passage the model was shown. The [n] markers in an answer refer to `index`.
export interface UiSource {
  index: number;
  documentId: string | null;
  documentName: string;
  pageNumber: number | null;
  snippet: string;
  score: number;
}

export interface UiMessage {
  key: string; // stable for the lifetime of the message, so the list doesn't re-create it when the server id arrives
  id: string; // server id once known
  role: 'User' | 'Assistant';
  content: string;
  sources: UiSource[]; // everything [n] can point to
  cited: number[]; // the source indexes the answer actually used
  streaming: boolean;
  answered: boolean;
  persisted: boolean; // saved on the server (feedback needs this)
  helpful: boolean | null;
}
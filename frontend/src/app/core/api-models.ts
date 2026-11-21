export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string; // ISO-8601, UTC
  tenantId: string;
  role: string;
}

export interface CurrentUser {
  userId: string;
  email: string | null;
  tenantId: string;
  role: string;
}

export type DocumentStatus = 'Pending' | 'Processing' | 'Ready' | 'Failed';

export interface DocumentDto {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: DocumentStatus;
  pageCount: number | null;
  error: string | null;
  createdAt: string;
}

export interface ConversationSummary {
  id: string;
  title: string;
  createdAt: string;
  lastActivityAt: string;
}

export interface CitationDto {
  sourceIndex: number;
  chunkId: string | null;
  documentId: string | null;
  documentName: string;
  pageNumber: number | null;
  snippet: string;
  score: number;
}

export interface MessageDto {
  id: string;
  role: 'User' | 'Assistant' | 'System';
  content: string;
  createdAt: string;
  model: string | null;
  citations: CitationDto[];
}

export interface ConversationDetail {
  id: string;
  title: string;
  messages: MessageDto[];
}
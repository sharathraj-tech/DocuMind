import { HttpErrorResponse } from '@angular/common/http';

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;

  const units = ['KB', 'MB', 'GB'];
  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${value.toFixed(value < 10 ? 1 : 0)} ${units[unit]}`;
}

// The API returns RFC 7807 problem details; the human-readable part is "title".
export function problemMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (error instanceof HttpErrorResponse) {
    const title = (error.error as { title?: string } | null)?.title;
    if (title) return title;
    if (error.status === 0) return 'Cannot reach the server. Is the API running?';
  }
  return fallback;
}

// Only follow same-site paths after login (guards against open redirects).
export function safeReturnUrl(url: string | null): string {
  return url && url.startsWith('/') && !url.startsWith('//') ? url : '/';
}
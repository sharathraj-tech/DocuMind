import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

// PDFs open in a new tab at the cited page. Everything else is downloaded, never opened:
// a blob URL runs in our origin, so uploaded HTML must not be rendered from one.
export async function openDocument(
  http: HttpClient,
  documentId: string,
  fileName: string,
  page?: number | null,
): Promise<void> {
  const blob = await firstValueFrom(http.get(`/api/documents/${documentId}/file`, { responseType: 'blob' }));

  if (fileName.toLowerCase().endsWith('.pdf')) {
    const url = URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }));
    window.open(page ? `${url}#page=${page}` : url, '_blank', 'noopener');
    setTimeout(() => URL.revokeObjectURL(url), 60_000);
    return;
  }

  const url = URL.createObjectURL(new Blob([blob], { type: 'application/octet-stream' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
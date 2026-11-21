// Turns [1] / [1, 2] markers into markdown links ([1](#cite-1)) so the renderer makes them clickable.
// Markers that don't match a known source are left alone.
export function linkCitations(text: string, available: readonly number[]): string {
  if (available.length === 0) return text;
  const known = new Set(available);

  return text.replace(/\[(\d+(?:\s*,\s*\d+)*)\]/g, (whole, group: string) => {
    const numbers = group.split(',').map((n) => Number(n.trim()));
    if (!numbers.every((n) => known.has(n))) return whole;
    return numbers.map((n) => `[${n}](#cite-${n})`).join('');
  });
}
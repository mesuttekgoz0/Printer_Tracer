const nf = new Intl.NumberFormat("tr-TR");
const mf = new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const uf = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 4 });

/** Tam sayı, tr-TR gruplu. */
export const n = (v: number | null | undefined): string => (v == null ? "–" : nf.format(v));

/** Para: 2 hane. */
export const money = (v: number | null | undefined): string => (v == null ? "–" : mf.format(v));

/** Birim fiyat: 4 haneye kadar. */
export const unit = (v: number | null | undefined): string => (v == null ? "–" : uf.format(v));

/** ISO tarih-saat → "dd.MM.yyyy HH:mm" (yerel). */
export function dt(iso: string | null | undefined): string {
  if (!iso) return "–";
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  const p = (x: number) => String(x).padStart(2, "0");
  return `${p(d.getDate())}.${p(d.getMonth() + 1)}.${d.getFullYear()} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

/** ISO/DateOnly → "dd.MM.yyyy". */
export function d(iso: string | null | undefined): string {
  if (!iso) return "–";
  const x = new Date(iso.length <= 10 ? iso + "T00:00:00" : iso);
  if (isNaN(x.getTime())) return iso;
  const p = (v: number) => String(v).padStart(2, "0");
  return `${p(x.getDate())}.${p(x.getMonth() + 1)}.${x.getFullYear()}`;
}

/** Date → "yyyy-MM-dd" (input[type=date] value). */
export function iso(date: Date): string {
  const p = (v: number) => String(v).padStart(2, "0");
  return `${date.getFullYear()}-${p(date.getMonth() + 1)}-${p(date.getDate())}`;
}

/** "0,15" veya "0.15" → number (geçersizse 0). */
export function parseFiyat(s: string): number {
  const v = parseFloat(s.trim().replace(",", "."));
  return isFinite(v) && v >= 0 ? v : 0;
}

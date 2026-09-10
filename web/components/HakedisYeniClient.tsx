"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { HakedisTaslak, TaslakRow, TedarikciSec } from "@/lib/types";
import { dt, money, n, unit } from "@/lib/format";

export function HakedisYeniClient() {
  const router = useRouter();
  const [suppliers, setSuppliers] = useState<TedarikciSec[]>([]);
  const [pick, setPick] = useState("");
  const [taslak, setTaslak] = useState<HakedisTaslak | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api
      .get<TedarikciSec[]>("/api/hakedisler/tedarikci-secenekleri")
      .then(setSuppliers)
      .catch((e) => setErr(e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı."));
  }, []);

  async function devam(e: React.FormEvent) {
    e.preventDefault();
    setErr(null);
    setBusy(true);
    try {
      setTaslak(await api.post<HakedisTaslak>("/api/hakedisler/taslak", { tedarikciId: Number(pick) }));
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Taslak hazırlanamadı.");
    } finally {
      setBusy(false);
    }
  }

  if (taslak) {
    return <Review taslak={taslak} onCancel={() => setTaslak(null)} onSaved={(id) => router.push(`/hakedisler/${id}`)} />;
  }

  return (
    <>
      <div className="page-head spread">
        <div>
          <h1>Yeni Hakediş</h1>
          <p className="muted small">Tedarikçi seçin; o tedarikçinin tüm yazıcıları için hakediş taslağı hazırlanır.</p>
        </div>
        <Link className="btn btn-sm" href="/hakedisler">← Hakedişler</Link>
      </div>

      {err && <div className="alert alert-err" style={{ marginBottom: "var(--space-4)" }}>{err}</div>}

      {suppliers.length === 0 ? (
        <div className="alert" style={{ background: "var(--color-neutral-100)" }}>
          Henüz tedarikçi yok. <Link href="/tedarikciler">Tedarikçiler</Link> sayfasından ekleyin, sonra yazıcılara tedarikçi atayın.
        </div>
      ) : (
        <div className="card" style={{ maxWidth: "34rem" }}>
          <div className="card-body">
            <form className="stack" onSubmit={devam}>
              <div className="field">
                <label htmlFor="h-ted">Tedarikçi</label>
                <select id="h-ted" className="select" value={pick} onChange={(e) => setPick(e.target.value)} required>
                  <option value="" disabled>— seçin —</option>
                  {suppliers.map((s) => (
                    <option key={s.id} value={s.id} disabled={s.printerCount === 0}>
                      {s.ad} ({s.printerCount} yazıcı){s.hasPriceList ? "" : " — fiyat listesi yok"}
                    </option>
                  ))}
                </select>
                <span className="muted small">Yazıcısı olmayan tedarikçiler seçilemez.</span>
              </div>
              <button type="submit" className="btn btn-primary" disabled={!pick || busy}>
                {busy ? "Hazırlanıyor…" : "Devam"}
              </button>
            </form>
          </div>
        </div>
      )}
    </>
  );
}

interface EditRow extends TaslakRow {
  prev: string;
  cur: string;
}

function Review({ taslak, onCancel, onSaved }: { taslak: HakedisTaslak; onCancel: () => void; onSaved: (id: number) => void }) {
  const [rows, setRows] = useState<EditRow[]>(
    taslak.rows.map((r) => ({ ...r, prev: String(r.previousCounter), cur: String(r.currentCounter) })),
  );
  const [periodStart, setPeriodStart] = useState(taslak.periodStart.slice(0, 10));
  const [periodEnd, setPeriodEnd] = useState(taslak.periodEnd.slice(0, 10));
  const [note, setNote] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const calc = useMemo(
    () =>
      rows.map((r) => {
        const p = parseInt(r.prev || "0", 10);
        const c = parseInt(r.cur || "0", 10);
        const pages = !isNaN(p) && !isNaN(c) && c >= p ? c - p : null;
        return { pages, amount: (pages ?? 0) * r.unitPrice };
      }),
    [rows],
  );
  const totalPages = calc.reduce((s, x) => s + (x.pages ?? 0), 0);
  const totalAmount = calc.reduce((s, x) => s + x.amount, 0);

  async function save() {
    setBusy(true);
    setErr(null);
    try {
      const created = await api.post<{ id: number; number: string }>("/api/hakedisler", {
        tedarikciId: taslak.tedarikciId,
        tedarikciAd: taslak.tedarikciAd,
        periodStart,
        periodEnd,
        note,
        rows: rows.map((r) => ({
          printerId: r.printerId,
          printerName: r.printerName,
          model: r.model,
          turId: r.turId,
          previousCounter: parseInt(r.prev || "0", 10) || 0,
          currentCounter: parseInt(r.cur || "0", 10) || 0,
          unitPrice: r.unitPrice,
        })),
      });
      onSaved(created.id);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Kaydedilemedi.");
      setBusy(false);
    }
  }

  return (
    <>
      <div className="page-head spread">
        <div>
          <h1>Hakediş Oluştur</h1>
          <p className="muted small">
            Tedarikçi: <strong>{taslak.tedarikciAd}</strong>
          </p>
        </div>
        <button className="btn btn-sm" onClick={onCancel}>Vazgeç</button>
      </div>

      {taslak.skipped.length > 0 && (
        <div className="alert" style={{ background: "var(--color-accent-100)", borderColor: "var(--color-accent-300)", color: "var(--color-accent-800)", marginBottom: "var(--space-4)" }}>
          Okuması olmadığı için eklenmeyen yazıcı(lar): <strong>{taslak.skipped.join(", ")}</strong>.
        </div>
      )}
      {taslak.warnings.length > 0 && (
        <div className="alert" style={{ background: "var(--color-accent-100)", borderColor: "var(--color-accent-300)", color: "var(--color-accent-800)", marginBottom: "var(--space-4)" }}>
          <ul style={{ margin: 0, paddingLeft: "1.1rem" }}>
            {taslak.warnings.map((w, i) => <li key={i}>{w}</li>)}
          </ul>
        </div>
      )}
      {err && <div className="alert alert-err" style={{ marginBottom: "var(--space-4)" }}>{err}</div>}

      <div className="card" style={{ marginBottom: "var(--space-4)" }}>
        <div className="card-body form-grid">
          <div className="field">
            <label>Dönem başlangıcı</label>
            <input className="input" type="date" value={periodStart} onChange={(e) => setPeriodStart(e.target.value)} />
          </div>
          <div className="field">
            <label>Dönem bitişi</label>
            <input className="input" type="date" value={periodEnd} onChange={(e) => setPeriodEnd(e.target.value)} />
          </div>
          <div className="field" style={{ gridColumn: "span 2" }}>
            <label>Açıklama</label>
            <input className="input" value={note} maxLength={500} onChange={(e) => setNote(e.target.value)} placeholder={`ör. Ocak 2026 – ${taslak.tedarikciAd} hakedişi`} />
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-head">Yazıcılar</div>
        <div className="card-body p0">
          <div style={{ overflowX: "auto" }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Yazıcı</th>
                  <th>Marka / Model</th>
                  <th>Tür</th>
                  <th className="num">Önceki sayaç</th>
                  <th className="num">Şimdiki sayaç</th>
                  <th className="num">Fark</th>
                  <th className="num">Sayfa başı ₺</th>
                  <th className="num">Tutar ₺</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={r.printerId}>
                    <td>
                      <strong>{r.printerName}</strong>
                      {r.firstHakedis && (
                        <div className="small" style={{ color: "var(--color-accent-800)" }}>
                          ilk hakediş — önceki sayaç ilk okumadan ({dt(r.previousReadingUtc)})
                        </div>
                      )}
                    </td>
                    <td className="muted small">{r.model ?? "–"}</td>
                    <td><span className="tag">{r.turAd}</span></td>
                    <td className="num">
                      <input
                        className="input select-sm num"
                        style={{ textAlign: "right", width: "7rem" }}
                        value={r.prev}
                        onChange={(e) => setRows((s) => s.map((x, j) => (j === i ? { ...x, prev: e.target.value } : x)))}
                      />
                    </td>
                    <td className="num">
                      <input
                        className="input select-sm num"
                        style={{ textAlign: "right", width: "7rem" }}
                        value={r.cur}
                        onChange={(e) => setRows((s) => s.map((x, j) => (j === i ? { ...x, cur: e.target.value } : x)))}
                      />
                    </td>
                    <td className="num"><strong>{calc[i].pages == null ? "?" : n(calc[i].pages)}</strong></td>
                    <td className="num">{unit(r.unitPrice)}</td>
                    <td className="num"><strong>{money(calc[i].amount)}</strong></td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <th colSpan={5} style={{ textAlign: "right" }}>Toplam</th>
                  <th className="num">{n(totalPages)}</th>
                  <th />
                  <th className="num">{money(totalAmount)}</th>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
        <div className="card-body" style={{ display: "flex", justifyContent: "flex-end", gap: "var(--space-2)", borderTop: "1px solid var(--color-divider)" }}>
          <button className="btn" onClick={onCancel}>Vazgeç</button>
          <button className="btn btn-primary" onClick={save} disabled={busy}>
            {busy ? "Kaydediliyor…" : "Hakedişi kaydet"}
          </button>
        </div>
      </div>
    </>
  );
}

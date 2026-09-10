"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { api, API_BASE, ApiError } from "@/lib/api";
import type { HakedisDetail } from "@/lib/types";
import { d, dt, money, n, unit } from "@/lib/format";

export function HakedisDetailClient({ id }: { id: number }) {
  const router = useRouter();
  const [data, setData] = useState<HakedisDetail | null>(null);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    api
      .get<HakedisDetail>(`/api/hakedisler/${id}`)
      .then(setData)
      .catch((e) => setErr(e instanceof ApiError ? e.message : "Bulunamadı."));
  }, [id]);

  async function remove() {
    if (!data || !confirm(`Hakediş ${data.number} silinsin mi? Bu belge tamamen silinir.`)) return;
    try {
      await api.del(`/api/hakedisler/${id}`);
      router.push("/hakedisler");
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Silinemedi.");
    }
  }

  if (err) return <div className="alert alert-err" style={{ marginTop: "var(--space-6)" }}>{err}</div>;
  if (!data) return <p className="empty">Yükleniyor…</p>;

  return (
    <>
      <div className="spread no-print" style={{ padding: "var(--space-6) 0 var(--space-3)" }}>
        <Link className="btn btn-sm" href="/hakedisler">← Hakedişler</Link>
        <div className="row-actions">
          <a className="btn btn-sm" href={`${API_BASE}/api/hakedisler/${id}/csv`}>CSV indir</a>
          <button className="btn btn-sm btn-primary" onClick={() => window.print()}>Yazdır / PDF</button>
          <button className="btn btn-sm btn-danger" onClick={remove}>Sil</button>
        </div>
      </div>

      <div className="card hakedis-doc">
        <div className="card-body">
          <div className="spread" style={{ marginBottom: "var(--space-4)", alignItems: "flex-start" }}>
            <div>
              <h1 style={{ letterSpacing: "0.05em", marginBottom: 2 }}>HAKEDİŞ</h1>
              <div className="muted">No: <strong>{data.number}</strong></div>
            </div>
            <div className="small" style={{ textAlign: "right" }}>
              <div>Düzenleme: <strong>{dt(data.createdUtc)}</strong></div>
              <div>Dönem: <strong>{d(data.periodStart)} – {d(data.periodEnd)}</strong></div>
            </div>
          </div>

          {data.tedarikciAd && (
            <div style={{ marginBottom: "var(--space-4)", paddingBottom: "var(--space-2)", borderBottom: "1px solid var(--color-divider)" }}>
              <span className="muted small" style={{ display: "block" }}>Tedarikçi</span>
              <span style={{ fontFamily: "var(--font-heading)", fontSize: "1.4rem", fontWeight: 600 }}>{data.tedarikciAd}</span>
            </div>
          )}

          {(data.fromCompany || data.toCompany) && (
            <div style={{ display: "flex", gap: "var(--space-6)", marginBottom: "var(--space-3)" }} className="small">
              {data.fromCompany && <div><div className="muted">Düzenleyen</div><strong>{data.fromCompany}</strong></div>}
              {data.toCompany && <div><div className="muted">Gönderilen</div><strong>{data.toCompany}</strong></div>}
            </div>
          )}

          {data.note && <p><span className="muted">Açıklama:</span> {data.note}</p>}

          <div style={{ overflowX: "auto" }}>
            <table className="table">
              <thead>
                <tr>
                  <th className="num" style={{ width: "2rem" }}>#</th>
                  <th>Yazıcı</th>
                  <th>Marka / Model</th>
                  <th>Tür</th>
                  <th className="num">Önceki Sayaç</th>
                  <th className="num">Şimdiki Sayaç</th>
                  <th className="num">Fark</th>
                  <th className="num">Sayfa Başı ₺</th>
                  <th className="num">Tutar ₺</th>
                </tr>
              </thead>
              <tbody>
                {data.lines.map((l, i) => (
                  <tr key={i}>
                    <td className="num muted">{i + 1}</td>
                    <td><strong>{l.printerName}</strong></td>
                    <td className="muted small">{l.model ?? "–"}</td>
                    <td>{l.turAd}</td>
                    <td className="num">{n(l.previousCounter)}</td>
                    <td className="num">{n(l.currentCounter)}</td>
                    <td className="num"><strong>{l.pages == null ? "?" : n(l.pages)}</strong></td>
                    <td className="num">{unit(l.unitPrice)}</td>
                    <td className="num"><strong>{money(l.amount)}</strong></td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <th colSpan={6} style={{ textAlign: "right" }}>TOPLAM</th>
                  <th className="num">{n(data.totalPages)}</th>
                  <th />
                  <th className="num">{money(data.totalAmount)}</th>
                </tr>
              </tfoot>
            </table>
          </div>

          <div style={{ display: "flex", gap: "var(--space-8)", marginTop: "var(--space-8)" }}>
            <div className="small muted" style={{ flex: 1, textAlign: "center" }}>Düzenleyen<br /><br /><br /><span style={{ borderTop: "1px solid #999", padding: "0 3rem" }}>İmza / Kaşe</span></div>
            <div className="small muted" style={{ flex: 1, textAlign: "center" }}>Teslim Alan<br /><br /><br /><span style={{ borderTop: "1px solid #999", padding: "0 3rem" }}>İmza / Kaşe</span></div>
          </div>
        </div>
      </div>
    </>
  );
}

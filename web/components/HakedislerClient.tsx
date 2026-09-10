"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { HakedisList } from "@/lib/types";
import { d, dt, money, n } from "@/lib/format";

export function HakedislerClient() {
  const [list, setList] = useState<HakedisList[]>([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState<{ kind: "ok" | "err"; text: string } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setList(await api.get<HakedisList[]>("/api/hakedisler"));
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı." });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function remove(h: HakedisList) {
    if (!confirm(`Hakediş ${h.number} silinsin mi? Bu belge tamamen silinir.`)) return;
    try {
      await api.del(`/api/hakedisler/${h.id}`);
      setMsg({ kind: "ok", text: `Hakediş ${h.number} silindi.` });
      await load();
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "Silinemedi." });
    }
  }

  return (
    <>
      <div className="page-head spread">
        <div>
          <h1>Hakedişler</h1>
          <p className="muted small">Oluşturulan hakediş belgeleri. Yeni belge bir tedarikçi seçilerek üretilir.</p>
        </div>
        <Link className="btn btn-primary" href="/hakedisler/yeni">+ Yeni hakediş</Link>
      </div>

      {msg && <div className={`alert ${msg.kind === "ok" ? "alert-ok" : "alert-err"}`} style={{ marginBottom: "var(--space-4)" }}>{msg.text}</div>}

      <div className="card">
        <div className="card-body p0">
          {loading ? (
            <p className="empty">Yükleniyor…</p>
          ) : list.length === 0 ? (
            <p className="empty">Henüz hakediş oluşturulmadı.</p>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>No</th>
                    <th>Tedarikçi</th>
                    <th>Düzenleme</th>
                    <th>Dönem</th>
                    <th className="num">Yazıcı</th>
                    <th className="num">Toplam sayfa</th>
                    <th className="num">Toplam tutar ₺</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {list.map((h) => (
                    <tr key={h.id}>
                      <td><strong>{h.number}</strong></td>
                      <td>{h.tedarikciAd || "–"}</td>
                      <td className="muted small">{dt(h.createdUtc)}</td>
                      <td className="muted small">{d(h.periodStart)} – {d(h.periodEnd)}</td>
                      <td className="num">{h.printerCount}</td>
                      <td className="num">{n(h.totalPages)}</td>
                      <td className="num">{money(h.totalAmount)}</td>
                      <td>
                        <div className="row-actions">
                          <Link className="btn btn-sm" href={`/hakedisler/${h.id}`}>Aç</Link>
                          <button className="btn btn-sm btn-danger" onClick={() => remove(h)}>Sil</button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </>
  );
}

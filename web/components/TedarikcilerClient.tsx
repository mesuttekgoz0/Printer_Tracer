"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { TedarikciList } from "@/lib/types";

export function TedarikcilerClient() {
  const [list, setList] = useState<TedarikciList[]>([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState<{ kind: "ok" | "err"; text: string } | null>(null);
  const [ad, setAd] = useState("");
  const [not, setNot] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setList(await api.get<TedarikciList[]>("/api/tedarikciler"));
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı." });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function add(e: React.FormEvent) {
    e.preventDefault();
    try {
      await api.post("/api/tedarikciler", { ad, not });
      setAd("");
      setNot("");
      setMsg({ kind: "ok", text: `${ad} eklendi.` });
      await load();
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "Eklenemedi." });
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>Tedarikçiler</h1>
        <p className="muted small">Yazıcı servis/bayi firmaları ve tür bazlı, tarih aralıklı sayfa-başı fiyatları.</p>
      </div>

      {msg && <div className={`alert ${msg.kind === "ok" ? "alert-ok" : "alert-err"}`} style={{ marginBottom: "var(--space-4)" }}>{msg.text}</div>}

      <div className="card" style={{ marginBottom: "var(--space-8)" }}>
        <div className="card-head">Yeni tedarikçi ekle</div>
        <div className="card-body">
          <form className="form-grid" onSubmit={add}>
            <div className="field">
              <label htmlFor="t-ad">Tedarikçi adı</label>
              <input id="t-ad" className="input" value={ad} onChange={(e) => setAd(e.target.value)} placeholder="ör. Olivetti Servis" required />
            </div>
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label htmlFor="t-not">Not (opsiyonel)</label>
              <input id="t-not" className="input" value={not} onChange={(e) => setNot(e.target.value)} maxLength={500} />
            </div>
            <button type="submit" className="btn btn-primary">Ekle</button>
          </form>
        </div>
      </div>

      <div className="card">
        <div className="card-head">Kayıtlı tedarikçiler</div>
        <div className="card-body p0">
          {loading ? (
            <p className="empty">Yükleniyor…</p>
          ) : list.length === 0 ? (
            <p className="empty">Henüz tedarikçi yok.</p>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Ad</th>
                    <th>Not</th>
                    <th className="num">Yazıcı</th>
                    <th className="num">Fiyat kaydı</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {list.map((t) => (
                    <tr key={t.id}>
                      <td><strong>{t.ad}</strong></td>
                      <td className="muted small">{t.not || "–"}</td>
                      <td className="num">{t.printerCount}</td>
                      <td className="num">
                        {t.detaySayisi === 0 ? <span className="neg">0</span> : t.detaySayisi}
                      </td>
                      <td><Link className="btn btn-sm" href={`/tedarikciler/${t.id}`}>Aç / Düzenle</Link></td>
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

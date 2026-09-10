"use client";

import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { ReadingsOverview, ReadResult } from "@/lib/types";
import { dt, n } from "@/lib/format";

export function ReadingsClient() {
  const [data, setData] = useState<ReadingsOverview | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<{ kind: "ok" | "err"; text: string } | null>(null);
  const [sel, setSel] = useState<Set<number>>(new Set());

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await api.get<ReadingsOverview>("/api/readings"));
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı." });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function read(ids: number[] | null) {
    setBusy(true);
    try {
      const r = await api.post<ReadResult>("/api/readings/read", { printerIds: ids });
      setMsg({ kind: r.success ? "ok" : "err", text: r.message });
      setSel(new Set());
      await load();
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "Okuma başarısız." });
    } finally {
      setBusy(false);
    }
  }

  const printers = data?.printers ?? [];
  const allChecked = printers.length > 0 && sel.size === printers.length;

  return (
    <>
      <div className="page-head spread">
        <div>
          <h1>Sayaç Oku</h1>
          <p className="muted small">Tek düğmeyle tüm ya da seçili yazıcıların o anki sayacını al.</p>
        </div>
      </div>

      {msg && <div className={`alert ${msg.kind === "ok" ? "alert-ok" : "alert-err"}`} style={{ marginBottom: "var(--space-4)" }}>{msg.text}</div>}

      <div className="card" style={{ marginBottom: "var(--space-8)" }}>
        <div className="card-body spread">
          <div>
            <div><strong>{data?.printerCount ?? 0}</strong> kayıtlı yazıcı</div>
            <div className="muted small">Son okuma: {dt(data?.lastReadingUtc ?? null)}</div>
          </div>
          <button className="btn btn-primary" disabled={busy || (data?.printerCount ?? 0) === 0} onClick={() => read(null)}>
            {busy ? "Okunuyor…" : "Tüm yazıcıları şimdi oku"}
          </button>
        </div>
      </div>

      <div className="card">
        <div className="card-head">
          <span>Son okumalar</span>
          <span className="row-actions">
            {data && data.totalDelta > 0 && (
              <span className="muted small">Son iki okuma arası toplam <strong>{n(data.totalDelta)}</strong> sayfa</span>
            )}
            <button
              className="btn btn-sm btn-primary"
              disabled={busy || sel.size === 0}
              onClick={() => read([...sel])}
            >
              Seçili yazıcıları oku
            </button>
          </span>
        </div>
        <div className="card-body p0">
          {loading ? (
            <p className="empty">Yükleniyor…</p>
          ) : printers.length === 0 ? (
            <p className="empty">Henüz yazıcı eklenmemiş.</p>
          ) : (
            <div style={{ overflowX: "auto", maxHeight: "60vh" }}>
              <table className="table">
                <thead>
                  <tr>
                    <th style={{ width: "2rem" }}>
                      <input
                        type="checkbox"
                        checked={allChecked}
                        onChange={(e) => setSel(e.target.checked ? new Set(printers.map((p) => p.printerId)) : new Set())}
                      />
                    </th>
                    <th>Yazıcı</th>
                    <th>Marka / Model</th>
                    <th>Tür</th>
                    <th>IP</th>
                    <th className="num">Önceki sayaç</th>
                    <th className="num">Son sayaç</th>
                    <th className="num">Fark</th>
                    <th>Önceki okuma</th>
                    <th>Son okuma</th>
                  </tr>
                </thead>
                <tbody>
                  {printers.map((p) => (
                    <tr key={p.printerId}>
                      <td>
                        <input
                          type="checkbox"
                          checked={sel.has(p.printerId)}
                          onChange={(e) => {
                            setSel((s) => {
                              const next = new Set(s);
                              e.target.checked ? next.add(p.printerId) : next.delete(p.printerId);
                              return next;
                            });
                          }}
                        />
                      </td>
                      <td><strong>{p.printerName}</strong></td>
                      <td className="muted small" style={{ maxWidth: "15rem" }} title={p.model ?? ""}>
                        {p.model ?? "–"}
                      </td>
                      <td>{p.turId == null ? <span className="muted">–</span> : <span className="tag">{p.turAd}</span>}</td>
                      <td className="muted"><code>{p.ipAddress}</code></td>
                      {!p.hasReading ? (
                        <td colSpan={5} className="muted">Henüz okunmadı — seçip yukarıdan okutun.</td>
                      ) : (
                        <>
                          <td className="num">{p.previousCounter != null ? n(p.previousCounter) : "–"}</td>
                          <td className="num"><strong>{n(p.latestCounter)}</strong></td>
                          <td className="num">
                            {p.previousCounter == null ? (
                              <span className="muted">ilk okuma</span>
                            ) : p.delta != null ? (
                              <strong>{n(p.delta)}</strong>
                            ) : (
                              <span className="muted" title="Sayaç geriye gitmiş">–</span>
                            )}
                          </td>
                          <td className="muted small">{dt(p.previousReadingUtc)}</td>
                          <td className="muted small">{dt(p.latestReadingUtc)}</td>
                        </>
                      )}
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

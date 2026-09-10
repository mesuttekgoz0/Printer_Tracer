"use client";

import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { ReportSummary } from "@/lib/types";
import { d, dt, iso, n } from "@/lib/format";

function monthStart(): string {
  const now = new Date();
  return iso(new Date(now.getFullYear(), now.getMonth(), 1));
}

export function ReportClient() {
  const [from, setFrom] = useState(monthStart());
  const [to, setTo] = useState(iso(new Date()));
  const [all, setAll] = useState(false);
  const [data, setData] = useState<ReportSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);

  const load = useCallback(async (f: string, t: string, a: boolean) => {
    setLoading(true);
    setErr(null);
    try {
      const q = new URLSearchParams({ from: f, to: t, all: String(a) });
      setData(await api.get<ReportSummary>(`/api/report/summary?${q}`));
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load(from, to, all);
    // ilk yük; sonraki yüklemeler butonlarla
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function shortcut(days: number) {
    const t = new Date();
    const f = new Date();
    f.setDate(f.getDate() - days + 1);
    setFrom(iso(f));
    setTo(iso(t));
    load(iso(f), iso(t), all);
  }

  return (
    <>
      <div className="page-head">
        <h1>Rapor</h1>
        <p className="muted small">Seçilen tarih aralığındaki tek tek okumalar, farkları ve dönem toplamları.</p>
      </div>

      <div className="card" style={{ marginBottom: "var(--space-6)" }}>
        <div className="card-body">
          <form
            className="form-grid"
            onSubmit={(e) => {
              e.preventDefault();
              load(from, to, all);
            }}
          >
            <div className="field">
              <label htmlFor="r-from">Başlangıç</label>
              <input id="r-from" type="date" className="input" value={from} onChange={(e) => setFrom(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="r-to">Bitiş</label>
              <input id="r-to" type="date" className="input" value={to} onChange={(e) => setTo(e.target.value)} />
            </div>
            <button type="submit" className="btn btn-primary">Getir</button>
          </form>
          <div className="row-actions" style={{ marginTop: "var(--space-3)" }}>
            <span className="muted small">Kısayol:</span>
            <button className="btn btn-sm" onClick={() => shortcut(7)}>Son 7 gün</button>
            <button className="btn btn-sm" onClick={() => shortcut(30)}>Son 30 gün</button>
            <button className="btn btn-sm" onClick={() => shortcut(90)}>Son 90 gün</button>
          </div>
        </div>
      </div>

      {err && <div className="alert alert-err" style={{ marginBottom: "var(--space-4)" }}>{err}</div>}

      {loading || !data ? (
        <p className="empty">Yükleniyor…</p>
      ) : (
        <>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "var(--space-4)", marginBottom: "var(--space-6)" }}>
            <Metric label="Dönem toplamı" value={`${n(data.grandTotal)} sayfa`} />
            <Metric label="Okuma sayısı" value={n(data.readingsTotal)} />
            <Metric label="Aralık" value={`${d(data.from)} – ${d(data.to)}`} />
          </div>

          <div className="card" style={{ marginBottom: "var(--space-6)" }}>
            <div className="card-head">Yazıcı bazında dönem toplamı</div>
            <div className="card-body p0">
              {data.printerTotals.length === 0 ? (
                <p className="empty">Bu aralıkta kayıtlı baskı yok.</p>
              ) : (
                <table className="table">
                  <thead>
                    <tr>
                      <th>Yazıcı</th>
                      <th className="num">Sayfa</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.printerTotals.map((p) => (
                      <tr key={p.printerId}>
                        <td>{p.name}</td>
                        <td className="num">{n(p.total)}</td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr>
                      <th>Toplam</th>
                      <th className="num">{n(data.grandTotal)}</th>
                    </tr>
                  </tfoot>
                </table>
              )}
            </div>
          </div>

          <div className="card">
            <div className="card-head">
              <span>Okumalar</span>
              <button
                className="btn btn-sm"
                onClick={() => {
                  const next = !all;
                  setAll(next);
                  load(from, to, next);
                }}
              >
                {data.showAll ? "Sadece son okumalar" : "Tümünü göster"}
              </button>
            </div>
            <div className="card-body p0">
              {data.readings.length === 0 ? (
                <p className="empty">Okuma yok.</p>
              ) : (
                <div style={{ overflowX: "auto", maxHeight: "34rem" }}>
                  <table className="table">
                    <thead>
                      <tr>
                        <th>Zaman</th>
                        <th>Yazıcı</th>
                        <th className="num">Sayaç</th>
                        <th className="num">Fark</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.readings.map((r, i) => (
                        <tr key={i}>
                          <td className="muted small">{dt(r.timestampUtc)}</td>
                          <td>{r.printerName}</td>
                          <td className="num">{n(r.pageCount)}</td>
                          <td className="num">
                            {r.delta == null ? <span className="muted">–</span> : r.delta > 0 ? <strong>+{n(r.delta)}</strong> : "0"}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              <div className="muted small" style={{ padding: "var(--space-2) var(--space-3)" }}>
                {data.showAll ? `${data.readingsTotal} okuma` : `son ${data.readings.length} / ${data.readingsTotal} okuma`}
              </div>
            </div>
          </div>
        </>
      )}
    </>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="card">
      <div className="card-body">
        <div className="muted small" style={{ textTransform: "uppercase", letterSpacing: "0.08em", fontSize: "0.7rem" }}>{label}</div>
        <div style={{ fontFamily: "var(--font-heading)", fontSize: "1.6rem", fontWeight: 600 }}>{value}</div>
      </div>
    </div>
  );
}

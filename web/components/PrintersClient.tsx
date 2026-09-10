"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Lookups, Printer } from "@/lib/types";

const tr = new Intl.NumberFormat("tr-TR");

function dt(iso: string | null): string {
  if (!iso) return "–";
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  const p = (n: number) => String(n).padStart(2, "0");
  return `${p(d.getDate())}.${p(d.getMonth() + 1)}.${d.getFullYear()} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

export function PrintersClient() {
  const [printers, setPrinters] = useState<Printer[]>([]);
  const [lookups, setLookups] = useState<Lookups>({ turler: [], tedarikciler: [] });
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState<{ kind: "ok" | "err"; text: string } | null>(null);

  const [name, setName] = useState("");
  const [ip, setIp] = useState("");
  const [turId, setTurId] = useState("");
  const [tedarikciId, setTedarikciId] = useState("");
  const [adding, setAdding] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [p, l] = await Promise.all([
        api.get<Printer[]>("/api/printers"),
        api.get<Lookups>("/api/lookups"),
      ]);
      setPrinters(p);
      setLookups(l);
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı." });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function run(fn: () => Promise<unknown>, ok: string) {
    try {
      await fn();
      setMsg({ kind: "ok", text: ok });
      await load();
    } catch (e) {
      setMsg({ kind: "err", text: e instanceof ApiError ? e.message : "İşlem başarısız." });
    }
  }

  async function addPrinter(e: React.FormEvent) {
    e.preventDefault();
    setAdding(true);
    await run(async () => {
      await api.post("/api/printers", {
        name,
        ipAddress: ip,
        turId: turId ? Number(turId) : null,
        tedarikciId: tedarikciId ? Number(tedarikciId) : null,
      });
      setName("");
      setIp("");
      setTurId("");
      setTedarikciId("");
    }, `${name} eklendi.`);
    setAdding(false);
  }

  return (
    <>
      <div className="page-head">
        <h1>Yazıcılar</h1>
        <p className="muted small">
          Ağa doğrudan bağlı yazıcıların listesi. Eklenen yazıcı yalnızca kaydedilir; ilk sayaç için Sayaç Oku sayfasını kullanın.
        </p>
      </div>

      {msg && <div className={`alert ${msg.kind === "ok" ? "alert-ok" : "alert-err"}`} style={{ marginBottom: "var(--space-4)" }}>{msg.text}</div>}

      <div className="card" style={{ marginBottom: "var(--space-8)" }}>
        <div className="card-head">Yeni yazıcı ekle</div>
        <div className="card-body">
          <form className="form-grid" onSubmit={addPrinter}>
            <div className="field">
              <label htmlFor="p-name">Yazıcı adı</label>
              <input id="p-name" className="input" value={name} onChange={(e) => setName(e.target.value)} placeholder="ör. Muhasebe" required />
            </div>
            <div className="field">
              <label htmlFor="p-ip">IP adresi</label>
              <input id="p-ip" className="input tnum" value={ip} onChange={(e) => setIp(e.target.value)} placeholder="192.168.1.51" required />
            </div>
            <div className="field">
              <label htmlFor="p-tur">Tür</label>
              <select id="p-tur" className="select" value={turId} onChange={(e) => setTurId(e.target.value)}>
                <option value="">Belirtilmemiş</option>
                {lookups.turler.map((t) => (
                  <option key={t.id} value={t.id}>{t.ad}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="p-ted">Tedarikçi</label>
              <select id="p-ted" className="select" value={tedarikciId} onChange={(e) => setTedarikciId(e.target.value)}>
                <option value="">—</option>
                {lookups.tedarikciler.map((t) => (
                  <option key={t.id} value={t.id}>{t.ad}</option>
                ))}
              </select>
            </div>
            <button type="submit" className="btn btn-primary" disabled={adding}>Ekle</button>
          </form>
        </div>
      </div>

      <div className="card">
        <div className="card-head">
          <span>Kayıtlı yazıcılar</span>
          <span className="muted small">{printers.length} cihaz</span>
        </div>
        <div className="card-body p0">
          {loading ? (
            <p className="empty">Yükleniyor…</p>
          ) : printers.length === 0 ? (
            <p className="empty">Henüz kayıtlı yazıcı yok.</p>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Ad</th>
                    <th>IP adresi</th>
                    <th>Tür</th>
                    <th>Tedarikçi</th>
                    <th className="num">Okuma</th>
                    <th>Son okuma</th>
                    <th className="num">Son sayaç</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {printers.map((p) => (
                    <PrinterRow key={p.id} p={p} lookups={lookups} run={run} />
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

function PrinterRow({
  p,
  lookups,
  run,
}: {
  p: Printer;
  lookups: Lookups;
  run: (fn: () => Promise<unknown>, ok: string) => Promise<void>;
}) {
  const [name, setName] = useState(p.name);

  return (
    <tr>
      <td>
        <div className="row-actions">
          <input className="input select-sm" style={{ minWidth: "9rem" }} value={name} onChange={(e) => setName(e.target.value)} />
          <button
            className="btn btn-sm"
            disabled={name.trim() === p.name || !name.trim()}
            onClick={() => run(() => api.patch(`/api/printers/${p.id}/name`, { name }), "Yazıcı adı güncellendi.")}
          >
            Kaydet
          </button>
        </div>
      </td>
      <td className="muted"><code>{p.ipAddress}</code></td>
      <td>
        <select
          className="select select-sm"
          value={p.turId ?? ""}
          onChange={(e) =>
            run(() => api.patch(`/api/printers/${p.id}/type`, { turId: e.target.value ? Number(e.target.value) : null }), "Tür güncellendi.")
          }
        >
          <option value="">Belirtilmemiş</option>
          {lookups.turler.map((t) => (
            <option key={t.id} value={t.id}>{t.ad}</option>
          ))}
        </select>
      </td>
      <td>
        <select
          className="select select-sm"
          value={p.tedarikciId ?? ""}
          onChange={(e) =>
            run(
              () => api.patch(`/api/printers/${p.id}/supplier`, { tedarikciId: e.target.value ? Number(e.target.value) : null }),
              "Tedarikçi güncellendi.",
            )
          }
        >
          <option value="">—</option>
          {lookups.tedarikciler.map((t) => (
            <option key={t.id} value={t.id}>{t.ad}</option>
          ))}
        </select>
      </td>
      <td className="num">{tr.format(p.readingCount)}</td>
      <td className="muted small">{dt(p.latestReadingUtc)}</td>
      <td className="num">{p.latestCounter != null ? tr.format(p.latestCounter) : "–"}</td>
      <td>
        <div className="row-actions">
          <Link className="btn btn-sm" href={`/snmp-tanilama?ip=${encodeURIComponent(p.ipAddress)}`}>
            Test et
          </Link>
          <button
            className="btn btn-sm btn-danger"
            onClick={() => {
              if (confirm(`${p.name} (${p.ipAddress}) silinsin mi? Geçmiş okumaları da silinecek.`)) {
                run(() => api.del(`/api/printers/${p.id}`), `${p.name} silindi.`);
              }
            }}
          >
            Sil
          </button>
        </div>
      </td>
    </tr>
  );
}
